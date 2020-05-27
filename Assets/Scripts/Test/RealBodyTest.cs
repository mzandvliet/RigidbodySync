using RamjetAnvil.RigidbodyParty;
using UnityEngine;

public class RealBodyTest : MonoBehaviour {
    [SerializeField] private PlayerSimulationConfig _config;
    [SerializeField] private UnityBody _body;

    private void FixedUpdate() {
        //_body.AddTorque(new Vector3(10f * Input.GetAxis("Roll"), 0f, 0f), ForceMode.Force);
        PlayerSimulation.Simulate(_config, _body, SampleInput());
    }

    public static PlayerInput SampleInput() {
        return new PlayerInput {
            Timestamp = Time.time,
            Thrust = Input.GetAxis("Thrust"),
            Pitch = Input.GetAxis("Pitch"),
            Roll = Input.GetAxis("Roll"),
            Yaw = Input.GetAxis("Yaw"),
            Boost = Input.GetButton("Boost")
        };
    }
}
