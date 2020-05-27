using System;
using System.Linq;
using System.Net;
using Lidgren.Network;
using RamjetAnvil.RamNet;
using System.Collections.Generic;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.RamNet {

    public struct TransportGroupId {
        public readonly int Value;

        public TransportGroupId(int value) {
            Value = value;
        }

        public bool Equals(TransportGroupId other) {
            return Value == other.Value;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TransportGroupId && Equals((TransportGroupId) obj);
        }

        public override int GetHashCode() {
            return Value;
        }

        public override string ToString() {
            return string.Format("TransportGroupId({0})", Value);
        }
    }

    public class TransportRouterConfig {

        public readonly IDictionary<TransportGroupId, TransportGroupConfig> Groups;

        public TransportRouterConfig(IDictionary<TransportGroupId, TransportGroupConfig> groups) {
            Groups = groups;
        }

        public TransportRouterConfig(params KeyValuePair<TransportGroupId, TransportGroupConfig>[] groups) {
            Groups = ArrayDictionary<TransportGroupId, TransportGroupConfig>.FromValues(
                id => id.Value,
                id => new TransportGroupId(id), 
                groups);
        }

        public int MaxConnections {
            get {
                return Groups.Values.Aggregate(0, (total, config) => total + config.MaxConnections);    
            }
        }
    }

    public struct TransportGroupConfig {
        public readonly int MaxConnections;

        public TransportGroupConfig(int maxConnections) {
            MaxConnections = maxConnections;
        }
    }

    /* Todo: 
     * Group identification should be twofold: This thing knows which connection is in which group, that's one. Communication also includes
     * a byte id indicating intended group, that's two. This way cheaters can't bypass auth module, and any inadvertent communication mismatches
     * can be caught as well. I.e. when client is trying to talk to game module but hasn't actually been authenticated yet.
     */
     // TODO Group handlers should handle group joined/group left events instead of onConnected/onDisconnected
    public class TransportGroupRouter : IDisposable {
        public static readonly TransportGroupId DefaultGroup = new TransportGroupId(0);

        private readonly IConnectionTransporter _transporter;
        private readonly TransportRouterConfig _config;

        private readonly IList<TransportGroupId> _groups; 
        private readonly IDictionary<TransportGroupId, IList<ConnectionId>> _connections;
        private readonly IDictionary<TransportGroupId, ITransportDataHandler> _dataHandlers;
        private readonly IDictionary<TransportGroupId, ITransportConnectionHandler> _connectionHandlers;
        
        public TransportGroupRouter(IConnectionTransporter transporter, TransportRouterConfig config) {
            _transporter = transporter;
            _config = config;

            _groups = config.Groups.Keys.ToList();
            _connections = new ArrayDictionary<TransportGroupId, IList<ConnectionId>>(
                id => id.Value,
                value => new TransportGroupId(value), 
                config.Groups.Count);
            for (int i = 0; i < _groups.Count; i++) {
                var group = _groups[i];
                var groupConfig = config.Groups[group];
                _connections[group] = new List<ConnectionId>(groupConfig.MaxConnections);
            }
            _dataHandlers =  new ArrayDictionary<TransportGroupId, ITransportDataHandler>(
                id => id.Value,
                value => new TransportGroupId(value), 
                config.Groups.Count);
            _connectionHandlers = new ArrayDictionary<TransportGroupId, ITransportConnectionHandler>(
                id => id.Value,
                value => new TransportGroupId(value), 
                config.Groups.Count);

            transporter.OnConnectionOpened += OnConnectionEstablished;
            transporter.OnConnectionClosed += OnDisconnected;
            transporter.OnDataReceived += OnDataReceived;
        }

        public void SetDataHandler(TransportGroupId group, ITransportDataHandler handler) {
            _dataHandlers[group] = handler;
        }

        public void SetConnectionHandler(TransportGroupId group, ITransportConnectionHandler handler) {
            _connectionHandlers[group] = handler;
        }

        private void OnConnectionEstablished(ConnectionId connectionId, IPEndPoint endPoint) {
            var assignedGroup = GetAssignedGroup(connectionId);
            if (!assignedGroup.HasValue) {
                AssignToGroup(DefaultGroup, connectionId);    
            }
            
            if (_connectionHandlers[DefaultGroup] != null) {
                _connectionHandlers[DefaultGroup].OnConnectionEstablished(connectionId, endPoint);
            }
        }

        private void OnDisconnected(ConnectionId connectionId) {
            var group = GetGroup(connectionId);
            //Debug.Log("removing " + connectionId + " from group " + group);
            _connections[group].Remove(connectionId);
            if (_connectionHandlers[group] != null) {
                _connectionHandlers[group].OnDisconnected(connectionId);
            }
        }

        public void OnDataReceived(ConnectionId connectionId, IPEndPoint endpoint, NetBuffer reader) {
            // Todo: read first byte to ensure data is meant for the group we're about to send it to
            // Todo: if we do the above, would it be good to also write that ID from within this class?

            var group = GetGroup(connectionId);
            if (_dataHandlers[group] != null) {
                //Debug.Log("message for group " + group + " on " + connectionId);
                _dataHandlers[group].OnDataReceived(connectionId, endpoint, reader);
            } else {
                Debug.LogWarning("message for group " + group + " but there is no handler");
            }
        }

        public TransportGroupId GetGroup(ConnectionId connectionId) {
            var group = GetAssignedGroup(connectionId);
            if (group.HasValue) {
                return group.Value;
            } else {
                throw new ArgumentException("Connection with ID " + connectionId + " is not in any group. This shouldn't happen.");    
            }
        }

        public IList<ConnectionId> GetActiveConnections(TransportGroupId groupId) {
            return _connections[groupId];
        }

        public TransportGroupId? GetAssignedGroup(ConnectionId connectionId) {
            for (int i = 0; i < _groups.Count; i++) {
                var group = _groups[i];
                if (_connections[group].Contains(connectionId)) {
                    return group;
                }
            }
            return null;
        }

        public bool AssignToGroup(TransportGroupId newGroup, ConnectionId connectionId) {
            //Debug.Log("assigning " + connectionId + " to " + newGroup);
            for (int i = 0; i < _groups.Count; i++) {
                var group = _groups[i];
                _connections[group].Remove(connectionId);
            }

            var groupConfig = _config.Groups[newGroup];
            var activeConnections = _connections[newGroup];
            if (activeConnections.Count < groupConfig.MaxConnections) {
                activeConnections.Add(connectionId);
                return true;
            }

            Debug.LogWarning("No more room in " + newGroup);
            return false;
        }

        public void Dispose() {
            _transporter.OnConnectionOpened -= OnConnectionEstablished;
            _transporter.OnConnectionClosed -= OnDisconnected;
            _transporter.OnDataReceived -= OnDataReceived;            
        }

        public void ClearConnectionHandlers() {
            _connectionHandlers.Clear();
        }

        public void ClearDataHandlers() {
            _dataHandlers.Clear();
        }
    }


}
