using RamjetAnvil.Unity.Utility;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class UnityBody : MonoBehaviour, IRigidbody {
    private Rigidbody _rigidbody;

    private Vector3 _lastVelocity;
    private Vector3 _acceleration;
    private Vector3 _lastAngularVelocity;
    private Vector3 _angularAcceleration;

    public Rigidbody Rigidbody {
        get { return _rigidbody; }
    }

    public RigidbodyConfig Config {
        get {
            return new RigidbodyConfig(
                _rigidbody.mass,
                _rigidbody.drag,
                _rigidbody.angularDrag,
                _rigidbody.inertiaTensor,
                _rigidbody.inertiaTensorRotation);
        }
        set {
            _rigidbody.mass = value.Mass;
            _rigidbody.drag = value.LinearDampening;
            _rigidbody.angularDrag = value.AngularDampening;
            _rigidbody.inertiaTensor = value.InertiaTensor;
            _rigidbody.inertiaTensorRotation = value.InertiaTensorRotation;
        }
    }

    public RigidbodyState State {
        get { return _rigidbody.ToImmutable(_acceleration, _angularAcceleration); }
        set {
            _acceleration = value.Acceleration;
            _angularAcceleration = value.AngularAcceleration;
            // Reverse-time integration to find suitable last-frame state
            _lastVelocity = value.Velocity - value.Acceleration * Time.fixedDeltaTime;
            _lastAngularVelocity = value.AngularVelocity - value.AngularAcceleration * Time.fixedDeltaTime;
            value.ApplyTo(_rigidbody);
        }
    }

    public void AddForce(Vector3 force, ForceMode mode) {
        _rigidbody.AddForce(force, mode);
    }

    public void AddRelativeForce(Vector3 force, ForceMode mode) {
        _rigidbody.AddRelativeForce(force, mode);
    }

    public void AddTorque(Vector3 torque, ForceMode forceMode) {
        _rigidbody.AddTorque(torque, forceMode);
    }

    public void AddRelativeTorque(Vector3 torque, ForceMode mode) {
        _rigidbody.AddRelativeTorque(torque, mode);
    }

    public void UpdateDerivates(float deltaTime) {
        // Todo: use less noisy derivation of acceleration
        _acceleration = (_rigidbody.velocity - _lastVelocity) / deltaTime;
        _lastVelocity = _rigidbody.velocity;

        _angularAcceleration = (_rigidbody.angularVelocity - _lastAngularVelocity) / deltaTime;
        _lastAngularVelocity = _rigidbody.angularVelocity;
    }

    public void Reset() {
        _lastVelocity = Vector3.zero;
        _acceleration = Vector3.zero;
        _lastAngularVelocity = Vector3.zero;
        _angularAcceleration = Vector3.zero;
        RigidbodyExtensions.Reset(_rigidbody);
    }

    private void Awake() {
        _rigidbody = gameObject.GetComponent<Rigidbody>();
    }
}