using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.Util;
using UnityEngine.Networking;

namespace RamjetAnvil.RamNet {

    public delegate void OnUnconnectedDataReceived(IPEndPoint endpoint, NetBuffer dataBuffer);
    public delegate void OnDataReceived(ConnectionId connectionId, IPEndPoint endpoint, NetBuffer dataBuffer);
    public delegate void OnConnectionEstablished(ConnectionId connectionId, IPEndPoint endPoint);
    public delegate void OnConnectionFailure(IPEndPoint endpoint, Exception exception);
    public delegate void OnDisconnected(ConnectionId connectionId);

    public enum TransporterStatus {
        Open, Closed
    }

    public interface ITransporter {
        IPEndPoint InternalEndpoint { get; }
        TransporterStatus Status { get; }
    }

    public interface IConnectionlessTransporter : ITransporter {
        event OnUnconnectedDataReceived OnUnconnectedDataReceived;
        void SendUnconnected(IPEndPoint ipEndPoint, NetBuffer buffer);
    }

    public interface IConnectionTransporter : ITransporter, ILatencyInfo {
        event OnConnectionEstablished OnConnectionOpened;
        event OnDisconnected OnConnectionClosed;
        event OnDataReceived OnDataReceived;

        ConnectionId Connect(IPEndPoint endpoint);
        void Disconnect(ConnectionId connectionId);

        void Send(ConnectionId connectionId, NetDeliveryMethod deliveryMethod, NetBuffer buffer);
    }
}
