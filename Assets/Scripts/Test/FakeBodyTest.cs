using RamjetAnvil.RigidbodyParty;
using UnityEngine;

public class FakeBodyTest : MonoBehaviour {
    [SerializeField] private PlayerSimulationConfig _simConfig;
    [SerializeField] private UnityBody _sourceBody;

    private EulerBody _body;

    private void Start() {
        var config = _sourceBody.Config;
        Debug.Log(config);
        _body = new EulerBody(config, _sourceBody.State);
    }

    private void FixedUpdate() {
        //_body.AddTorque(new Vector3(10f * Input.GetAxis("Roll"), 0f, 0f), ForceMode.Force);
        PlayerSimulation.Simulate(_simConfig, _body, RealBodyTest.SampleInput());

        _body.Integrate(Time.fixedDeltaTime);
        transform.position = _body.State.Position;
        transform.rotation = _body.State.Rotation;
    }
}