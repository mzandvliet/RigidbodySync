using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using RamjetAnvil.Coroutine;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;
using Guid = RamjetAnvil.Util.Guid;
using MessageType = RamjetAnvil.RamNet.MessageType;

public class RamnetBase {

    private readonly LidgrenNetworkTransporter _transporter;
    private readonly LidgrenNatPunchClient _natPunchClient;
    private readonly LidgrenNatFacilitatorConnection _natFacilitatorConnection;
    private readonly IMessageSender _messageSender;
    private readonly ReplicatedObjectStore _replicatedObjectStore;
    private readonly IConnectionManager _connectionManager;
    private readonly TransportGroupRouter _groupRouter;
    private readonly MessageRouter _messageRouter;
    private readonly MessagePool _messagePool;
    private readonly IConnectionlessMessageSender _connectionlessMessageSender;
    private readonly IDictionary<Guid, GameObject> _preExistingObjects;

    public RamnetBase(
        ICoroutineScheduler coroutineScheduler,
        LidgrenNetworkTransporter transporter, 
        IPEndPoint natFacilitatorEndpoint,
        IMessageSender messageSender, 
        IDictionary<ObjectType, GameObject> prefabs, 
        DependencyContainer dependencyContainer) {

        _messageSender = messageSender;
        _transporter = transporter;
        ILatencyInfo latencyInfo = _transporter;

        var networkMessageTypes = MessageExtensions.GenerateNetworkIds(MessageExtensions.GetNetworkMessageTypes(Assembly.GetExecutingAssembly()));
        var objectMessageTypes = MessageExtensions.GenerateNetworkIds(MessageExtensions.GetObjectMessageTypes(Assembly.GetExecutingAssembly()));
        _messagePool = new MessagePool(MessageExtensions.CreateMessagePools(networkMessageTypes));
        var objectMessagePools = MessageExtensions.CreateMessagePools(objectMessageTypes);

        var routerConfig = new TransportRouterConfig(
            new KeyValuePair<TransportGroupId, TransportGroupConfig>(NetworkGroup.Default,
                new TransportGroupConfig(32)));
        _natFacilitatorConnection = new LidgrenNatFacilitatorConnection(natFacilitatorEndpoint, _transporter);
        _natPunchClient = new LidgrenNatPunchClient(coroutineScheduler, _natFacilitatorConnection, transporter);
        _connectionManager = new LidgrenPunchThroughFacilitator(
            _transporter,
            coroutineScheduler,
            connectionAttemptTimeout: 10f,
            natPunchClient: _natPunchClient);
        _groupRouter = new TransportGroupRouter(_transporter, routerConfig);

        Func<ObjectMessageRouter> objectMessageDispatcherFactory = 
            () => new ObjectMessageRouter(latencyInfo, objectMessageTypes);
        var networkMessagePool = new BasicObjectPool<ReplicatedObjectStore.ObjectMessageSender.MulticastNetworkMessage>(
            pool => new ReplicatedObjectStore.ObjectMessageSender.MulticastNetworkMessage(pool, networkMessageTypes[typeof(BasicMessage.ToObject)]));
        var messageSenderFactory = ReplicatedObjectStore.ObjectMessageSender.CreateFactory(
            messageSender,
            _groupRouter,
            NetworkGroup.Default,
            networkMessagePool);

        var objectDependencies = new List<object>();
        objectDependencies.Add(latencyInfo);
        foreach (var pool in objectMessagePools) {
            objectDependencies.Add(pool.Value);
        }
        var replicationDecorator = ReplicatedObjectStore.GameObjectReplicationDecorator(objectMessageDispatcherFactory,
            messageSenderFactory,
            objectDependencies,
            objectMessageTypes);

        var gameObjectFactories = new Dictionary<ObjectType, Func<GameObject>>();
        foreach (var kvPair in prefabs) {
            var objectType = kvPair.Key;
            var prefab = kvPair.Value;
            gameObjectFactories[objectType] = () => {
                var instance = GameObject.Instantiate(prefab);
                // TODO Copy dependencies into global object dependency container
                DependencyInjection.Inject(instance, dependencyContainer);
                return instance;
            };
        }
        int replicatedObjectCapacity = 256;
        var objectMessageParser = new ObjectMessageParser(objectMessageTypes);
        _replicatedObjectStore = new ReplicatedObjectStore(objectMessageParser, gameObjectFactories, replicationDecorator, replicatedObjectCapacity);

        _messageRouter = new MessageRouter(networkMessageTypes);
        _groupRouter.SetDataHandler(NetworkGroup.Default, _messageRouter);

        _connectionlessMessageSender = new ConnectionlessMessageSender(_transporter);

        _preExistingObjects = RamjetAnvil.RamNet.PreExistingObjects.FindAll();
    }

    public IConnectionManager ConnectionManager {
        get { return _connectionManager; }
    }

    public IMessageSender MessageSender {
        get { return _messageSender; }
    }

    public ReplicatedObjectStore ReplicatedObjectStore {
        get { return _replicatedObjectStore; }
    }

    public TransportGroupRouter GroupRouter {
        get { return _groupRouter; }
    }

    public MessageRouter MessageRouter {
        get { return _messageRouter; }
    }

    public MessagePool MessagePool {
        get { return _messagePool; }
    }

    public LidgrenNatFacilitatorConnection NatFacilitatorConnection {
        get { return _natFacilitatorConnection; }
    }

    public IDictionary<Guid, GameObject> PreExistingObjects {
        get { return _preExistingObjects; }
    }
    
    public IConnectionlessMessageSender ConnectionlessMessageSender {
        get { return _connectionlessMessageSender; }
    }
}
