using System;
using System.Collections.Generic;
using System.Net;
using Lidgren.Network;
using RamjetAnvil.Networking;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.RamNet {
    
    public delegate void MessageHandler<in T>(ConnectionId connectionId, IPEndPoint endpoint, T message, NetBuffer reader);

    public class MessageRouter : ITransportDataHandler {
        private readonly IDictionary<Type, MessageType> _networkMessageTypes; 
        private readonly IDictionary<MessageType, Action<ConnectionId, IPEndPoint, NetBuffer>> _handlers;

        public MessageRouter(IDictionary<Type, MessageType> networkMessageTypes) {
            _networkMessageTypes = networkMessageTypes;
            _handlers = new ArrayDictionary<MessageType, Action<ConnectionId, IPEndPoint, NetBuffer>>(
                messageType => (int) messageType.Value,
                i => new MessageType((uint) i), 
                _networkMessageTypes.Count);

//            foreach (var networkMessageType in _networkMessageTypes) {
//                Debug.Log("network message type: " + networkMessageType.Key + " has id: " + networkMessageType.Value);
//            }
        }

        public void OnDataReceived(ConnectionId connectionId, IPEndPoint endpoint, NetBuffer reader) {
            while (reader.PositionInBytes < reader.LengthBytes) {
                var messageSize = reader.ReadVariableUInt32();
                var messageStartPosition = reader.PositionInBytes;
                var messageType = reader.ReadMessageType();

                Action<ConnectionId, IPEndPoint, NetBuffer> handler;
                if (_handlers.TryGetValue(messageType, out handler)) {
                    handler(connectionId, endpoint, reader);
                }
                // Skip bytes that weren't read
                var bytesRead = reader.PositionInBytes - messageStartPosition;
                var bytesToSkip = messageSize - bytesRead;
                if (bytesRead > messageSize) {
                    // TODO How to handle this error?
                    throw new Exception("Error! Bytes read (" + bytesRead + ") > messageSizeInBytes (" + messageSize + ")" + ", reader posInBytes: " + reader.PositionInBytes);
                }
                reader.SkipBytes(bytesToSkip);
            }
        }
        
        public MessageRouter RegisterHandler<T>(Action<ConnectionId, IPEndPoint, T, NetBuffer> handler) where T : IMessage, new() {
            var message = new T();
            var messageTypeId = _networkMessageTypes[typeof (T)];
            _handlers[messageTypeId] = (connectionId, endpoint, reader) => {
                message.Deserialize(reader);
                handler(connectionId, endpoint, message, reader);
            };
            return this;
        }

        public void ClearHandlers() {
            _handlers.Clear();
        }
    }

    public static class BasicMessageRouterExtensions {
        
        public static MessageRouter RegisterHandler<T>(this MessageRouter router, Action<T> handler) where T : IMessage, new() {
            return router.RegisterHandler<T>((connectionId, endpoint, msg, reader) => handler(msg));
        }

        public static MessageRouter RegisterHandler<T>(this MessageRouter router, Action<ConnectionId, T> handler) where T : IMessage, new() {
            return router.RegisterHandler<T>((connectionId, endpoint, msg, reader) => handler(connectionId, msg));
        }

        public static MessageRouter RegisterHandler<T>(this MessageRouter router, Action<ConnectionId, IPEndPoint, T> handler) where T : IMessage, new() {
            return router.RegisterHandler<T>((connectionId, endpoint, msg, reader) => handler(connectionId, endpoint, msg));
        }
    }
}