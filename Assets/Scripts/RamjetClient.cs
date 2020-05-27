using System;
using System.Collections;
using System.Collections.Generic;
using Lidgren.Network;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Util;
using UnityEngine;
using System.Net;
using RamjetAnvil.Unity.Utility;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

public class RamjetClient : MonoBehaviour {

    [SerializeField] private Ipv4Endpoint _natFacilitatorEndpoint = new Ipv4Endpoint("95.85.31.166", 15493);
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private UnityCoroutineScheduler _coroutineScheduler;
    [SerializeField] private AbstractUnityControllableClock _synchedGameClock;
    [SerializeField] private AbstractUnityControllableClock _syncedFixedClock;
    [SerializeField] private PlayerCamera _camera;

    [SerializeField] private LidgrenNetworkTransporter _transporter;
    [SerializeField] private QueueingMessageSender _messageTransporter;

    public event Action<IPEndPoint, BasicMessage.HostInfoResponse> HostInfoUpdate;

    private RamnetBase _ramnet;
    private INetworkMessagePool<BasicMessage.Ping> _pingMessages; 
    private IList<double> _updatePlayerPacketsReceived; 

    private bool _isConnected;
    private ConnectionId _hostConnectionId;

    private static double _smoothRoundtripTime = 0.1f;
    private static double _roundtripTime = 0.1f;

    private GameObject _player;

    public static double SmoothRoundtripTime {
        get { return _smoothRoundtripTime; }
    }

    public static double RoundtripTime {
        get { return _roundtripTime; }
    }

    private void Awake() {
        //_gameClock.TimeScale = 0f;

        _updatePlayerPacketsReceived = new CircularBuffer<double>(500);
        var dependencyContainer = new DependencyContainer(new Dictionary<string, object> {
            {"clock", _synchedGameClock},
            {"fixedClock", _syncedFixedClock},
            {"updatePlayerPacketsCounter", _updatePlayerPacketsReceived}
        });

        var prefabs = new Dictionary<ObjectType, GameObject> {
            {ObjectTypes.Player, _playerPrefab},
        };

        _ramnet = new RamnetBase(_coroutineScheduler, _transporter, _natFacilitatorEndpoint.ToIpEndPoint(), _messageTransporter, prefabs, dependencyContainer);
        _pingMessages = _ramnet.MessagePool.GetPool<BasicMessage.Ping>();
    }

    private void OnDestroy() {
        Disconnect();
    }

    private void Update() {
        if (_isConnected) {
            _smoothRoundtripTime = Mathd.Lerp(_smoothRoundtripTime, _roundtripTime, _synchedGameClock.DeltaTime * 10.0);
        }
    }

    public void Connect(IPEndPoint remoteEndPoint, OnConnectionEstablished onEstablished, 
        OnConnectionFailure onFailure, OnDisconnected onDisconnected) {

        _ramnet.MessageRouter.ClearHandlers();
        _ramnet.MessageRouter
            .RegisterHandler<BasicMessage.Pong>(OnServerClockSync)
            .RegisterHandler<BasicMessage.ReplicatePreExistingObject>((connectionId, endpoint, message) => {
                Debug.Log("replicating existing instance " + message.GlobalObjectId);
                var instance = _ramnet.PreExistingObjects[message.GlobalObjectId];
                Debug.Log("instance found: " + instance);
                _ramnet.ReplicatedObjectStore.ReplicateExistingInstance(message.ObjectRole, connectionId, instance,
                    message.NetworkObjectId, message.GlobalObjectId);
            })
            .RegisterHandler<BasicMessage.CreateObject>((connectionId, endpoint, message, reader) => {
                var replicatedObject = _ramnet.ReplicatedObjectStore.AddReplicatedInstance(
                    message.ObjectType, 
                    message.ObjectRole,
                    message.ObjectId, 
                    connectionId);
                Debug.Log("Creating object with id " + message.ObjectId, replicatedObject.GameObject);
                _ramnet.ReplicatedObjectStore.DispatchMessages(connectionId, message.ObjectId, 
                    message.AdditionalData, message.AdditionalData.LengthBytes);
                replicatedObject.Activate();
                if (message.ObjectType == ObjectTypes.Player && message.ObjectRole.IsOwner()) {
                    _camera.Target = replicatedObject.GameObject.GetComponent<PlayerSimulation>();
                }    
            })
            .RegisterHandler<BasicMessage.ToObject>((connectionId, endpoint, message, reader) => {
                _ramnet.ReplicatedObjectStore.DispatchMessage(connectionId, message.ReceiverId, 
                    reader);
            })
            .RegisterHandler<BasicMessage.DeleteObject>((connectionId, endpoint, message, reader) => {
                _ramnet.ReplicatedObjectStore.RemoveReplicatedInstance(connectionId, message.ObjectId);
            });
        _ramnet.GroupRouter.ClearConnectionHandlers();

        if (_transporter.Status == TransporterStatus.Closed) {
            var transportConfig = new NetPeerConfiguration("RigidbodyParty");
            transportConfig.MaximumConnections = 64;
            _transporter.Open(transportConfig);
        }

        Debug.Log("Connecting to " + remoteEndPoint);
        _initialSyncDone = false;
        _ramnet.ConnectionManager.Connect(remoteEndPoint,
            (connId, endpoint) => {
                OnConnectionEstablished(connId, endpoint);
                onEstablished(connId, endpoint);
            },
            (endpoint, exception) => {
                OnConnectionFailure(endpoint);
                onFailure(endpoint, exception);
            },
            connId => {
                OnDisconnected(connId);
                onDisconnected(connId);
            });
        _camera.gameObject.SetActive(true);
    }

    public void RequestHostInfo(IPEndPoint endPoint) {
        var request = _ramnet.MessagePool.GetMessage<BasicMessage.RequestHostInfo>();
        _ramnet.ConnectionlessMessageSender.Send(endPoint, request);
    }

    public void Disconnect() {
        if (_hostConnectionId != ConnectionId.NoConnection) {
            _initialSyncDone = false;
            _ramnet.ConnectionManager.Disconnect(_hostConnectionId);
        }
    }

    public void OnConnectionEstablished(ConnectionId newPeerConnectionId, IPEndPoint endpoint) {
        _hostConnectionId = newPeerConnectionId;
        _isConnected = true;

        StartCoroutine(PingRepeatedly(newPeerConnectionId));
    }

    public void OnConnectionFailure(IPEndPoint endpoint) {
        _hostConnectionId = ConnectionId.NoConnection;
        _isConnected = false;
        Debug.LogError("Failed to connect to " + endpoint);
    }

    void OnDisconnected(ConnectionId connectionId) {
        Debug.Log("Disconnected from server");
        _isConnected = false;

        // Remove all replicated objects created by the host
        _ramnet.ReplicatedObjectStore.RemoveAll(connectionId);

        _hostConnectionId = ConnectionId.NoConnection;
    }

    private IEnumerator PingRepeatedly(ConnectionId hostId) {
        while (_isConnected) {
            RequestClockSync(hostId);
            yield return new WaitForSeconds(3f);
        }
    }

    private void RequestClockSync(ConnectionId hostId) {
        var pingMessage = _pingMessages.Create();
        pingMessage.Content.Timestamp = _synchedGameClock.CurrentTime;
        pingMessage.Content.FixedTimestamp = _syncedFixedClock.CurrentTime; // Todo: don't use this for ping
        _ramnet.MessageSender.Send(hostId, pingMessage);

        //Debug.Log("Clock sync frame: was sent on render frame: " + Time.frameCount);
    }

    private bool _initialSyncDone;
    private void OnServerClockSync(BasicMessage.Pong pong) {
        _roundtripTime = _synchedGameClock.CurrentTime - pong.SenderTimestamp;
    }

    private struct PingSession {
        public ushort Id;
        public float StartTime;
    }

    public IList<double> UpdatePlayerPacketsReceived {
        get { return _updatePlayerPacketsReceived; }
    }
}
