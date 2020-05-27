using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RamjetAnvil.RamNet {
    public class LidgrenDefaultConnectionManager : IConnectionManager {

        private static readonly OnConnectionEstablished EmptyOnConnectionEstablished = (id, point) => { };
        private static readonly OnDisconnected EmptyOnDisconnected = id => { };

        private readonly IConnectionTransporter _transporter;

        private readonly IDictionary<ConnectionId, ConnectionRegistration> _connections; 

        public LidgrenDefaultConnectionManager(IConnectionTransporter transporter) {
            _transporter = transporter;
            _connections = new Dictionary<ConnectionId, ConnectionRegistration>();

            _transporter.OnConnectionOpened += OnConnectionOpened;
            _transporter.OnConnectionClosed += OnConnectionClosed;
        }

        public void Connect(IPEndPoint hostEndpoint, OnConnectionEstablished onConnectionEstablished = null,
            OnConnectionFailure onConnectionFailure = null, OnDisconnected onDisconnected = null) {
            var connectionId = _transporter.Connect(hostEndpoint);
            _connections[connectionId] = new ConnectionRegistration {
                OnConnectionEstablished = onConnectionEstablished ?? EmptyOnConnectionEstablished,
                OnDisconnected = onDisconnected ?? EmptyOnDisconnected
            };
        }

        public void Disconnect(ConnectionId connectionId) {
            _transporter.Disconnect(connectionId);
        }

        public void Dispose() {
            _transporter.OnConnectionOpened -= OnConnectionOpened;
            _transporter.OnConnectionClosed -= OnConnectionClosed;
        }
        
        private void OnConnectionOpened(ConnectionId connectionId, IPEndPoint endPoint) {
            ConnectionRegistration registration;
            if (_connections.TryGetValue(connectionId, out registration)) {
                registration.OnConnectionEstablished(connectionId, endPoint);
            }
        }

        private void OnConnectionClosed(ConnectionId connectionId) {
            ConnectionRegistration registration;
            if (_connections.TryGetValue(connectionId, out registration)) {
                registration.OnDisconnected(connectionId);
                _connections.Remove(connectionId);
            }
        }

        private struct ConnectionRegistration {
            public OnConnectionEstablished OnConnectionEstablished;
            public OnDisconnected OnDisconnected;
        }
    }
}
