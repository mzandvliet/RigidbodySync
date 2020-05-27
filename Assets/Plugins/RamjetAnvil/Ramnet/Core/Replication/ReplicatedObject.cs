using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.Unity.Utility;
using UnityEngine;
using Guid = RamjetAnvil.Util.Guid;

namespace RamjetAnvil.RamNet {

    public class ReplicatedObject {
        public ObjectType? Type;
        public ObjectRole Role;
        public ConnectionId OwnerConnectionId;
        public ConnectionId AuthorityConnectionId;
        public ObjectId Id;
        public readonly Guid GlobalObjectId;
        public bool IsPreExisting;
        public readonly GameObject GameObject;
        public readonly GameObjectNetworkInfo GameObjectNetworkInfo;
        public readonly ObjectMessageRouter MessageHandler;
        public readonly IReplicationConstructor ReplicationConstructor;

        public ReplicatedObject(
            GameObject gameObject, 
            ObjectMessageRouter messageHandler, 
            IReplicationConstructor replicationConstructor) {

            GlobalObjectId = new Guid();
            GameObject = gameObject;
            MessageHandler = messageHandler;
            ReplicationConstructor = replicationConstructor;
            GameObjectNetworkInfo = gameObject.GetComponent<GameObjectNetworkInfo>() ?? gameObject.AddComponent<GameObjectNetworkInfo>();
        }

        public void Activate() {
            if (!GameObject.IsDestroyed()) {
                GameObject.SetActive(true);    
            }
        }
    }
}
