using RamjetAnvil.RamNet;

namespace RamjetAnvil.RigidbodyParty {

    public static class NetworkGroup {
        public static readonly TransportGroupId Default = TransportGroupRouter.DefaultGroup;
    }

    public static class ObjectTypes {
        public static readonly ObjectType Player = new ObjectType(0);
    }
}
