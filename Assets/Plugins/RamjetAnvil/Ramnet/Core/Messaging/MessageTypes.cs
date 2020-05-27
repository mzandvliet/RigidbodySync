using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.Networking;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.RamNet {

    public interface IMessage : ISerializable {
        NetDeliveryMethod QosType { get; }
    }

    public interface ISerializable {
        void Serialize(NetBuffer writer);
        void Deserialize(NetBuffer reader);
    }

    public interface INetworkMessage : IDisposable {
        NetDeliveryMethod QosType { get; }
        void Serialize(NetBuffer writer);
    }

    public class NetworkMessage<T> : INetworkMessage where T : IMessage {
        private readonly Action<NetworkMessage<T>> _returnToPool;
        public readonly MessageType MessageType;
        public readonly T Content;

        public NetworkMessage(Action<NetworkMessage<T>> returnToPool, MessageType messageType, T content) {
            _returnToPool = returnToPool;
            MessageType = messageType;
            Content = content;
        }

        public void Serialize(NetBuffer writer) {
            writer.Write(MessageType);
            Content.Serialize(writer);
        }

        public NetDeliveryMethod QosType {
            get { return Content.QosType; }
        }

        public void Dispose() {
            _returnToPool(this);
        }
    }
    
    public interface INetworkMessagePool<T> where T : IMessage {
        NetworkMessage<T> Create();
    }

    public class NetworkMessagePool<T> : INetworkMessagePool<T> where T : IMessage, new() {
        private readonly MessageType _messageTypeId;
        private readonly Queue<NetworkMessage<T>> _pool;
        private readonly Action<NetworkMessage<T>> _returnToPool;

        public NetworkMessagePool(IDictionary<Type, MessageType> networkMessageIds) {
            _messageTypeId = networkMessageIds[typeof (T)];
            _pool = new Queue<NetworkMessage<T>>(count: 1);
            _returnToPool = message => _pool.Enqueue(message);
            GrowPool();
        }

        public NetworkMessage<T> Create() {
            if (_pool.Count < 1) {
                GrowPool();
            }
            return _pool.Dequeue();
        }

        private void GrowPool() {
            _pool.Enqueue(new NetworkMessage<T>(_returnToPool, _messageTypeId, new T()));
        }
    }

    public interface IObjectMessage : IMessage {}

    public struct MessageType : IEquatable<MessageType> {
        public readonly uint Value;

        public MessageType(uint value) {
            Value = value;
        }

        public bool Equals(MessageType other) {
            return Value == other.Value;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MessageType && Equals((MessageType) obj);
        }

        public override int GetHashCode() {
            return (int) Value;
        }

        public static bool operator ==(MessageType left, MessageType right) {
            return left.Equals(right);
        }

        public static bool operator !=(MessageType left, MessageType right) {
            return !left.Equals(right);
        }

        public override string ToString() {
            return string.Format("MessageType({0})", Value);
        }
    }

    public class MessagePool {
        private readonly IDictionary<Type, object> _pools;

        public MessagePool(IDictionary<Type, object> pools) {
            _pools = pools;
        }

        public INetworkMessagePool<T> GetPool<T>() where T : IMessage, new() {
            return (INetworkMessagePool<T>)_pools[typeof (T)];
        }

        public NetworkMessage<T> GetMessage<T>() where T : IMessage {
            var pool = (INetworkMessagePool<T>)_pools[typeof (T)];
            return pool.Create();
        }
    }

    public static class MessageExtensions {

        public static IEnumerable<Type> GetNetworkMessageTypes(params Assembly[] userAssemblies) {
            var ramnetAssembly = typeof(IMessage).Assembly;
            return userAssemblies
                .Concat(new [] { ramnetAssembly })
                .GetAllMessageTypes()
                .Where(type => !typeof(IObjectMessage).IsAssignableFrom(type));
        }

        public static IEnumerable<Type> GetObjectMessageTypes(params Assembly[] userAssemblies) {
            var ramnetAssembly = typeof(IMessage).Assembly;
            return userAssemblies
                .Concat(new [] { ramnetAssembly })
                .GetAllMessageTypes()
                .Where(type => typeof(IObjectMessage).IsAssignableFrom(type));
        }

        private static IEnumerable<Type> GetAllMessageTypes(this IEnumerable<Assembly> assemblies) {
            return assemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IMessage).IsAssignableFrom(type) &&
                               type.IsClass &&
                               !type.IsAbstract);
        } 

        public static IDictionary<Type, MessageType> GenerateNetworkIds(IEnumerable<Type> networkMessageTypes) {
            var networkMessages = networkMessageTypes
                .OrderBy(type => type.FullName);

            var messageTypes = new Dictionary<Type, MessageType>();
            var messageId = 0u;
            foreach (var networkMessageType in networkMessages) {
                messageTypes.Add(networkMessageType, new MessageType(messageId));
                messageId++;
            }
            return messageTypes;
        }

        public static IDictionary<Type, object> CreateMessagePools(IDictionary<Type, MessageType> messageTypes) {
            var objectPools = new Dictionary<Type, object>();
            foreach (var kvPair in messageTypes) {
                var messageType = kvPair.Key;
                var messagePool = Activator.CreateInstance(typeof (NetworkMessagePool<>).MakeGenericType(messageType), messageTypes);
                objectPools.Add(messageType, messagePool);
            }
            return objectPools;
        } 

    }
}
