using System;
using System.Collections.Generic;
using Lidgren.Network;
using RamjetAnvil.Coroutine;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Util;
using RamjetAnvil.Volo.MasterServerClient;
using UnityEngine;
using System.Net;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using Coroutine = RamjetAnvil.Coroutine.Coroutine;

/* Todo:
 * A player hosting a listening server (or singleplayer) needs to use a local client instead of a regular
 * remote client. myClient = ClientScene.ConnectLocalServer();
 * 
 * We don't need synced clocks for prediction, we need very accurate latency info.
 * 
 * Todo: use clocks that work using doubles?
 * 
 * Todo: use wireshark to figure out what's going on with packet batching & size
 * 
 */

public class RamjetServer : MonoBehaviour, ITransportConnectionHandler {
    [SerializeField] private Ipv4Endpoint _natFacilitatorEndpoint = new Ipv4Endpoint("95.85.31.166", 15493);
    [SerializeField]private UnityMasterServerClient _masterServerClient;
    [SerializeField] private PlayerCamera _playerCamera;
    [SerializeField] private AbstractUnityControllableClock _syncedGameClock;
    [SerializeField] private AbstractUnityControllableClock _syncedFixedClock;

    [SerializeField] private UnityCoroutineScheduler _coroutineScheduler;
    [SerializeField] private LidgrenNetworkTransporter _transporter;
    [SerializeField] private QueueingMessageSender _messageTransporter;

    [SerializeField] private GameObject _playerPrefab;

    private bool _isStarted;
    private AuthToken _authToken;
    private IDisposable _facilitatorRegistration;
    private RamnetBase _ramnet;
    private INetworkMessagePool<BasicMessage.Pong> _pongMessages;
    private Replicator _replicator;
    
    private IDisposable _masterServerRegistration;
    
    public event Action OnStarted;
    public event Action OnStopped;

    private void Awake() {
        var dependencyContainer = new DependencyContainer(new Dictionary<string, object> {
            {"clock", _syncedGameClock},
            {"fixedClock", _syncedFixedClock},
        });

        var prefabs = new Dictionary<ObjectType, GameObject> {
            {ObjectTypes.Player, _playerPrefab},
        };
        
        _ramnet = new RamnetBase(_coroutineScheduler, _transporter, _natFacilitatorEndpoint.ToIpEndPoint(), _messageTransporter, prefabs, dependencyContainer);
        _pongMessages = _ramnet.MessagePool.GetPool<BasicMessage.Pong>();
        _replicator = new Replicator(_ramnet.ReplicatedObjectStore);
    }

    private void OnDestroy() {
        if (_isStarted) {
            Stop();    
        }
    }

	public void Host(string hostName, int port = 0) {
        _playerCamera.gameObject.SetActive(true);

        _ramnet.MessageRouter.ClearHandlers();
        _ramnet.MessageRouter
            .RegisterHandler<BasicMessage.Ping>(OnClientClockSyncRequest)
            .RegisterHandler<BasicMessage.ToObject>((connectionId, endpoint, message, reader) => {
                _ramnet.ReplicatedObjectStore.DispatchMessage(connectionId, message.ReceiverId, 
                    reader);
            });

        _ramnet.GroupRouter.ClearConnectionHandlers();
        _ramnet.GroupRouter.SetConnectionHandler(NetworkGroup.Default, this);

	    var hostConfig = new NetPeerConfiguration("RigidbodyParty");
	    hostConfig.Port = port;
	    hostConfig.MaximumConnections = 64;
	    hostConfig.AcceptIncomingConnections = true;
        _transporter.Open(hostConfig);

	    foreach (var preExistingObject in _ramnet.PreExistingObjects) {
            var gameObject = preExistingObject.Value;
            Debug.Log("replicating existing instance "+ gameObject + " under id " + preExistingObject.Key);
            _replicator.AddPreExistingInstance(ObjectRole.Authority, ConnectionId.NoConnection, gameObject, preExistingObject.Key);
	    }

        _facilitatorRegistration = _coroutineScheduler.Run(_ramnet.NatFacilitatorConnection.Register());
	    _masterServerRegistration = _coroutineScheduler.Run(RegisterAtMasterServer(hostName));

	    _isStarted = true;
        if (OnStarted != null) {
            OnStarted();
        }

    }

    public void Stop() {
        // TODO Kill transporter
        _facilitatorRegistration.Dispose();
        _masterServerRegistration.Dispose();
        var externalEndpoint = _ramnet.NatFacilitatorConnection.ExternalEndpoint;
        if (externalEndpoint.IsResultAvailable) {
            _masterServerClient.Client.UnregisterHost(_authToken, externalEndpoint.Result, statusCode => {
                Debug.Log("Unregistered at master server with code " + statusCode);
            });    
        }
        _ramnet.NatFacilitatorConnection.Unregister();
        _transporter.Close();
        _isStarted = false;
        if (OnStopped != null) {
            OnStopped();
        }
    }

    public IPEndPoint LocalEndpoint {
        get { return _transporter.InternalEndpoint; }
    }

    private IEnumerator<WaitCommand> RegisterAtMasterServer(string hostName) {
        while (!_ramnet.NatFacilitatorConnection.ExternalEndpoint.IsResultAvailable) {
            yield return WaitCommand.WaitForNextFrame;
        }
        var externalEndpoint = _ramnet.NatFacilitatorConnection.ExternalEndpoint.Result;

        var isRegistrationSuccessful = false;
        while (true) {
            while (!isRegistrationSuccessful) {
                var registrationConfirmation = Coroutine.FromCallback<HttpStatusCode>(callback => {
                    Debug.Log("External endpoint is: " + externalEndpoint);
                    var request = new HostRegistrationRequest(hostName,
                        new PeerInfo(externalEndpoint, _transporter.InternalEndpoint),
                        shouldAdvertise: true,
                        version: VersionInfo.VersionNumber);
                    _masterServerClient.Client.RegisterHost(_authToken, request, callback);
                });
                while (!registrationConfirmation.IsResultAvailable) {
                    yield return WaitCommand.WaitForNextFrame;
                }

                if (registrationConfirmation.Result != HttpStatusCode.OK) {
                    Debug.LogWarning("Failed to register at the master server due to: " + registrationConfirmation.Result);
                } else {
                    isRegistrationSuccessful = true;
                    Debug.Log("Successfully registered at master server");
                }

                yield return WaitCommand.WaitSeconds(3f);
            }

            while (isRegistrationSuccessful) {
                yield return WaitCommand.WaitSeconds(30f);

                var asyncResult = Coroutine.FromCallback<HttpStatusCode>(callback => {
                    _masterServerClient.Client.Ping(_authToken, externalEndpoint, callback);
                });
                while (!asyncResult.IsResultAvailable) {
                    yield return WaitCommand.WaitForNextFrame;
                }
                isRegistrationSuccessful = asyncResult.Result == HttpStatusCode.OK;
            }
        }
    }

//    private void OnHostInfoRequest(IPEndPoint endPoint, BasicMessage.RequestHostInfo request) {
//        var response = _ramnet.MessagePool.GetMessage<BasicMessage.HostInfoResponse>();
//        response.Content.SenderTimestamp = request.Timestamp;
//        response.Content.PlayerCount = 0;
//        _ramnet.ConnectionlessMessageSender.Send(endPoint, response);
//    }

    private void OnClientClockSyncRequest(ConnectionId connectionId, BasicMessage.Ping ping) {
        var pong = _pongMessages.Create();
        pong.Content.SenderTimestamp = ping.Timestamp;
        pong.Content.SenderFixedTimestamp = ping.FixedTimestamp;
        pong.Content.Timestamp = _syncedGameClock.CurrentTime;
        pong.Content.FixedTimestamp = _syncedFixedClock.CurrentTime;
        _ramnet.MessageSender.Send(connectionId, pong);
    }

    public void OnConnectionEstablished(ConnectionId newPeerConnectionId, IPEndPoint endpoint) {
        // Replicate existing objects to new player
        _ramnet.MessageSender.ReplicateEverything(_ramnet.MessagePool, newPeerConnectionId, _ramnet.ReplicatedObjectStore);

        /* First, create instance and spawn */
        var instance = _replicator.CreateReplicatedInstance(
            ObjectTypes.Player, 
            ObjectRole.Authority,
            newPeerConnectionId);
        instance.GameObject.transform.position = Vector3.up * 100f;
        instance.GameObject.transform.rotation = Quaternion.identity;
        instance.GameObject.GetComponent<PlayerInitialState>().Color = UnityEngine.Random.ColorHSV(0f, 1f, 0.5f, 0.5f, 0.75f, 0.75f);
        _replicator.Activate(instance);

        Debug.Log("adding replicated instance");

        _playerCamera.Target = instance.GameObject.GetComponent<PlayerSimulation>();

        // Replicate new object to everyone
        var activeConnections = _ramnet.GroupRouter.GetActiveConnections(NetworkGroup.Default);
        _ramnet.MessageSender.Replicate(_ramnet.MessagePool, activeConnections, instance);
    }

    public void OnConnectionFailure(ConnectionId connectionId, PeerEndpoint endpoint) {
        OnDisconnected(connectionId);
    }

    public void OnDisconnected(ConnectionId connectionId) {
        var activeConnections = _ramnet.GroupRouter.GetActiveConnections(NetworkGroup.Default);
        _ramnet.MessageSender.RemovePlayer(_ramnet.MessagePool, connectionId, activeConnections, _replicator);
    }

    public AuthToken AuthToken {
        set { _authToken = value; }
    }
}
