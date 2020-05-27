using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.Unity.Utility;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;
using Guid = RamjetAnvil.Util.Guid;

namespace RamjetAnvil.RamNet {

    // TODO Add support for non-pooled (static) objects

    public class ObjectMessageParser {
        private readonly IDictionary<MessageType, IObjectMessage> _messages;

        public ObjectMessageParser(IDictionary<Type, MessageType> messageTypes) {
            _messages = new ArrayDictionary<MessageType, IObjectMessage>(i => (int) i.Value, i => new MessageType((uint)i), messageTypes.Count);
            foreach (var messageType in messageTypes) {
                _messages.Add(messageType.Value, (IObjectMessage) Activator.CreateInstance(messageType.Key));
            }
        }

        public IObjectMessage Parse(MessageType messageId, NetBuffer reader) {
            var message = _messages[messageId];
            message.Deserialize(reader);
            return message;
        }
    }

    public class ReplicatedObjectStore : IReplicatedObjectDatabase {

        public event Action<ReplicatedObject> ObjectAdded;
        public event Action<ReplicatedObject> ObjectRemoved;

        private readonly ObjectMessageParser _objectMessageParser;
        private readonly Func<GameObject, IReplicatedObjectDatabase, ReplicatedObject> _replicationDecorator; 
        private readonly IDictionary<ObjectType, IObjectPool<ReplicatedObject>> _objectPools;
        private readonly IList<ObjectId> _objectIds; 
        private IPooledObject<ReplicatedObject>[] _instances;

        public ReplicatedObjectStore(
            ObjectMessageParser objectMessageParser,
            IDictionary<ObjectType, Func<GameObject>> objectFactories,
            Func<GameObject, IReplicatedObjectDatabase, ReplicatedObject> replicationDecorator, 
            int initialInstanceCapacity = 256) {

            _objectMessageParser = objectMessageParser;
            // TODO Rewrite to use fast ArrayDictionary
            _objectPools = new Dictionary<ObjectType, IObjectPool<ReplicatedObject>>();
            foreach (var objectFactory in objectFactories) {
                _objectPools[objectFactory.Key] = CreateReplicatedPool(replicationDecorator, this, objectFactory.Value);
            }
            _replicationDecorator = replicationDecorator;
            _objectIds = new List<ObjectId>(initialInstanceCapacity);
            _instances = new IPooledObject<ReplicatedObject>[initialInstanceCapacity];
        }


        public ReplicatedObject FindObject(ObjectType type, ObjectRole role = ObjectRoles.Everyone) {
            for (int i = 0; i < _objectIds.Count; i++) {
                var objectId = _objectIds[i];
                var instance = _instances[objectId.Value].Instance;
                if (instance.Type == type && (instance.Role & role) != 0) {
                    return instance;
                }
            }
            return null;
        }

        public void FindObjects(ObjectType type, IList<ReplicatedObject> results, ObjectRole role = ObjectRoles.Everyone) {
            for (int i = 0; i < _objectIds.Count; i++) {
                var objectId = _objectIds[i];
                var instance = _instances[objectId.Value].Instance;
                if (instance.Type == type && (instance.Role & role) != 0) {
                    results.Add(instance);
                }
            }
        }

        public ReplicatedObject Find(ObjectId id) {
            return _instances[id.Value].Instance;
        }

        public ReplicatedObject AddReplicatedInstance(ObjectType type, ObjectRole role, ObjectId objectId, ConnectionId connectionId) {
            var objectPool = _objectPools[type];
            var replicatedObject = objectPool.Take();
            AddReplicatedInstance(replicatedObject, type, role, objectId, connectionId);
            return replicatedObject.Instance;
        }

        public ReplicatedObject ReplicateExistingInstance(ObjectRole role, ConnectionId hostConnectionId, 
            GameObject instance, ObjectId objectId, Guid globalId) {

            var replicatedObject = _replicationDecorator(instance, this);
            AddReplicatedInstance(new UnmanagedObject<ReplicatedObject>(replicatedObject), null, role, objectId, hostConnectionId);
            replicatedObject.IsPreExisting = true;
            replicatedObject.GlobalObjectId.CopyFrom(globalId);
            return replicatedObject;
        }

        private void AddReplicatedInstance(IPooledObject<ReplicatedObject> pooledObject, ObjectType? type, ObjectRole role,
            ObjectId objectId, ConnectionId connectionId) {

            var @object = pooledObject.Instance;
            @object.GameObjectNetworkInfo.ObjectType = type.HasValue ? type.Value : new ObjectType(0);
            @object.GameObjectNetworkInfo.ObjectId = objectId;
            @object.GameObjectNetworkInfo.Role = role;
            @object.Type = type;
            @object.Role = role;
            @object.Id = objectId;
            @object.IsPreExisting = false;
            @object.GlobalObjectId.CopyFrom(Guid.Empty);
            @object.OwnerConnectionId = role.IsOwner() ? ConnectionId.NoConnection : connectionId;
            @object.AuthorityConnectionId = role.IsAuthority() ? ConnectionId.NoConnection : connectionId;
            role.ApplyRoleTo(@object.GameObject);
            if (_instances[@object.Id.Value] == null) {
                _instances[@object.Id.Value] = pooledObject;
                _objectIds.Add(@object.Id);

                if (ObjectAdded != null) {
                    ObjectAdded(pooledObject.Instance);
                }
            } else {
                throw new Exception("Cannot replicate instance of " + type + " with " + objectId + " cause the object id is already assigned");
            }
        }

        public void RemoveReplicatedInstance(ConnectionId connectionId, ObjectId id) {
            var pooledInstance = _instances[id.Value];
            if (pooledInstance.Instance.AuthorityConnectionId == connectionId) {
                if (ObjectRemoved != null) {
                    ObjectRemoved(pooledInstance.Instance);
                }

                pooledInstance.Dispose();
                _instances[id.Value] = null;
                _objectIds.Remove(id);
            } else {
                Debug.LogError("" + connectionId + " is not allowed to delete " + pooledInstance.Instance.Id 
                    + " with name " + pooledInstance.Instance.GameObject + " because he is not the authority");
            }
        }

        public IList<ObjectId> ObjectIds {
            get { return _objectIds; }
        }

        public void DispatchMessages(ConnectionId connectionId, ObjectId receiverObjectId, NetBuffer reader, int totalBytes) {
            var bytesRead = 0;
            while (bytesRead < totalBytes) {
                var startPosition = reader.ReaderPosition();
                DispatchMessage(connectionId, receiverObjectId, reader);
                bytesRead += reader.ReaderPosition() - startPosition;
            }
        }

        public void DispatchMessage(ConnectionId connectionId, ObjectId receiverObjectId, NetBuffer reader) {
            var messageType = reader.ReadMessageType();
            var objectMessage = _objectMessageParser.Parse(messageType, reader);
            DispatchMessage(connectionId, messageType, receiverObjectId, objectMessage);
        }

        public void DispatchMessage(ConnectionId connectionId, MessageType objectMessageType, ObjectId receiverObjectId, IObjectMessage objectMessage) {
            var pooledInstance = _instances[receiverObjectId.Value];
            if (pooledInstance == null) {
                //Debug.LogWarning("object with ID " + receiverObjectId + " does not exist");
            }
            else {
                var instance = pooledInstance.Instance;

                var senderType = ObjectRole.Nobody;
                var isOwner = connectionId == instance.OwnerConnectionId;
                var isAuthority = connectionId == instance.AuthorityConnectionId;
                senderType = senderType | (isOwner ? ObjectRole.Owner : ObjectRole.Nobody);
                senderType = senderType | (isAuthority ? ObjectRole.Authority : ObjectRole.Nobody);
                senderType = senderType | (!isOwner && !isAuthority ? ObjectRole.Others : ObjectRole.Nobody);

                instance.GameObjectNetworkInfo.LastReceivedMessageTimestamp = Time.realtimeSinceStartup;
                instance.MessageHandler.Dispatch(objectMessageType, objectMessage, connectionId, senderType);
            }
        }

        public uint Capacity {
            get { return (uint) _instances.Length; }
            set {
                if (_instances.Length != value) {
                    _instances = new IPooledObject<ReplicatedObject>[value];
                }
            }
        }

        public static Func<GameObject, IReplicatedObjectDatabase, ReplicatedObject> GameObjectReplicationDecorator(
            Func<ObjectMessageRouter> objectMessageDispatcherFactory,
            Func<ReplicatedObject, ObjectMessageSender> messageSenderFactory,
            IList<object> dependencies,
            IDictionary<Type, MessageType> objectMessageTypes) {

            var globalDependencies = new DependencyContainer(dependencies.ToArray());

            var objectMessageCache = new ArrayDictionary<MessageType, IObjectMessage>(
                messageType => (int) messageType.Value,
                i => new MessageType((uint) i),
                objectMessageTypes.Count);
            foreach (var kvPair in objectMessageTypes) {
                var type = kvPair.Key;
                var serializableType = kvPair.Value;
                objectMessageCache[serializableType] = (IObjectMessage) Activator.CreateInstance(type);
            }

            return (gameObject, replicationDatabase) => {
                var messageHandler = objectMessageDispatcherFactory();
                messageHandler.RegisterGameObject(gameObject);

                var constructors = InitialStateLogic.FindInitialStateConstructors(gameObject, objectMessageTypes);
                
                var replicatedObject = new ReplicatedObject(gameObject, messageHandler, 
                    new ReplicationConstructor(constructors, objectMessageCache));

                // Inject message sender
                var messageSender = messageSenderFactory(replicatedObject);
                DependencyInjection.DependencyInjection.Inject(gameObject, globalDependencies, overrideExisting: true);
                DependencyInjection.DependencyInjection.InjectSingle(gameObject, messageSender, overrideExisting: true);
                DependencyInjection.DependencyInjection.InjectSingle(gameObject, replicationDatabase, overrideExisting: true);
                DependencyInjection.DependencyInjection.InjectSingle(gameObject, replicatedObject.GameObjectNetworkInfo, overrideExisting: true);

                return replicatedObject;
            };
        }

        public static IObjectPool<ReplicatedObject> CreateReplicatedPool(
            Func<GameObject, IReplicatedObjectDatabase, ReplicatedObject> gameObjectReplicationDecorator,
            IReplicatedObjectDatabase objectDatabase,
            Func<GameObject> gameObjectFactory) {

            var gameObjectPool = GameObject.Find("ObjectPool") ?? new GameObject("ObjectPool");
            var replicatedObjectPool = GameObject.Find("ReplicatedObjects") ?? new GameObject("ReplicatedObjects");

            return new ObjectPool<ReplicatedObject>(() => {
                var gameObject = gameObjectFactory();
                var resetables = gameObject.GetComponentsOfInterfaceInChildren<IResetable>();

                return new ManagedObject<ReplicatedObject>(
                    instance: gameObjectReplicationDecorator(gameObject, objectDatabase),
                    onReturnedToPool: () => {
                        if (!gameObject.IsDestroyed()) {
                            gameObject.SetActive(false);
                            gameObject.transform.parent = gameObjectPool.transform;
                            for (int i = 0; i < resetables.Count; i++) {
                                resetables[i].Reset();
                            }   
                        }
                    },
                    onTakenFromPool: () => {
                        gameObject.transform.parent = replicatedObjectPool.transform;
                    });
            });
        }

        public class ObjectMessageSender : IObjectMessageSender {

            private readonly ReplicatedObject _object;
            private readonly IBasicObjectPool<MulticastNetworkMessage> _networkMessages;
            private readonly IMessageSender _sender;

            private readonly TransportGroupRouter _groupRouter;
            private readonly TransportGroupId _group;

            public static Func<ReplicatedObject, ObjectMessageSender> CreateFactory(
                IMessageSender sender,
                TransportGroupRouter groupRouter,
                TransportGroupId group,
                IBasicObjectPool<MulticastNetworkMessage> networkMessages) {

                return @object => new ObjectMessageSender(sender, groupRouter, group, networkMessages, @object);
            }

            public ObjectMessageSender(IMessageSender sender,
                TransportGroupRouter groupRouter,
                TransportGroupId group,
                IBasicObjectPool<MulticastNetworkMessage> networkMessages,
                ReplicatedObject o) {

                _sender = sender;
                _object = o;
                _groupRouter = groupRouter;
                _group = group;
                _networkMessages = networkMessages;
            }

            private readonly IList<ConnectionId> _receiverConnectionIds = new List<ConnectionId>(); 

            public void Send<T>(NetworkMessage<T> message, ObjectRole recipient) where T : IObjectMessage {
                var activeConnections = _groupRouter.GetActiveConnections(_group);

                _receiverConnectionIds.Clear();
                if ((recipient & ObjectRole.Authority) != 0 && (recipient & ObjectRole.Owner) != 0) {
                    if (_object.AuthorityConnectionId == _object.OwnerConnectionId) {
                        _receiverConnectionIds.Add(_object.AuthorityConnectionId);
                    } else {
                        _receiverConnectionIds.Add(_object.AuthorityConnectionId);
                        _receiverConnectionIds.Add(_object.OwnerConnectionId);
                    }
                }
                else if ((recipient & ObjectRole.Authority) != 0) {
                    _receiverConnectionIds.Add(_object.AuthorityConnectionId);
                }
                else if ((recipient & ObjectRole.Owner) != 0) {
                    _receiverConnectionIds.Add(_object.OwnerConnectionId);    
                }

                if ((recipient & ObjectRole.Others) != 0) {
                    for (int i = 0; i < activeConnections.Count; i++) {
                        var connectionId = activeConnections[i];
                        if (connectionId != _object.OwnerConnectionId && connectionId != _object.AuthorityConnectionId) {
                            _receiverConnectionIds.Add(connectionId);    
                        }
                    }
                }

                var networkMessage = CreateMulticastMessage(message, usageCount: _receiverConnectionIds.Count);
                for (int i = 0; i < _receiverConnectionIds.Count; i++) {
                    var connectionId = _receiverConnectionIds[i];
                    var sendToSelf = connectionId == ConnectionId.NoConnection;
                    if (sendToSelf) {
                        _object.MessageHandler.Dispatch(message.MessageType, message.Content, ConnectionId.NoConnection, _object.Role);    
                        networkMessage.Dispose();
                    } else {
                        _sender.Send(connectionId, networkMessage);    
                    }
                }
            }

            private INetworkMessage CreateMulticastMessage(INetworkMessage message, int usageCount) {
                var multicastMessage = _networkMessages.Take();
                multicastMessage.ObjectId = _object.Id;
                multicastMessage.Message = message;
                multicastMessage.UsageCount = usageCount;
                return multicastMessage;
            }

            public class MulticastNetworkMessage : INetworkMessage {
                private readonly IBasicObjectPool<MulticastNetworkMessage> _pool;
                private readonly MessageType _toObjectIdMessageType;
                public ObjectId ObjectId;
                public INetworkMessage Message;
                public int UsageCount;

                public MulticastNetworkMessage(IBasicObjectPool<MulticastNetworkMessage> pool, MessageType toObjectIdMessageType) {
                    _pool = pool;
                    _toObjectIdMessageType = toObjectIdMessageType;
                }

                public NetDeliveryMethod QosType {
                    get { return Message.QosType; }
                }

                public void Serialize(NetBuffer writer) {
                    // TODO Put reading an writing of object messages together in
                    // one place for readability
                    writer.Write(_toObjectIdMessageType);
                    writer.Write(ObjectId);
                    Message.Serialize(writer);
                }

                public void Dispose() {
                    if (UsageCount > 1) {
                        UsageCount--;
                    } else {
                        Message.Dispose();
                        _pool.Return(this);
                    }
                }
            }

        }

    }
}
