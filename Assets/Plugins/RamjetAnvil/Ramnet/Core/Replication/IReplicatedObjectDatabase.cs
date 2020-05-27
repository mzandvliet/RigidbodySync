using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.RamNet;

namespace RamjetAnvil.RamNet {
    public interface IReplicatedObjectDatabase {
        event Action<ReplicatedObject> ObjectAdded;
        event Action<ReplicatedObject> ObjectRemoved;

        void FindObjects(ObjectType type, IList<ReplicatedObject> results, ObjectRole role = ObjectRoles.Everyone);
        ReplicatedObject FindObject(ObjectType type, ObjectRole role = ObjectRoles.Everyone);
        ReplicatedObject Find(ObjectId id);
    }
}
