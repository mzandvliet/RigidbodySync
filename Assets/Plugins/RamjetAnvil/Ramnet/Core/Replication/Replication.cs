using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.Unity.Utility;
using RamjetAnvil.Util;
using UnityEngine;

namespace RamjetAnvil.RamNet {

    public static class Replication {

        private static readonly IList<ObjectId> ObjectIdCache = new List<ObjectId>(); 
        public static void RemovePlayer(this IMessageSender messageSender, 
            MessagePool messagePool,
            ConnectionId connectionId, 
            IList<ConnectionId> others, 
            Replicator replicator) {

            var store = replicator.Store;
            ObjectIdCache.Clear();
            store.ObjectIds.CopyListInto(ObjectIdCache);
            for (int i = 0; i < ObjectIdCache.Count; i++) {
                var objectId = ObjectIdCache[i];
                var instance = store.Find(objectId);
                if (instance.OwnerConnectionId == connectionId || instance.AuthorityConnectionId == connectionId) {
                    replicator.RemoveReplicatedInstance(objectId);

                    for (int j = 0; j < others.Count; j++) {
                        var otherConnectionId = others[j];
                        messageSender.DeleteObject(messagePool, otherConnectionId, instance.Id);
                    }
                }
            }
        }

        public static void DeleteObject(this IMessageSender messageSender,
            MessagePool messagePool,
            ConnectionId connectionId,
            ObjectId objectId) {
            
            var deleteObjectMsg = messagePool.GetMessage<BasicMessage.DeleteObject>();
            deleteObjectMsg.Content.ObjectId = objectId;
            messageSender.Send(connectionId, deleteObjectMsg);
        }

        public static void RemoveAll(this ReplicatedObjectStore store, 
            ConnectionId connectionId) {

            ObjectIdCache.Clear();
            store.ObjectIds.CopyListInto(ObjectIdCache);
            for (int i = 0; i < ObjectIdCache.Count; i++) {
                var objectId = ObjectIdCache[i];
                var instance = store.Find(objectId);
                if (instance.OwnerConnectionId == connectionId || instance.AuthorityConnectionId == connectionId) {
                    store.RemoveReplicatedInstance(connectionId, objectId);
                }
            }
        }

        public static void Replicate(
            this IMessageSender messageSender,
            MessagePool messagePool,
            IList<ConnectionId> receivers,
            ReplicatedObject instance) {

            for (int i = 0; i < receivers.Count; i++) {
                var receiver = receivers[i];
                messageSender.Replicate(messagePool, receiver, instance);
            }
        }

        public static void ReplicateEverything(
            this IMessageSender messageSender,
            MessagePool messagePool,
            ConnectionId receiver,
            ReplicatedObjectStore store) {
            
            var existingObjectIds = store.ObjectIds;
            for (int i = 0; i < existingObjectIds.Count; i++) {
                var objectId = existingObjectIds[i];
                var @object = store.Find(objectId);
                messageSender.Replicate(messagePool, receiver, @object);
            }
        }

        public static void Replicate(this IMessageSender messageSender,
            MessagePool messagePool,
            ConnectionId receiver,
            ReplicatedObject instance) {

            var objectRole = ObjectRole.Nobody;
            var isOwner = receiver == instance.OwnerConnectionId;
            var isAuthority = receiver == instance.AuthorityConnectionId;
            objectRole = objectRole | (isOwner ? ObjectRole.Owner : ObjectRole.Nobody);
            objectRole = objectRole | (isAuthority ? ObjectRole.Authority : ObjectRole.Nobody);
            objectRole = objectRole | (!isOwner && !isAuthority ? ObjectRole.Others : ObjectRole.Nobody);

            Debug.Log("replicating to " + receiver + " role: " + objectRole);

            if (instance.IsPreExisting) {
                var replicatePreExistingMsg = messagePool.GetMessage<BasicMessage.ReplicatePreExistingObject>();
                replicatePreExistingMsg.Content.GlobalObjectId.CopyFrom(instance.GlobalObjectId);
                replicatePreExistingMsg.Content.NetworkObjectId = instance.Id;
                replicatePreExistingMsg.Content.ObjectRole = objectRole;
                messageSender.Send(receiver, replicatePreExistingMsg);
            } else {
                var createObjectMsg = messagePool.GetMessage<BasicMessage.CreateObject>();
                createObjectMsg.Content.ObjectId = instance.Id;
                createObjectMsg.Content.ObjectRole = objectRole;
                createObjectMsg.Content.ObjectType = instance.Type.Value;

                // Add any additional messages that are required to atomically construct the object
                createObjectMsg.Content.AdditionalData.Reset();
                instance.ReplicationConstructor.SerializeInitialState(createObjectMsg.Content.AdditionalData);

                messageSender.Send(receiver, createObjectMsg);
            }
        }
    }
}
