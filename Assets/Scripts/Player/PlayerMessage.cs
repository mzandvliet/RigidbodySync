using System;
using Lidgren.Network;
using RamjetAnvil.RamNet;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.RigidbodyParty {

    public static class PlayerMessage {

        public class SetColor : IObjectMessage {

            public Color32 Color;

            public void Serialize(NetBuffer writer) {
                writer.WriteRgbColor(Color);
            }

            public void Deserialize(NetBuffer reader) {
                Color = reader.ReadRgbColor();
            }

            public NetDeliveryMethod QosType {
                get { return NetDeliveryMethod.ReliableUnordered; }
            }
        }

        public class InputUpdate : IObjectMessage {
            public PlayerInput Input;

            public void Serialize(NetBuffer networkWriter) {
                Input.Serialize(networkWriter);
            }

            public void Deserialize(NetBuffer networkReader) {
                Input = PlayerInputExtensions.Deserialize(networkReader);
            }

            public NetDeliveryMethod QosType { get { return NetDeliveryMethod.UnreliableSequenced; } }
        }

        public class StateUpdate : IObjectMessage {

            public RigidbodyState State;

            public void Serialize(NetBuffer writer) {
                State.Serialize(writer);
            }

            public void Deserialize(NetBuffer reader) {
                State = RigidbodyExtensions.Deserialize(reader);
            }

            public NetDeliveryMethod QosType { get { return NetDeliveryMethod.UnreliableSequenced; } }
        }

        public class CollisionEvent : IObjectMessage {
            public void Serialize(NetBuffer writer) {}
            public void Deserialize(NetBuffer reader) {}

            public NetDeliveryMethod QosType { get { return NetDeliveryMethod.ReliableUnordered;} }
        }
    }
}
