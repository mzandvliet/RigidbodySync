using System;
using System.Collections.Generic;
using System.Net;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Unity.Utility;
using RamjetAnvil.Volo.MasterServerClient;
using UnityEngine;

public class Game : MonoBehaviour {

    [SerializeField] private UnityMasterServerClient _masterServerClient;
    [SerializeField] private AbstractUnityClock _synchedGameClock;
    [SerializeField] private AbstractUnityClock _syncedFixedClock;

    [SerializeField] private RamjetClient _client;
    [SerializeField] private RamjetServer _server;

    private bool _isHostListRefreshing;
    private IList<HostInfo> _availableHosts;
    private GameState _state = GameState.Disconnected;
    private AuthToken _authToken;
    private string _hostName = "SomeHost";
    private string _port = "5623"; // 0 for auto
    private Vector2 _hostListScrollPosition;

    void Awake() {
        Application.runInBackground = true;

        _availableHosts = new List<HostInfo>();
        _authToken = new AuthToken("dummy", "dummy");
        _server.AuthToken = _authToken;
        _server.OnStarted += OnServerStarted;
        _server.OnStopped += OnServerStopped;
    }

    private GUIStyle _graphStyle = new GUIStyle();
    private void OnGUI() {
        GUILayout.BeginVertical();
        {
            GUILayout.Label("Status: " + _state);
            switch (_state) {
                case GameState.Disconnected:
//                    _endpoint = GUILayout.TextField(_endpoint);
//                    if (GUILayout.Button("Connect")) {
//                        StartClient();
//                    }

                    GUI.color = Color.black;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Host", GUILayout.Width(200));
                    GUILayout.Label("Endpoint", GUILayout.Width(150));
                    GUILayout.Label("Distance", GUILayout.Width(100));
                    GUILayout.Label("Country", GUILayout.Width(100));
                    GUILayout.Label("Version", GUILayout.Width(50));
                    GUILayout.EndHorizontal();

                    _hostListScrollPosition = GUILayout.BeginScrollView(
                        _hostListScrollPosition,
                        GUILayout.MaxHeight(400));
                    if (_availableHosts != null) {
                        for (int i = 0; i < _availableHosts.Count || i >= 40; i++) {
                            var hostInfo = _availableHosts[i];
                            GUI.color = Color.black;
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(hostInfo.Host.Name, GUILayout.Width(200));
                            GUILayout.Label(hostInfo.Host.PeerInfo.ExternalEndpoint.ToString(), GUILayout.Width(150));
                            GUILayout.Label(Mathf.RoundToInt((float) hostInfo.Host.DistanceInKm) + " km", GUILayout.Width(100));
                            GUILayout.Label(hostInfo.Host.Country, GUILayout.Width(100));
                            GUILayout.Label("v" + hostInfo.Host.Version, GUILayout.Width(50));
                            GUI.color = Color.white;
                            if (GUILayout.Button("Connect")) {
                                StartClient(hostInfo.Host.PeerInfo.ExternalEndpoint);
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.EndScrollView();

                    GUI.color = Color.white;
                    GUI.enabled = !_isHostListRefreshing;
                    if (GUILayout.Button("Refresh host list")) {
                        _isHostListRefreshing = true;
                        _masterServerClient.Client.ListHosts(_authToken, (statusCode, hosts) => {
                            if (statusCode == HttpStatusCode.OK) {
                                _availableHosts.Clear();
                                for (int i = 0; i < hosts.Count; i++) {
                                    var host = hosts[i];
                                    _availableHosts.Add(new HostInfo(host));
                                }
                            }
                            _isHostListRefreshing = false;
                        });
                    }
                    GUI.enabled = true;

                    GUILayout.BeginHorizontal();
                    _hostName = GUILayout.TextField(_hostName, maxLength: 100);
                    GUILayout.Label("Port: ");
                    _port = GUILayout.TextField(_port, maxLength: 20);
                    if (GUILayout.Button("Host Dedicated Server")) {
                        var port = Convert.ToInt32(_port);
                        StartDedicatedServer(_hostName, port);
                    }
                    GUILayout.EndHorizontal();
//                    if (GUILayout.Button("Host Listening Server")) {
//                        StartListeningServer();
//                    }
                    break;
                case GameState.Connecting:
                case GameState.StartingDedicatedServer:
                case GameState.StartingListeningServer:
                    // Todo: cancel
                    break;
                case GameState.Client:
                    GUILayout.Label("Latency: " + Mathd.ToMillis(RamjetClient.RoundtripTime * 0.5));
                    if (GUILayout.Button("Disconnect")) {
                        StopClient();
                    }
                    GUILayout.Label("Game Clock: " + _synchedGameClock.CurrentTime);
                    GUILayout.Label("Fixed Clock: " + _syncedFixedClock.CurrentTime);

                    
                    var packetsReceived = _client.UpdatePlayerPacketsReceived;
                    if (packetsReceived != null) {
                        for (int i = packetsReceived.Count - 1; i >= 0; i--) {
                            var packetTimestamp = packetsReceived[i];
                            if (packetTimestamp < _synchedGameClock.CurrentTime - 10f) {
                                break;
                            } 

                            var graphSize = new Vector2(100, 50);
                            var graphPosition = new Vector2(Screen.width - graphSize.x, Screen.height - graphSize.y);
                            GUI.color = Color.black;
                            _graphStyle.fontSize = 30;
                            var distanceFromOrigin = (int)Math.Round((_synchedGameClock.CurrentTime - packetTimestamp) * 50.0);
                            GUI.Label(
                                new Rect(new Vector2((Screen.width - 50) - distanceFromOrigin, graphPosition.y), graphSize), 
                                "|",
                                _graphStyle);
                        }
                    }

                    break;
                case GameState.DedicatedServer:
                    if (GUILayout.Button("Disconnect")) {
                        StopServer();
                    }
                    GUILayout.Label("Game Clock: " + _synchedGameClock.CurrentTime);
                    GUILayout.Label("Fixed Clock: " + _syncedFixedClock.CurrentTime);
                    GUILayout.Label("Diff: " + Math.Round(_synchedGameClock.CurrentTime - _syncedFixedClock.CurrentTime, 3));

                    break;
                case GameState.ListeningServer:
                    if (GUILayout.Button("Disconnect")) {
                        StopListeningServer();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

//        GUILayout.Label("Time diff: " + (_gameClock.Time - _fixedClock.Time));
//        GUILayout.Label("Time diff Unity: " + (Time.time - Time.fixedTime));
//        GUILayout.Label("Unity vs. .NET clock: " + (Time.time - _dotNetClock.Time));
//        GUILayout.Label("Unity vs. Ramjet Game clock: " + (Time.time - _gameClock.Time));
//        GUILayout.Label("Unity vs. Ramjet Fixed clock: " + (Time.fixedTime - _fixedClock.Time));
//        GUILayout.Label(".NET clock vs. Ramjet clock: " + (_dotNetClock.Time - _gameClock.Time));
        GUILayout.EndVertical();

        GUI.color = Color.black;
        GUI.Label(new Rect(Screen.width - 40, 10, 40, 50), "v" + VersionInfo.VersionNumber);
    }

    private void StartClient(IPEndPoint remoteEndpoint) {
        _state = GameState.Connecting;
        
        _client.Connect(remoteEndpoint, OnClientConnected, OnClientDisconnected, OnClientDisconnected);
    }

    private void StartDedicatedServer(string hostName, int port) {
        _state = GameState.StartingDedicatedServer;
        _server.Host(hostName, port);
    }

    private void StopServer() {
        _server.Stop();
    }

    private void StopClient() {
        _client.Disconnect();
    }

    private void StopListeningServer() {
        _client.Disconnect();
        _server.Stop();
    }

    private void OnClientConnected(ConnectionId connectionId, IPEndPoint endpoint) {
        if (_state == GameState.StartingListeningServer) {
            _state = GameState.ListeningServer;
        }
        else {
            _state = GameState.Client;                   
        }
    }

    private void OnClientDisconnected(ConnectionId connectionId) {
        _state = GameState.Disconnected;
    }

    private void OnClientDisconnected(IPEndPoint endpoint, Exception e) {
        _state = GameState.Disconnected;
    }

    private void OnServerStarted() {
        _state = GameState.DedicatedServer;
    }

    private void OnServerStopped() {
        _state = GameState.Disconnected;
    }

    private enum GameState {
        Disconnected,
        Connecting,
        StartingDedicatedServer,
        StartingListeningServer,
        Client,
        DedicatedServer,
        ListeningServer,
    }

    private struct HostInfo {
        public readonly RemoteHost Host;
        public float? Ping;

        public HostInfo(RemoteHost host) : this() {
            Host = host;
        }
    }
}
