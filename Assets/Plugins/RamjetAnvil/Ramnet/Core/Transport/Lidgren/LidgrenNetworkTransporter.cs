using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.RamNet {
    public class LidgrenNetworkTransporter : MonoBehaviour, IConnectionTransporter, IConnectionlessTransporter {
        
        public event OnConnectionEstablished OnConnectionOpened;
        public event OnDisconnected OnConnectionClosed;
        public event OnUnconnectedDataReceived OnUnconnectedDataReceived;
        public event OnDataReceived OnDataReceived;
        public event Action<string, IPEndPoint> OnNatPunchSuccess;

        private NetPeer _netPeer;
        private IPEndPoint _internalEndpoint;
        private TransporterStatus _status = TransporterStatus.Closed;
        private int _lastSampledFrame;

        private Stack<ConnectionId> _connectionIdPool; 
        private IDictionary<NetConnection, ConnectionId> _connections;
        private IDictionary<ConnectionId, NetConnection> _connectionsById;
        private IDictionary<ConnectionId, float> _latencyTable;

        public void Open(NetPeerConfiguration config) {
            config.UseMessageRecycling = true;
            config.NetworkThreadName = "LidgrenNetworkTransporter";

            config.EnableMessageType(NetIncomingMessageType.StatusChanged);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            //config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.Data);
            config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.DebugMessage);
            config.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            config.EnableMessageType(NetIncomingMessageType.WarningMessage);
            config.EnableMessageType(NetIncomingMessageType.ErrorMessage);
            config.EnableMessageType(NetIncomingMessageType.Error);

            _lastSampledFrame = -1;

            _connections = new Dictionary<NetConnection, ConnectionId>(config.MaximumConnections);
            _connectionsById = new ArrayDictionary<ConnectionId, NetConnection>(
                connId => connId.Value,
                i => new ConnectionId(i), 
                config.MaximumConnections);
            _latencyTable = new ArrayDictionary<ConnectionId, float>(
                connId => connId.Value,
                i => new ConnectionId(i), 
                config.MaximumConnections);

            _connectionIdPool = new Stack<ConnectionId>(config.MaximumConnections);
            for (int i = config.MaximumConnections - 1; i >= 0; i--) {
                _connectionIdPool.Push(new ConnectionId(i));
            }

            _netPeer = new NetPeer(config);
            _netPeer.Start();
            
            Debug.Log("internal endpoint is: " + UnityEngine.Network.player.ipAddress);
            _internalEndpoint = new IPEndPoint(IPAddress.Parse(UnityEngine.Network.player.ipAddress), _netPeer.Port);

            _status = TransporterStatus.Open;

            Debug.Log("Opened Lidgren transporter at " + _netPeer.Socket.LocalEndPoint);
        }

        public void Close() {
            if (_status == TransporterStatus.Open) {
                _netPeer.Shutdown("Close");
                var connectionIds = _connections.Values;
                foreach (var connectionId in connectionIds) {
                    if (OnConnectionClosed != null) {
                        OnConnectionClosed(connectionId);
                    }
                }
                _connections.Clear();
                _connectionsById.Clear();

                _status = TransporterStatus.Closed;
            }
        }

        public float GetLatency(ConnectionId connectionId) {
            if (connectionId == ConnectionId.NoConnection) {
                return 0f;
            }
            return _latencyTable[connectionId];
        }

        public IPEndPoint InternalEndpoint {
            get { return _internalEndpoint; }
        }

        public TransporterStatus Status {
            get { return _status; }
        }

        public ConnectionId Connect(IPEndPoint endpoint) {
            var connection = _netPeer.Connect(endpoint);
            var connectionId = _connectionIdPool.Pop();
            AddConnection(connectionId, connection);
            return connectionId;
        }

        public void Disconnect(ConnectionId connectionId) {
            if (connectionId != ConnectionId.NoConnection && _status == TransporterStatus.Open) {
                NetConnection connection;
                if (_connectionsById.TryGetValue(connectionId, out connection)) {
                    connection.Disconnect("Disconnect");
                }
            }
        }

        public void SendUnconnected(IPEndPoint endPoint, NetBuffer buffer) {
            var message = _netPeer.CreateMessage();
            if (buffer.LengthBytes > 0) {
                message.Write(buffer);    
            }
            Console.WriteLine("Sending data " + buffer.ToHexString() + " to " + endPoint);
            _netPeer.SendUnconnectedMessage(message, endPoint);
        }

        public void Send(ConnectionId connectionId, NetDeliveryMethod deliveryMethod, NetBuffer buffer) {
            var connection = _connectionsById[connectionId];
            if (connection != null && connection.Status == NetConnectionStatus.Connected) {
                var message = _netPeer.CreateMessage();
                //Debug.Log("sending data " + buffer.Data.ToHexString(0, buffer.LengthBytes) + " to " + connection.RemoteEndPoint);
                message.Write(buffer);
                _netPeer.SendMessage(message, connection, deliveryMethod);
            }
        }

        public void Flush() {
            _netPeer.FlushSendQueue();
        }

        void FixedUpdate() {
            if (Time.renderedFrameCount > _lastSampledFrame && _status == TransporterStatus.Open) {
                Receive();
                _lastSampledFrame = Time.renderedFrameCount;
            }
        }

        void Receive() {
            NetIncomingMessage msg;
            while ((msg = _netPeer.ReadMessage()) != null) {
                //Debug.Log("incoming message of type " + msg.MessageType + " from " + msg.SenderEndPoint);
                switch (msg.MessageType) {
                    case NetIncomingMessageType.Error:
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        var connectionStatus = (NetConnectionStatus)msg.ReadByte();
                        //Debug.Log("connection status " + connectionStatus);
                        ConnectionId connectionId;
						switch (connectionStatus) {
						    case NetConnectionStatus.None:
						    case NetConnectionStatus.ReceivedInitiation:
						    case NetConnectionStatus.RespondedAwaitingApproval:
						    case NetConnectionStatus.RespondedConnect:
                            case NetConnectionStatus.InitiatedConnect:
                            case NetConnectionStatus.Disconnecting:
                                break;
                            case NetConnectionStatus.Connected:
						        if (!_connections.TryGetValue(msg.SenderConnection, out connectionId)) {
						            connectionId = _connectionIdPool.Pop();
                                    AddConnection(connectionId, msg.SenderConnection);
						        } 
                                
                                Debug.Log("Connection opened to " + msg.SenderConnection.RemoteEndPoint + " with connection id " +
						                      connectionId);

						        if (OnConnectionOpened != null) {
						            OnConnectionOpened(connectionId, msg.SenderEndPoint);
						        }
						        break;
                            case NetConnectionStatus.Disconnected:
						        if (_connections.TryGetValue(msg.SenderConnection, out connectionId)) {
                                    Debug.Log("Disconnected: " + connectionId);
						            RemoveConnection(msg.SenderConnection);
						            if (OnConnectionClosed != null) {
						                OnConnectionClosed(connectionId);
						            }
						        }
						        break;
							default:
                                Debug.LogError("Unhandled connection status: " + connectionStatus);
								break;
						}
                        break;
                    case NetIncomingMessageType.UnconnectedData:
                        Console.WriteLine("Receiving unconnected data from " + msg.SenderEndPoint + ": " + msg.ToHexString());
                        if (OnUnconnectedDataReceived != null) {
                            OnUnconnectedDataReceived(msg.SenderEndPoint, msg);    
                        }
                        break;
//                    case NetIncomingMessageType.ConnectionApproval:
//                        Debug.Log("Approving connection to " + msg.SenderEndPoint);
//                        msg.SenderConnection.Approve();
//                        break;
                    case NetIncomingMessageType.Data:
                        if (OnDataReceived != null) {
                            connectionId = (ConnectionId) msg.SenderConnection.Tag;
                            OnDataReceived(connectionId, msg.SenderEndPoint, msg);
                        }
                        break;
                    case NetIncomingMessageType.NatIntroductionSuccess:
                        var token = msg.ReadString();
                        if (OnNatPunchSuccess != null) {
                            OnNatPunchSuccess(token, msg.SenderEndPoint);
                        }
                        break;
                    case NetIncomingMessageType.ConnectionLatencyUpdated:
                        connectionId = (ConnectionId) msg.SenderConnection.Tag;
                        var roundtripTime = msg.ReadFloat();
                        var latency = roundtripTime * 0.5f;
                        _latencyTable[connectionId] = latency;
                        break;
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Console.WriteLine("LIDGREN (trace): " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.DebugMessage:
                        Console.WriteLine("LIDGREN (info): " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Console.WriteLine("LIDGREN (warning): " + msg.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        Console.WriteLine("LIDGREN (error): " + msg.ReadString());
                        break;
                    default:
                        Console.WriteLine("LIDGREN (warning): Unhandled message type: " + msg.MessageType);
                        break;
                }
                _netPeer.Recycle(msg);
            }
        }

        private void AddConnection(ConnectionId id, NetConnection connection) {
            connection.Tag = id;
            _connections.Add(connection, id);
            _connectionsById.Add(id, connection);
            _latencyTable[id] = 0f;
        }

        private void RemoveConnection(NetConnection connection) {
            var connectionId = _connections[connection];
            _connectionIdPool.Push(connectionId);
            _connections.Remove(connection);
            _connectionsById.Remove(connectionId);
            _latencyTable[connectionId] = 0f;
        }

        void OnDestroy() {
            Close();
        }
    }
}
