using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lidgren.Network;
using RamjetAnvil.RamNet;
using UnityEngine;

namespace RamjetAnvil.RigidbodyParty {
    public static class RaceMessage {

        public class RequestRace : IObjectMessage {
            public void Serialize(NetBuffer writer) {}
            public void Deserialize(NetBuffer reader) {}

            public NetDeliveryMethod QosType {
                get { return NetDeliveryMethod.ReliableUnordered; }
            }
        }

        public class RaceStarted : IObjectMessage {
            public readonly IList<RacerPosition> RacerPositions;

            public RaceStarted() {
                RacerPositions = new List<RacerPosition>();
            }

            public void Serialize(NetBuffer writer) {
                writer.Write((buffer, racerPosition) => {
                    buffer.Write(racerPosition.RacerId);
                    buffer.Write(racerPosition.Position);
                    buffer.WriteRotation(racerPosition.Rotation);
                }, RacerPositions);
            }

            public void Deserialize(NetBuffer reader) {
                RacerPositions.Clear();
                reader.ReadList(buffer => new RacerPosition {
                    RacerId = buffer.ReadObjectId(),
                    Position = buffer.ReadVector3(),
                    Rotation = buffer.ReadRotation()
                }, RacerPositions);
            }

            public NetDeliveryMethod QosType {
                get { return NetDeliveryMethod.ReliableOrdered; }
            }
        }

        public class RaceFinished : IObjectMessage {
            public ObjectId Winner;

            public void Serialize(NetBuffer writer) {
                writer.Write(Winner);
            }

            public void Deserialize(NetBuffer reader) {
                Winner = reader.ReadObjectId();
            }

            public NetDeliveryMethod QosType {
                get { return NetDeliveryMethod.ReliableOrdered; }
            }
        }

        public struct RacerPosition {
            public ObjectId RacerId;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public class CheckpointHit : IObjectMessage {
            public uint CheckpointId;
            public ObjectId RacerId;

            public void Serialize(NetBuffer writer) {
                writer.WriteVariableUInt32(CheckpointId);
                writer.Write(RacerId);
            }

            public void Deserialize(NetBuffer reader) {
                CheckpointId = reader.ReadVariableUInt32();
                RacerId = reader.ReadObjectId();
            }

            public NetDeliveryMethod QosType {
                get {
                    return NetDeliveryMethod.ReliableOrdered;
                }
            }
        }

    }
}
