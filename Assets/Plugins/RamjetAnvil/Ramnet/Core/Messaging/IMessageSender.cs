using System.Net;
using RamjetAnvil.Util;

namespace RamjetAnvil.RamNet {

    public interface IMessageSender {
        IPEndPoint InternalEndpoint { get; }
        void Send(ConnectionId connectionId, INetworkMessage message);
    }
}
