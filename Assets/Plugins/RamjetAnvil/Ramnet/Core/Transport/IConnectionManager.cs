using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace RamjetAnvil.RamNet {

    public interface IConnectionManager : IDisposable {
        void Connect(IPEndPoint hostEndpoint,
            OnConnectionEstablished onConnectionEstablished = null,
            OnConnectionFailure onConnectionFailure = null,
            OnDisconnected onDisconnected = null);
        void Disconnect(ConnectionId connectionId);
    }
}
