using System.Collections;
using UnityEngine;

/* Todo:
 * - Figure out how to apply same principle to predicting rotation
 * 
 * Try deriving a rigidbody's acceleration by constructing a spline around sampled points
 * and finding slopes of that spline near the end point. Might result in less
 * numerical instability.
 */

public class SplineTest : MonoBehaviour {
    [SerializeField] private PlayerSimulation _target;

    [SerializeField] private float _predictStart = -1f;
    [SerializeField] private float _predictEnd = 2f;

    private RigidbodyState _oldState;
    private RigidbodyState _newState;

    private void Start() {
        StartCoroutine(OnDeserialize());
    }

    private IEnumerator OnDeserialize() {
        while (true) {
            _oldState = _newState;
            //_newState = RigidbodyExtensions.ToImmutable(_target.Rigidbody, _target.Acceleration, Vector3.zero, Time.fixedDeltaTime);

            yield return new WaitForSeconds(2f);
        }
    }

    private void DrawHermite() {
        Gizmos.color = Color.cyan;

        Gizmos.DrawRay(_oldState.Position, _oldState.Velocity);
        Gizmos.DrawRay(_newState.Position, _newState.Velocity);

        Vector3 p0 = _oldState.Position;
        Vector3 p1 = _oldState.Position + _oldState.Velocity;
        Vector3 p2 = _newState.Position + _newState.Velocity * _predictEnd + 0.5f * _newState.Acceleration * (_predictEnd * _predictEnd);
        Vector3 p3 = p2 - _newState.Velocity + _newState.Acceleration * _predictEnd;

        Gizmos.color = Color.blue;

        int res = 16;
        for (int i = 0; i < res; i++) {
            float tA = i / (float)(res - 1);
            float tB = i / (float)(res);
            Vector3 pointA = SplineUtil.Hermite(p0, p1, p2, p3, tA);
            Vector3 pointB = SplineUtil.Hermite(p0, p1, p2, p3, tB);
            Gizmos.DrawLine(pointA, pointB);
        }
    }

    private void DrawHermitePrediction() {
        Gizmos.color = Color.cyan;

        Gizmos.DrawRay(_oldState.Position, _oldState.Velocity);
        Gizmos.DrawRay(_newState.Position, _newState.Velocity);

        Gizmos.color = Color.blue;

        int res = 32;
        Vector3[] points = new Vector3[res];

        for (int i = 0; i < res; i++) {
            float t = Mathf.Lerp(_predictStart, _predictEnd, i / (float) res);
            Vector3 p0 = _oldState.Position;
            Vector3 p1 = _oldState.Position + _oldState.Velocity;
            Vector3 p2 = _newState.Position + _newState.Velocity * t + 0.5f * _newState.Acceleration * (t * t);
            Vector3 p3 = p2 - _newState.Velocity + _newState.Acceleration * t;

            points[i] = SplineUtil.Hermite(p0, p1, p2, p3, 1.0f);
        }

        for (int i = 0; i < points.Length-1; i++) {
            Gizmos.DrawLine(points[i], points[i+1]);
        }
    }

    private void OnDrawGizmos() {
        DrawHermitePrediction();
    }
}
