using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;
using UnityEngine;
using Guid = RamjetAnvil.Util.Guid;

namespace RamjetAnvil.RamNet {

    // TODO Get rid of the Replicator and make two different types of ReplicationStores
    // one for the server and one for the client
    /// <summary>
    /// Allows you to create instance of objects that can be used for replication
    /// over the network. Manages unique ids.
    /// </summary>
    public class Replicator {

        private readonly NetBuffer _buffer;
        private readonly ReplicatedObjectStore _store;
        private readonly Stack<ObjectId> _unusedObjectIds;
        private readonly int _growth;
        private uint _currentCapacity;

        public Replicator(
            ReplicatedObjectStore store,
            int growth = 256) {

            var initialCapacity = (int)store.Capacity;
            _buffer = new NetBuffer();
            _currentCapacity = 0;
            _growth = growth;
            _store = store;
            _unusedObjectIds = new Stack<ObjectId>(initialCapacity);
            GenerateAdditionalObjectIds(initialCapacity);
        }

        public ReplicatedObject AddPreExistingInstance(ObjectRole role, 
            ConnectionId hostConnectionId, GameObject instance, Guid globalId) {

            var objectId = RequestObjectId();
            return _store.ReplicateExistingInstance(role, hostConnectionId, instance, objectId, globalId);
        }

        public ReplicatedObject CreateReplicatedInstance(
            ObjectType type, 
            ObjectRole role,
            ConnectionId connectionId) {

            var objectId = RequestObjectId();
            return _store.AddReplicatedInstance(type, role, objectId, connectionId);
        }

        public void RemoveReplicatedInstance(ObjectId objectId) {
            _store.RemoveReplicatedInstance(ConnectionId.NoConnection, objectId);
            _unusedObjectIds.Push(objectId);
        }

        private ObjectId RequestObjectId() {
            if (_unusedObjectIds.Count == 0) {
                GenerateAdditionalObjectIds(_growth);
            }
            var objectId = _unusedObjectIds.Pop();
            return objectId;
        }

        private void GenerateAdditionalObjectIds(int growth) {
            var currentCapacity = _currentCapacity;
            var newCapacity = _currentCapacity + (uint)growth;
            for (var i = newCapacity; i > currentCapacity; i--) {
                _unusedObjectIds.Push(new ObjectId(i));
            }
            _currentCapacity = newCapacity;
            _store.Capacity = newCapacity;
        }

        public ReplicatedObjectStore Store {
            get { return _store; }
        }

        public void Activate(ReplicatedObject replicatedObject) {
            _buffer.Reset();
            replicatedObject.ReplicationConstructor.SerializeInitialState(_buffer);
            _store.DispatchMessages(ConnectionId.NoConnection, replicatedObject.Id, _buffer, _buffer.LengthBytes);
            replicatedObject.Activate();
        }
    }
}
