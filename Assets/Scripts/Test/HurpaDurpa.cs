//using System;
//using UnityEngine;
//using System.Collections;
//using System.Net;
//using System.Net.Sockets;
//using System.Threading;
//using RamjetAnvil.RamNet;
//using RamjetAnvil.Util;
//using UnityEngine.Networking;
//
//public class HurpaDurpa : MonoBehaviour {
//    private UnityNetworkTransporter _transport;
//
//    [SerializeField] private bool _hurp;
//    [SerializeField] private Ipv4Endpoint _targetEndpoint;
//
//    private ConnectionId _connId;
//    private bool _isConnected;
//
//    private Socket _socket;
//
//    private volatile bool _isRunning;
//
//    // Use this for initialization
//    void Start() {
//        _isRunning = true;
//        _transport = gameObject.AddComponent<UnityNetworkTransporter>();
//
//        var transportConfig = TransportCommon.GetTransportConfig();
//
//        _transport.OnConnectionOpened += (id, point) => { _isConnected = true; };
//        _transport.OnConnectionClosed += id => { Debug.Log("Hurp failed"); };
//
//        if (!_hurp) {
//            //_transport.Open(new HostTopology(transportConfig, maxDefaultConnections: 64), new Ipv4Endpoint("0.0.0.0", _targetEndpoint.Port));
//
//            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
//            _socket.Bind(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port: _targetEndpoint.Port));
//
//            new Thread(() => {
//                var buffer = new byte[256];
//                while (_isRunning) {
//                    _socket.Receive(buffer);
//                    Debug.Log("received data");
//                }
//            }).Start();
//        }
//        else {
//            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
//            _socket.Bind(new IPEndPoint(IPAddress.Parse("0.0.0.0"), port: 0));
//            //socket.Connect(IPAddress.Parse(_targetEndpoint.Address), _targetEndpoint.Port);
//            //_transport.Open(new HostTopology(transportConfig, maxDefaultConnections: 64), new Ipv4Endpoint("0.0.0.0", port: 0));
//        }
//
//        if (_hurp) {
//            //_connId = _transport.Connect(_targetEndpoint);
//        }
//    }
//
//    // Update is called once per frame
//    void Update() {
//        if (_hurp) {
//            //_transport.Send(_connId, QosType.Unreliable, new byte[] { 123 }, 1);
//            //_socket.Send(new byte[] {123});
//            _socket.SendTo(new byte[] {123},
//                new IPEndPoint(IPAddress.Parse(_targetEndpoint.Address), _targetEndpoint.Port));
//        }
//    }
//
//    void OnDestroy() {
//        _isRunning = false;
//        _socket.Close();
//    }
//}
