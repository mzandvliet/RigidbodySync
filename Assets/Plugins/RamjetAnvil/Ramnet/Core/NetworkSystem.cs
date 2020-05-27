using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.Unity.Utility;
using RamjetAnvil.Util;
using UnityEngine;

namespace RamjetAnvil.RamNet {

    public class NetworkSystems : IDisposable {
        private readonly IDictionary<Type, MessageType> _messageTypes;
        private readonly IConnectionlessMessageSender _connectionlessMessageSender;
        private readonly IMessageSender _messageSender;
        private readonly MessagePool _messagePool;
        private readonly IConnectionManager _connectionManager;
        private readonly TransportGroupRouter _groupRouter;
        private readonly ReplicatedObjectStore _objectStore;

        public NetworkSystems(IMessageSender messageSender, 
            IConnectionlessMessageSender connectionlessMessageSender, 
            MessagePool messagePool, 
            IConnectionManager connectionManager, TransportGroupRouter groupRouter, 
            ReplicatedObjectStore objectStore, IDictionary<Type, MessageType> messageTypes) {

            _messageSender = messageSender;
            _messagePool = messagePool;
            _connectionlessMessageSender = connectionlessMessageSender;
            _connectionManager = connectionManager;
            _groupRouter = groupRouter;
            _objectStore = objectStore;
            _messageTypes = messageTypes;
        }

        public IMessageSender MessageSender {
            get { return _messageSender; }
        }

        public IConnectionManager ConnectionManager {
            get { return _connectionManager; }
        }

        public TransportGroupRouter GroupRouter {
            get { return _groupRouter; }
        }

        public ReplicatedObjectStore ObjectStore {
            get { return _objectStore; }
        }

        public MessagePool MessagePool {
            get { return _messagePool; }
        }

        public IDictionary<Type, MessageType> MessageTypes {
            get { return _messageTypes; }
        }

        public IConnectionlessMessageSender ConnectionlessMessageSender {
            get { return _connectionlessMessageSender; }
        }

        public void Dispose() {
            _connectionManager.Dispose();
            _groupRouter.Dispose();
        }
    }

    public static class NetworkSystem {

        public static NetworkSystems Create(
            IConnectionTransporter transporter,
            IConnectionlessTransporter connectionlessTransporter,
            TransportRouterConfig groupRouterConfig,
            IDictionary<ObjectType, GameObject> replicationPrefabs,
            IMessageSender messageSender,
            IConnectionManager connectionManager) {
            
            ILatencyInfo latencyInfo = transporter;
            var networkMessageTypes = MessageExtensions.GenerateNetworkIds(MessageExtensions.GetNetworkMessageTypes());
            var objectMessageTypes = MessageExtensions.GenerateNetworkIds(MessageExtensions.GetObjectMessageTypes());
            
            var connectionlessMessageSender = new ConnectionlessMessageSender(connectionlessTransporter);

            var messagePool = new MessagePool(MessageExtensions.CreateMessagePools(networkMessageTypes));
            var objectMessagePools = MessageExtensions.CreateMessagePools(objectMessageTypes);

            var groupRouter = new TransportGroupRouter(transporter, groupRouterConfig);

            Func<ObjectMessageRouter> objectMessageDispatcherFactory = 
                () => new ObjectMessageRouter(latencyInfo, objectMessageTypes);
            var networkMessagePool = new BasicObjectPool<ReplicatedObjectStore.ObjectMessageSender.MulticastNetworkMessage>(
                pool => new ReplicatedObjectStore.ObjectMessageSender.MulticastNetworkMessage(pool, networkMessageTypes[typeof(BasicMessage.ToObject)]));
            var objectMessageSenderFactory = ReplicatedObjectStore.ObjectMessageSender.CreateFactory(
                messageSender,
                groupRouter,
                new TransportGroupId(2),
                networkMessagePool);

            var objectDependencies = new List<object> {latencyInfo};
            foreach (var pool in objectMessagePools) {
                objectDependencies.Add(pool.Value);
            }
            var replicationDecorator = ReplicatedObjectStore.GameObjectReplicationDecorator(objectMessageDispatcherFactory,
                objectMessageSenderFactory,
                objectDependencies,
                objectMessageTypes);
            var replicatedObjectPools = replicationPrefabs
                .Select(kvPair => new KeyValuePair<ObjectType, Func<GameObject>>(kvPair.Key, kvPair.Value.ToFactory()))
                .ToDictionary(kvPair => kvPair.Key, kvPair => kvPair.Value);
            int replicatedObjectCapacity = 256;
            var objectMessageParser = new ObjectMessageParser(objectMessageTypes);
            var replicatedObjectStore = new ReplicatedObjectStore(objectMessageParser, replicatedObjectPools, 
                replicationDecorator, replicatedObjectCapacity);

            return new NetworkSystems(
                messageSender, 
                connectionlessMessageSender,
                messagePool,
                connectionManager,
                groupRouter,
                replicatedObjectStore,
                networkMessageTypes);
        }

        public static void InstallBasicServerHandlers(MessageRouter messageRouter,
            IClock clock, IClock fixedClock, Func<ushort> playerCount, NetworkSystems networkSystems) {

            var messagePools = networkSystems.MessagePool;
            var messageSender = networkSystems.MessageSender;
            messageRouter
                .RegisterHandler(DefaultMessageHandlers.Ping(clock, fixedClock,
                    messagePools.GetPool<BasicMessage.Pong>(),
                    messageSender))
                .RegisterHandler(DefaultMessageHandlers.ToObject(networkSystems.ObjectStore));
        }

        public static void InstallBasicClientHandlers(MessageRouter messageRouter, NetworkSystems networkSystems) {
            messageRouter
                .RegisterHandler(DefaultMessageHandlers.CreateObject(networkSystems.ObjectStore))
                .RegisterHandler(DefaultMessageHandlers.DeleteObject(networkSystems.ObjectStore))
                .RegisterHandler(DefaultMessageHandlers.ToObject(networkSystems.ObjectStore));
        }

        public static class DefaultMessageHandlers {

            public static Action<ConnectionId, IPEndPoint, BasicMessage.CreateObject, NetBuffer> CreateObject(
                ReplicatedObjectStore objectStore) {

                return (connectionId, endpoint, message, reader) => {
                    var instance = objectStore.AddReplicatedInstance(message.ObjectType, message.ObjectRole,
                        message.ObjectId, connectionId);
                    objectStore.DispatchMessages(connectionId,
                        message.ObjectId,
                        message.AdditionalData,
                        message.AdditionalData.WriterPosition());
                    instance.Activate();
                };
            }

            public static Action<ConnectionId, BasicMessage.DeleteObject> DeleteObject(ReplicatedObjectStore objectStore) {
                return (connectionId, message) => {
                    objectStore.RemoveReplicatedInstance(connectionId, message.ObjectId);
                };
            }

            public static Action<ConnectionId, IPEndPoint, BasicMessage.ToObject, NetBuffer> ToObject(
                ReplicatedObjectStore objectStore) {

                return (connectionId, endpoint, message, reader) => {
                    // TODO How many bytes of the reader should be read?
                    objectStore.DispatchMessage(connectionId, message.ReceiverId, reader);
                };
            }

            public static Action<IPEndPoint, BasicMessage.Ping> Ping(
                IClock clock,
                IClock fixedClock,
                INetworkMessagePool<BasicMessage.Pong> messagePool,
                IConnectionlessMessageSender messageSender) {

                return (endpoint, ping) => {
                    Debug.Log("connectionless ping received");
                    var pong = messagePool.Create();
                    pong.Content.SenderTimestamp = ping.Timestamp;
                    pong.Content.SenderFixedTimestamp = ping.FixedTimestamp;
                    pong.Content.Timestamp = clock.CurrentTime;
                    pong.Content.FixedTimestamp = fixedClock.CurrentTime;
                    messageSender.Send(endpoint, pong);
                };
            }

            public static Action<ConnectionId, BasicMessage.Ping> Ping(
                IClock clock,
                IClock fixedClock,
                INetworkMessagePool<BasicMessage.Pong> messagePool,
                IMessageSender messageSender) {

                return (connectionId, ping) => {
                    var pong = messagePool.Create();
                    pong.Content.SenderTimestamp = ping.Timestamp;
                    pong.Content.SenderFixedTimestamp = ping.FixedTimestamp;
                    pong.Content.Timestamp = clock.CurrentTime;
                    pong.Content.FixedTimestamp = fixedClock.CurrentTime;
                    messageSender.Send(connectionId, pong);
                };
            }

            public static Action<IPEndPoint, BasicMessage.Ping> UnconnectedPing(
                IClock clock,
                IClock fixedClock,
                INetworkMessagePool<BasicMessage.Pong> messagePool,
                IConnectionlessMessageSender messageSender) {

                return (endpoint, ping) => {
                    var pong = messagePool.Create();
                    pong.Content.SenderTimestamp = ping.Timestamp;
                    pong.Content.SenderFixedTimestamp = ping.FixedTimestamp;
                    pong.Content.Timestamp = clock.CurrentTime;
                    pong.Content.FixedTimestamp = fixedClock.CurrentTime;
                    messageSender.Send(endpoint, pong);
                };
            }

            public static Action<IPEndPoint, BasicMessage.RequestHostInfo> HostInfoRequest(
                INetworkMessagePool<BasicMessage.HostInfoResponse> messagePool,
                Func<ushort> playerCount,
                IConnectionlessMessageSender messageSender) {

                return (endpoint, message) => {
                    var response = messagePool.Create();
                    response.Content.SenderTimestamp = message.Timestamp;
                    response.Content.PlayerCount = playerCount();
                    messageSender.Send(endpoint, response);
                };
            }
            
        }
        
    }
}
