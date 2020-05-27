using UnityEngine;

public class PlayerCamera : MonoBehaviour {
    [SerializeField] private PlayerSimulation _target;
    [SerializeField] private float _distance = 24f;
    [SerializeField] private Vector3 _orbitRotation;

    private Vector3 _lookDirection;

    public PlayerSimulation Target
    {
        get { return _target; }
        set { _target = value; }
    }

    private void Awake() {
        _lookDirection = Vector3.forward;
    }

    private void Update() {
        if (!_target) {
            return;
        }

        var position = _target.UnityBody.State.Position;
        var velocity = _target.UnityBody.State.Velocity;
        _lookDirection = Vector3.Slerp(_lookDirection, velocity.normalized, Mathf.Min(1f, velocity.magnitude) * 5f * Time.deltaTime);

        //transform.rotation = Quaternion.LookRotation(_lookDirection, Vector3.up);
        transform.rotation = Quaternion.Lerp(transform.rotation, _target.transform.rotation * Quaternion.Euler(_orbitRotation), 2f * Time.deltaTime);
        transform.position = position - transform.rotation * Vector3.forward * _distance;
        //transform.position += Vector3.up * 1f;
    }
}
