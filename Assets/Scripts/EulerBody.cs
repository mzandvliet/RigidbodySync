using UnityEngine;

public interface IRigidbody {
    RigidbodyConfig Config { get; set; }
    RigidbodyState State { get; set; }

    void AddForce(Vector3 force, ForceMode forceMode);
    void AddRelativeForce(Vector3 force, ForceMode mode);
    void AddTorque(Vector3 torque, ForceMode forceMode);
    void AddRelativeTorque(Vector3 force, ForceMode mode);
}

[System.Serializable]
public struct RigidbodyConfig {
    [SerializeField] public float Mass;
    [SerializeField] public float LinearDampening;
    [SerializeField] public float AngularDampening;
    [SerializeField] public Vector3 InertiaTensor;
    [SerializeField] public Quaternion InertiaTensorRotation;

    public RigidbodyConfig(float mass, float linearDampening, float angularDampening, Vector3 inertiaTensor, Quaternion inertiaTensorRotation) {
        Mass = mass;
        LinearDampening = linearDampening;
        AngularDampening = angularDampening;
        InertiaTensor = inertiaTensor;
        InertiaTensorRotation = inertiaTensorRotation;
    }

    public override string ToString() {
        return string.Format("Mass: {0}, LinearDampening: {1}, AngularDampening: {2}, InertiaTensor: ({3:0.00}, {4:0.00}, {5:0.00})", Mass, LinearDampening, AngularDampening, InertiaTensor.x, InertiaTensor.y, InertiaTensor.z);
    }
}

public class EulerBody : IRigidbody {
    private RigidbodyConfig _config;
    private RigidbodyState _state;
    private Vector3 _linearForceToIntegrate;
    private Vector3 _torqueToIntegrate;

    public RigidbodyConfig Config {
        get { return _config; }
        set { _config = value; }
    }

    public RigidbodyState State
    {
        get { return _state; }
        set {
            _state = value;
            Reset();
        }
    }

    public EulerBody(RigidbodyConfig config, RigidbodyState state) {
        _config = config;
        _state = state;

        Reset();
    }

    public void AddForce(Vector3 force, ForceMode forceMode) {
        _linearForceToIntegrate += force;
    }

    public void AddRelativeForce(Vector3 force, ForceMode forceMode) {
        _linearForceToIntegrate += _state.Rotation * force;
    }

    public void AddTorque(Vector3 torque, ForceMode forceMode) {
        _torqueToIntegrate += torque;
    }

    public void AddRelativeTorque(Vector3 torque, ForceMode forceMode) {
        _torqueToIntegrate += _state.Rotation * torque;
    }

    public void Integrate(float dt) {
        /* Linear force to linear acceleration */

        _linearForceToIntegrate /= _config.Mass;

        /* Torque to angular acceleration */

        // Todo: Inertia Tensor isn't handled properly yet. Can't construct one ourselves, for example.

        Quaternion tensorSpace = _state.Rotation*Config.InertiaTensorRotation;

        Vector3 localAngularAcceleration = Quaternion.Inverse(tensorSpace) * _torqueToIntegrate;
        localAngularAcceleration.x *= (1f / _config.InertiaTensor.x);
        localAngularAcceleration.y *= (1f / _config.InertiaTensor.y);
        localAngularAcceleration.z *= (1f / _config.InertiaTensor.z);
        _torqueToIntegrate = tensorSpace * localAngularAcceleration;

        /* Apply dampening */

        _linearForceToIntegrate -= _state.Velocity * _config.LinearDampening;
        _torqueToIntegrate -= State.AngularVelocity * _config.AngularDampening;

        /* Integrate */

        _state.Velocity += _linearForceToIntegrate * dt;
        _state.Position += _state.Velocity * dt;

        _state.AngularVelocity += _torqueToIntegrate * dt;
        Quaternion rotationDelta = Quaternion.Euler((_state.AngularVelocity*dt)*Mathf.Rad2Deg);
        //Quaternion rotationDelta = Quaternion.AngleAxis(_state.AngularVelocity.magnitude *dt*Mathf.Rad2Deg, _state.AngularVelocity.normalized);
        _state.Rotation = rotationDelta * _state.Rotation;

        /* Store derivatives */

        _state.Acceleration = _linearForceToIntegrate;
        _state.AngularAcceleration = _torqueToIntegrate;

        /* Reset aggregated state for next frame */

        Reset();
    }

    private void Reset() {
        _linearForceToIntegrate = Vector3.zero;
        _torqueToIntegrate = Vector3.zero;
    }
}