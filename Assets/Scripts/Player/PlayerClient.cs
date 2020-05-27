using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Unity.Utility;
using RamjetAnvil.Util;
using UnityEngine;

/*
 * - Todo: Handle collisions better
 */

[RequireComponent(typeof(PlayerSimulation))]
[NetworkRole(ObjectRoles.NonAuthoritive)]
public class PlayerClient : MonoBehaviour, IUpdate, IFixedUpdate, IResetable {

    [Dependency("localRealtimeClock"), SerializeField] private AbstractUnityClock _localRealtimeClock;
    [Dependency("clock"), SerializeField] private AbstractUnityClock _clock;
    [Dependency("fixedClock"), SerializeField] private AbstractUnityClock _fixedClock;
    [Dependency] private GameObjectNetworkInfo _objectNetworkInfo;
    [SerializeField] private Renderer _renderer;

    [SerializeField] private double _lastReceivedStateTimestamp = 0.0;
    [SerializeField] private double _lastReceivedStateRTT = 0.0f;
    private RigidbodyState _lastServerState;

    [SerializeField] private float _errorCorrectionSpeed = 10f;
    [SerializeField] private float _errorSmoothing = 1.0f;
    private PlayerSimulation _simulation;

    private ICircularBuffer<PlayerInput> _inputHistory;
    private ICircularBuffer<RigidbodyState> _serverHistory; // Todo: only keep most recent, buffer is just for debug
    private RigidbodyState _correctedState;
    private EulerBody _eulerBody;

    [SerializeField] private bool _applyCorrection = true;
    private float _posError;
    private float _rotError;
    private float _posErrorSmooth;
    private float _rotErrorSmooth;

    public bool ApplyCorrection {
        get { return _applyCorrection; }
        set { _applyCorrection = value; }
    }

    private void Awake() {
        const float bufferLenghtInS = 5f;
        _simulation = GetComponent<PlayerSimulation>();
        _inputHistory = new CircularBuffer<PlayerInput>(Mathf.RoundToInt(60 * bufferLenghtInS));
        _serverHistory = new CircularBuffer<RigidbodyState>(50); // Todo: in external debug vizualizer component
    }

    private void Start() {
        _eulerBody = new EulerBody(_simulation.UnityBody.Config, _simulation.UnityBody.State);
    }

    public void OnFixedUpdate(IClock clock) {
        //Debug.Log("client fixed update " + clock.FrameCount);

        // First we simulate 
        _simulation.SimulateUnity(clock.DeltaTime);
        
        // Then we apply any correction from the server
        Correct(clock);
    }

    public void OnUpdate(IClock clock) {
        _renderer.material.color = Color.Lerp(Color.white, Color.red, _posError);
    }

    private void Correct(IClock fixedClock) {
        if (_serverHistory.Count < 1) {
            Debug.Log("no server history available");
            return;
        }

        /* Extrapolate last received server state to catch up to client time */

        /* Todo: Save massive amounts of calculation by only simulating 1 correction tick per actual physics frame, you dummy
         * New server state? Do offline sim for however many steps it requires to catch up to local client time instantly.
         * THEN, then you only have to simulate one tick for each real-time physics tick in order to keep up. Until the new server state arrives.
         * 
         * So in this method we only tick once
         * In the receive state handler we tick multiple times to catch up to local client time
         */

        var latency = _lastReceivedStateRTT / 2d;
        var timeSinceLastReceivedState = fixedClock.CurrentTime - _lastReceivedStateTimestamp;
        var timeSinceServerState = latency + timeSinceLastReceivedState;
        var startTime = _lastReceivedStateTimestamp - latency;

        Debug.Assert(timeSinceServerState > 0.0, "timeSinceServerState > 0: " + timeSinceServerState);

        _eulerBody.State = _lastServerState;
        PlayerSimulation.SimulateOffline(_simulation.Config, _eulerBody, t => _inputHistory.Lerp(t), startTime, timeSinceServerState, fixedClock.DeltaTime);
        _correctedState = _eulerBody.State;

        /* Calculate error between corrected server state and current client state */

        var currentClientState = _simulation.UnityBody.State;

        const float maxPosError = 2f;
        const float maxRotError = 45f;
        _posError = Vector3.Distance(currentClientState.Position, _correctedState.Position) / maxPosError;
        _rotError = Quaternion.Angle(currentClientState.Rotation, _correctedState.Rotation) / maxRotError;

        /* Apply corrected state over time, with use of error metrics */

        _posErrorSmooth = Mathf.Lerp(_posErrorSmooth, 0.5f + _posError, 1f / _errorSmoothing * fixedClock.DeltaTime);
        _rotErrorSmooth = Mathf.Lerp(_rotErrorSmooth, 0.5f + _rotError, 1f / _errorSmoothing * fixedClock.DeltaTime);

        var interpolatedState = RigidbodyExtensions.Lerp(
            currentClientState, _correctedState, 
            _posErrorSmooth * fixedClock.DeltaTime * _errorCorrectionSpeed, 
            _rotErrorSmooth * fixedClock.DeltaTime * _errorCorrectionSpeed);

        if (_applyCorrection) {
            interpolatedState.ApplyTo(_simulation.UnityBody.Rigidbody);
        }
    }
    
    // Todo: for the OTHER role, handling input interpolation works COMPLETELY DIFFERENT from OWNER!!!!!!

    [MessageHandler]
    private void OnReceiveInput(PlayerMessage.InputUpdate message) {
        var input = message.Input;

        input.Timestamp = _fixedClock.CurrentTime;

        _simulation.SetInput(input);
        _inputHistory.Enqueue(input);
    }

    [MessageHandler]
    private void OnReceiveState(PlayerMessage.StateUpdate message, MessageMetadata metadata) {
        _lastServerState = message.State;
        _lastReceivedStateTimestamp = _fixedClock.CurrentTime;
        _lastReceivedStateRTT = RamjetClient.RoundtripTime;

        _serverHistory.Enqueue(message.State);
    }

    [MessageHandler]
    private void OnPlayerCollision(PlayerMessage.CollisionEvent _) {

    }

    #region Debug drawing logic

    private void OnDrawGizmos() {
        if (!Application.isPlaying) {
            return;
        }

        /* Draw server states */

        Gizmos.color = Color.blue;

        for (int i = 0; i < _serverHistory.Count - 1; i++) {
            Gizmos.DrawLine(_serverHistory[i].Position, _serverHistory[i + 1].Position);
        }

        for (int i = 0; i < _serverHistory.Count; i++) {
            var rigidbodyState = _serverHistory[i];
            Gizmos.DrawSphere(rigidbodyState.Position, 0.33f);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(_correctedState.Position, 0.5f);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(_correctedState.Position, _correctedState.Rotation * (Vector3.right * 10f));
        Gizmos.color = Color.green;
        Gizmos.DrawRay(_correctedState.Position, _correctedState.Rotation * (Vector3.up * 10f));
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(_correctedState.Position, _correctedState.Rotation * (Vector3.forward * 10f));
    }

    private void OnGUI() {
        if (_objectNetworkInfo.Role.IsOwner()) {
            GUILayout.BeginArea(new Rect(0f, Screen.height - 50f, 200f, 50f));
            GUILayout.Label("Speed: " + Mathf.RoundToInt(_simulation.UnityBody.State.Velocity.magnitude * 3.6f) + " km/h");
            GUILayout.EndArea();   
        }
    }
    #endregion

    public void Reset() {
        _inputHistory.Clear();
        _serverHistory.Clear();
        _posError = 0f;
        _rotError = 0f;
        _posErrorSmooth = 0f;
        _rotErrorSmooth = 0f;
    }
}

