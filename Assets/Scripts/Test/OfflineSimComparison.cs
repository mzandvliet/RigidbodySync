using RamjetAnvil.RigidbodyParty;
using UnityEngine;

public class OfflineSimComparison : MonoBehaviour {
    [SerializeField]
    private PlayerSimulationConfig _simConfig;
    [SerializeField]
    private UnityBody _unityBody;

    private EulerBody _eulerBody;

    private void Start() {
        var config = _unityBody.Config;
        _eulerBody = new EulerBody(config, _unityBody.State);

        SimulateOffline();
    }

    private void FixedUpdate() {
        if (Time.time > 5f) {
            _unityBody.Rigidbody.isKinematic = true;
            _unityBody.enabled = false;
            return;
        }

        PlayerSimulation.Simulate(_simConfig, _unityBody, GetInput(Time.fixedTime));
    }

    private void SimulateOffline() {
        PlayerSimulation.SimulateOffline(_simConfig, _eulerBody, GetInput, 0d, 5d, Time.fixedDeltaTime);

        transform.position = _eulerBody.State.Position;
        transform.rotation = _eulerBody.State.Rotation;
    }

    private PlayerInput GetInput(double time) {
        return new PlayerInput {
            Thrust = (time > 1f && time < 3f) ? 1f : 0f,
            Pitch = (time < 2f) ? 1f : 0f,
            Roll = (time < 1f) ? 1f : 0f
        };
    }
}