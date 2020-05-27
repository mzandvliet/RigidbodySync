using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RamjetAnvil.RamNet;
using RamjetAnvil.Util;
using UnityEngine;
using UnityEngine.Networking;
using Whathecode.System;
using Object = UnityEngine.Object;

namespace RamjetAnvil.RamNet {

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class MessageHandler : Attribute {
        public readonly ObjectRole AllowedSenders;

        public MessageHandler(ObjectRole allowedSenders = ObjectRoles.Everyone) {
            AllowedSenders = allowedSenders;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class NetworkRole : Attribute {
        private readonly ObjectRole _includedObjectRoles;
        private readonly ObjectRole _excludedObjectRoles;

        public NetworkRole(ObjectRole roles) {
            _includedObjectRoles = roles;
            // Authority is mostly mutually exlusive with the Others role so
            // it is excluded unless it is explicitely included.
            if (roles.IsOther() && !roles.IsAuthority()) {
                _excludedObjectRoles = ObjectRole.Authority;
            } else {
                _excludedObjectRoles = ObjectRole.Nobody;
            }
        }

        public bool IsAllowed(ObjectRole objectRole) {
            return (_includedObjectRoles & objectRole) != 0 &&
                   (_excludedObjectRoles & objectRole) == 0;
        }

        public ObjectRole Roles {
            get { return _includedObjectRoles; }
        }
    }

    public struct MessageMetadata {
        public readonly ConnectionId ConnectionId;
        public readonly float Latency;

        public MessageMetadata(ConnectionId connectionId, float latency) {
            ConnectionId = connectionId;
            Latency = latency;
        }
    }

    

    public class ObjectMessageRouter {

        // TODO Consider using a global router for all objects
        // to more efficiently store handlers

        private readonly IDictionary<Type, MessageType> _messageTypeIds; 
        private readonly IDictionary<MessageType, IList<ObjectMessageHandler>> _registeredHandlers;
        private readonly ILatencyInfo _latencyInfo;

        public ObjectMessageRouter(ILatencyInfo latencyInfo, IDictionary<Type, MessageType> messageTypeIds) {
            _latencyInfo = latencyInfo;
            _messageTypeIds = messageTypeIds;
            _registeredHandlers = new ArrayDictionary<MessageType, IList<ObjectMessageHandler>>(
                i => (int) i.Value,
                i => new MessageType((uint)i),
                _messageTypeIds.Count);
        }

        public void Dispatch(MessageType messageType, IObjectMessage message, ConnectionId connectionId, ObjectRole sender) {
            IList<ObjectMessageHandler> handlers;
            if (_registeredHandlers.TryGetValue(messageType, out handlers)) {
                for (int i = 0; i < handlers.Count; i++) {
                    var handler = handlers[i];
                    if ((handler.MetaData.AllowedSenders & sender) != 0) {
                        handler.Invoke(message, new MessageMetadata(connectionId, _latencyInfo.GetLatency(connectionId)));
                    } else {
                        Debug.Log(sender + " is not allowed to send message of type " + message.GetType());
                    }
                }
            }
        }

        public ObjectMessageRouter RegisterHandler(Type messageType, ObjectMessageHandler handler) {
            var messageTypeId = _messageTypeIds[messageType];
            IList<ObjectMessageHandler> existingHandlers;
            if (!_registeredHandlers.TryGetValue(messageTypeId, out existingHandlers)) {
                existingHandlers = new List<ObjectMessageHandler>();
                _registeredHandlers[messageTypeId] = existingHandlers;
            }
            existingHandlers.Add(handler);
            return this;
        }
    }

    //public delegate void ObjectMessageHandler(IObjectMessage message, MessageMetadata connectionId);

    public class ObjectMessageHandler {
        public readonly MessageHandler MetaData;
        public readonly Action<IObjectMessage, MessageMetadata> Invoke;

        public ObjectMessageHandler(MessageHandler metaData, Action<IObjectMessage, MessageMetadata> invoke) {
            MetaData = metaData;
            Invoke = invoke;
        }
    }

    public static class ObjectMessageDispatcherExtensions {

        private static readonly List<MonoBehaviour> ComponentCache = new List<MonoBehaviour>(); 

        public static void RegisterGameObject(
            this ObjectMessageRouter router, 
            GameObject g) {

            ComponentCache.Clear();
            g.GetComponentsInChildren(ComponentCache);

            for (int componentIndex = 0; componentIndex < ComponentCache.Count; componentIndex++) {
                var component = ComponentCache[componentIndex];
                var componentType = component.GetType();
                var callSites = GetMessageHandlerCallSite(componentType);
                for (int i = 0; i < callSites.Count; i++) {
                    var callSite = callSites[i];

                    if (VerifySignature(callSite.MethodInfo, new[] {typeof (IObjectMessage), typeof (MessageMetadata)}) ||
                        VerifySignature(callSite.MethodInfo, new[] {typeof (IObjectMessage)})) {

                        var handlerParameters = callSite.MethodInfo.GetParameters();
                        var messageParameter = handlerParameters[0];
                        var handlerType = messageParameter.ParameterType;
                        // TODO Better error handling
                        if (handlerParameters.Length == 1) {
                            var messageHandleCallSite = DelegateHelper.CreateDelegate<Action<IObjectMessage>>(
                                callSite.MethodInfo,
                                component);
                            router.RegisterHandler(handlerType, new ObjectMessageHandler(
                                metaData: callSite.MetaData,
                                invoke: (message, metadata) => {
                                    if (component.enabled) {
                                        messageHandleCallSite(message);
                                    }
                                }));
                        } else if (handlerParameters.Length == 2) {
                            var messageHandleCallSite = DelegateHelper.CreateDelegate<Action<IObjectMessage, MessageMetadata>>(
                                callSite.MethodInfo,
                                component);
                            router.RegisterHandler(handlerType, new ObjectMessageHandler(
                                metaData: callSite.MetaData,
                                invoke: (message, metadata) => {
                                    if (component.enabled) {
                                        messageHandleCallSite(message, metadata);
                                    }
                                }));
                        }
                    } else {
                        throw new Exception("Message handler has incorrect signature: " +
                                            "use (IObjectMessage, MessageMetadata) or (IObjectMessage) instead");
                    }
                }
            }
        }

        private static bool VerifySignature(MethodInfo method, Type[] requiredParams) {
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++) {
                var parameter = parameters[i];
                if (i >= requiredParams.Length) {
                    return false;
                }
                if (!requiredParams[i].IsAssignableFrom(parameter.ParameterType)) {
                    return false;
                }
            }
            return true;
        }

        public static readonly Func<Type, IList<MessageHandlerReference>> GetMessageHandlerCallSite = Memoization
            .Memoize<Type, IList<MessageHandlerReference>>(
                componentType => {
                    var callSites = new List<MessageHandlerReference>();
                    var methods = componentType.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++) {
                        var method = methods[methodIndex];
                        var attributes = method.GetCustomAttributes(typeof (MessageHandler), inherit: true);
                        var isMessageHandler = attributes.Length > 0;
                        if (isMessageHandler) {
                            callSites.Add(new MessageHandlerReference(attributes[0] as MessageHandler, method));
                        }
                    }
                    return callSites;
                });

        public static void ApplyRoleTo(this ObjectRole objectRole, GameObject instance) {
            ComponentCache.Clear();
            instance.GetComponentsInChildren(ComponentCache);
            for (int componentIndex = 0; componentIndex < ComponentCache.Count; componentIndex++) {
                var component = ComponentCache[componentIndex];
                var networkRole = GetNetworkRole(component.GetType());
                if (networkRole != null) {
                    component.enabled = networkRole.IsAllowed(objectRole);
                }
            }
        }

        public static readonly Func<Type, NetworkRole> GetNetworkRole = Memoization.Memoize<Type, NetworkRole>(componentType => {
            var attributes = componentType.GetCustomAttributes(typeof (NetworkRole), inherit: false);
            if (attributes.Length > 0) {
                return (NetworkRole) attributes[0];
            }
            return null;
        });

        public class MessageHandlerReference {
            public readonly MessageHandler MetaData;
            public readonly MethodInfo MethodInfo;

            public MessageHandlerReference(MessageHandler metaData, MethodInfo methodInfo) {
                MetaData = metaData;
                MethodInfo = methodInfo;
            }
        }

    }
}

