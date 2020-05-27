using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.Coroutine;
using UnityEngine;

namespace RamjetAnvil.RamNet {

    public enum NatFacilitatorRequestType : byte {
        RegisterPeer = 0,
        UnregisterPeer = 1,
        RequestIntroduction = 2,
        RequestExternalEndpoint = 3
    }

    public enum NatFacilitatorMessageType : byte {
        HostNotRegistered = 0,
        PeerRegistrationSuccess = 1
    }

    public class LidgrenPunchThroughFacilitator : IConnectionManager {

        private static readonly OnConnectionEstablished EmptyOnConnectionEstablished = (id, point) => { };
        private static readonly OnConnectionFailure EmptyOnConnectionFailure = (endpoint, exception) => { };
        private static readonly OnDisconnected EmptyOnDisconnected = id => { };

        private readonly float _connectionAttemptTimeout;
        private readonly LidgrenNetworkTransporter _transporter;
        private readonly LidgrenNatPunchClient _natPunchClient;

        private readonly IDictionary<NatPunchId, ConnectionRegistration> _punchAttempts;
        private readonly IDictionary<ConnectionId, ConnectionRegistration> _connectionAttempts;
        private readonly IList<ConnectionRegistration> _connectionAttemptRegistrations; 
        private readonly IDictionary<ConnectionId, ConnectionRegistration> _connections;

        private readonly IDisposable _cleanupRoutine;

        public LidgrenPunchThroughFacilitator(LidgrenNetworkTransporter transporter, 
            ICoroutineScheduler coroutineScheduler, LidgrenNatPunchClient natPunchClient, float connectionAttemptTimeout) {

            _punchAttempts = new Dictionary<NatPunchId, ConnectionRegistration>();
            _connectionAttempts = new Dictionary<ConnectionId, ConnectionRegistration>();
            _connectionAttemptRegistrations = new List<ConnectionRegistration>();
            _connections = new Dictionary<ConnectionId, ConnectionRegistration>();

            _transporter = transporter;
            _connectionAttemptTimeout = connectionAttemptTimeout;
            _natPunchClient = natPunchClient;

            _transporter.OnConnectionOpened += OnConnectionOpened;
            _transporter.OnConnectionClosed += OnConnectionClosed;

            _cleanupRoutine = coroutineScheduler.Run(ConnectionTimeoutCleanup());
        }

        public void Connect(IPEndPoint hostEndpoint, 
            OnConnectionEstablished onConnectionEstablished = null, 
            OnConnectionFailure onConnectionFailure = null, 
            OnDisconnected onDisconnected = null) {

            var connectionRegistration = new ConnectionRegistration();
            connectionRegistration.Timestamp = Time.realtimeSinceStartup;
            connectionRegistration.ConnectionId = ConnectionId.NoConnection;
            connectionRegistration.PublicEndpoint = hostEndpoint;
            connectionRegistration.OnConnectionEstablished = onConnectionEstablished ?? EmptyOnConnectionEstablished;
            connectionRegistration.OnConnectionFailure = onConnectionFailure ?? EmptyOnConnectionFailure;
            connectionRegistration.OnDisconnected = onDisconnected ?? EmptyOnDisconnected;

            var punchId = _natPunchClient.Punch(hostEndpoint, OnPunchSuccess, OnPunchFailure);

            _punchAttempts.Add(punchId, connectionRegistration);
        }

        public void Disconnect(ConnectionId connectionId) {
            _transporter.Disconnect(connectionId);
        }

        private void OnPunchSuccess(NatPunchId punchId, IPEndPoint endPoint) {
            ConnectionRegistration registration;
            if (_punchAttempts.TryGetValue(punchId, out registration)) {
                //Debug.Log("NAT introduction succeeded to " + endPoint);

                _punchAttempts.Remove(punchId);

                registration.ConnectionEndpoint = endPoint;
                var connectionId = _transporter.Connect(endPoint);
                AddConnectionAttempt(connectionId, registration);
            }
        }

        private void OnPunchFailure(NatPunchId punchId) {
            ConnectionRegistration registration;
            if (_punchAttempts.TryGetValue(punchId, out registration)) {
                _punchAttempts.Remove(punchId);
                // TODO Add punch exception
                registration.OnConnectionFailure(registration.PublicEndpoint, exception: null);
            }
        }

        private void OnConnectionOpened(ConnectionId connectionId, IPEndPoint endpoint) {
            ConnectionRegistration connectionRegistration;
            if (_connectionAttempts.TryGetValue(connectionId, out connectionRegistration)) {
                connectionRegistration.OnConnectionEstablished(connectionId, endpoint);
                RemoveConnectionAttempt(connectionId);
                _connections.Add(connectionId, connectionRegistration);
            }
        }

        private void OnConnectionClosed(ConnectionId connectionId) {
            ConnectionRegistration connectionRegistration;
            if (_connections.TryGetValue(connectionId, out connectionRegistration)) {
                connectionRegistration.OnDisconnected(connectionId);
                _connections.Remove(connectionId);
            } else if (_connectionAttempts.TryGetValue(connectionId, out connectionRegistration)) {
                // TODO Fill exception
                connectionRegistration.OnConnectionFailure(connectionRegistration.PublicEndpoint, exception: null);
                RemoveConnectionAttempt(connectionId);
            }
        }

        private IEnumerator<WaitCommand> ConnectionTimeoutCleanup() {
            var removableRegistrations = new List<ConnectionId>();
            while (true) {
                removableRegistrations.Clear();
                for (int i = 0; i < _connectionAttemptRegistrations.Count; i++) {
                    var registration = _connectionAttemptRegistrations[i];
                    if (registration.Timestamp + _connectionAttemptTimeout < Time.realtimeSinceStartup) {
                        registration.OnConnectionFailure(registration.PublicEndpoint, exception: null);
                        removableRegistrations.Add(registration.ConnectionId);
                    }
                }
                for (int i = 0; i < removableRegistrations.Count; i++) {
                    var connectionId = removableRegistrations[i];
                    RemoveConnectionAttempt(connectionId);
                }
                yield return WaitCommand.WaitSeconds(_connectionAttemptTimeout);
            }
        } 

        private void AddConnectionAttempt(ConnectionId connectionId, ConnectionRegistration registration) {
            registration.ConnectionId = connectionId;
            _connectionAttempts.Add(connectionId, registration);
            _connectionAttemptRegistrations.Add(registration);
        }

        private void RemoveConnectionAttempt(ConnectionId connectionId) {
            ConnectionRegistration registration;
            if (_connectionAttempts.TryGetValue(connectionId, out registration)) {
                _connectionAttempts.Remove(connectionId);
                _connectionAttemptRegistrations.Remove(registration);
            }
        }

        public void Dispose() {
            _cleanupRoutine.Dispose();
        }

        // TODO Recycle connection registrations
        private class ConnectionRegistration {
            public float Timestamp;
            public ConnectionId ConnectionId;
            public IPEndPoint PublicEndpoint;
            public IPEndPoint ConnectionEndpoint;
            public OnConnectionEstablished OnConnectionEstablished;
            public OnConnectionFailure OnConnectionFailure;
            public OnDisconnected OnDisconnected;
        }
    }
}
