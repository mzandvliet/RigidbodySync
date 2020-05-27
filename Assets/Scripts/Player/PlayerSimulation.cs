using System;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Unity.Utility;
using RamjetAnvil.Util;
using UnityEngine;

public class PlayerSimulation : MonoBehaviour, IResetable {
    [SerializeField] private UnityBody _body;
    [SerializeField] private PlayerSimulationConfig _config = new PlayerSimulationConfig {
        RotationSpeed = Vector3.one
    };

    private PlayerInput _input;
    
    public UnityBody UnityBody {
        get { return _body; }
    }

    public PlayerSimulationConfig Config {
        get { return _config; }
    }

    public void SetInput(PlayerInput input) {
        _input = input;
    }

    /* 
     * Actual per-frame simulation step that translates player's input to forces.
     * Important: call once every fixed frame
     */

    public void SimulateUnity(float deltaTime) {
        _body.UpdateDerivates(deltaTime);
        Simulate(_config, _body, _input);
    }

    /* Offline Simulation */

    public static void SimulateOffline(PlayerSimulationConfig config, EulerBody body, Func<double, PlayerInput> inputSampler, double startTime, double duration, float dt) {

        int numSteps = Mathf.RoundToInt((float)duration / dt);
        double time = startTime;

        // Error is introduced by losing the fraction part of duration/timestep
        // can be, say, a 1 meter translation difference if moving at 100m/s

        for (int i = 0; i < numSteps; i++) {
            var input = inputSampler(time);
            Simulate(config, body, input);
            body.Integrate(dt);

            time += dt;
        }
    }
    
    public static void Simulate(PlayerSimulationConfig config, IRigidbody body, PlayerInput input) {
        Vector3 forward = body.State.Rotation * Vector3.forward;
        Vector3 up = body.State.Rotation * Vector3.up;

        float speedFactor = Mathf.Clamp01((body.State.Velocity.magnitude - 5f) / 10f);
        float forwardDot = Mathf.Clamp01(1f - forward.y);
        float rollAngle = MathUtils.AngleAroundAxis(up, Vector3.up, forward);
        float rollInducedBank = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(rollAngle) * MathUtils.OneOver180 * forwardDot * speedFactor), 1.5f) * Mathf.Sign(rollAngle);

        float rollStabilizer = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(rollAngle) * MathUtils.OneOver180 * forwardDot), 2f) * Mathf.Sign(rollAngle);
        rollStabilizer *= 1f - Mathf.Abs(input.Roll);

        body.AddRelativeTorque(new Vector3(
            input.Pitch * config.RotationSpeed.x + Mathf.Abs(rollInducedBank) * -0.66f * config.RotationSpeed.x,
            input.Yaw * config.RotationSpeed.y + rollInducedBank * 0.66f * config.RotationSpeed.y,
            input.Roll * -config.RotationSpeed.z + rollStabilizer * 0.66f * config.RotationSpeed.z),
            ForceMode.Force);

        float maxThrust = input.Boost ? 50f : 20f;
        body.AddRelativeForce(Vector3.forward * maxThrust * input.Thrust, ForceMode.Force);

        Vector3 offHeadingVelocity = body.State.Velocity - Vector3.Project(body.State.Velocity, forward);
        Vector3 dampening = offHeadingVelocity * -2.0f;
        Vector3 conserved = offHeadingVelocity.magnitude * forward * 0.33f;
        body.AddForce(dampening + conserved + Vector3.down, ForceMode.Force);
    }

    public void Reset() {
        _input = PlayerInput.Zero;
        _body.Reset();
    }
}

[Serializable]
public struct PlayerSimulationConfig {
    public Vector3 RotationSpeed;
}