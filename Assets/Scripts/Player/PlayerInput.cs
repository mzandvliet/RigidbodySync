using System;
using System.Collections.Generic;
using Lidgren.Network;
using RamjetAnvil.Unity.Utility;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.RigidbodyParty {

    public struct PlayerInput {
        public static readonly PlayerInput Zero = new PlayerInput {
            Timestamp = 0.0,
            Thrust = 0,
            Pitch = 0,
            Roll = 0,
            Yaw = 0,
            Boost = false
        };

        public float Pitch;
        public float Roll;
        public float Thrust;
        public double Timestamp;
        public float Yaw;
        public bool Boost;

        public static PlayerInput operator +(PlayerInput a, PlayerInput b) {
            return new PlayerInput {
                Timestamp = a.Timestamp,
                Thrust = a.Thrust + b.Thrust,
                Pitch = a.Pitch + b.Pitch,
                Roll = a.Roll + b.Roll,
                Yaw = a.Yaw + b.Yaw,
                Boost = a.Boost
            };
        }

        public static PlayerInput operator /(PlayerInput a, float s) {
            return new PlayerInput {
                Timestamp = a.Timestamp,
                Thrust = a.Thrust / s,
                Pitch = a.Pitch / s,
                Roll = a.Roll / s,
                Yaw = a.Yaw / s,
                Boost = a.Boost
            };
        }
    }

    public static class PlayerInputExtensions {
        public static PlayerInput Lerp(this PlayerInput a, PlayerInput b, float lerp) {
            return new PlayerInput {
                Timestamp = Mathd.Lerp(a.Timestamp, b.Timestamp, lerp),
                Thrust = Mathf.Lerp(a.Thrust, b.Thrust, lerp),
                Pitch = Mathf.Lerp(a.Pitch, b.Pitch, lerp),
                Roll = Mathf.Lerp(a.Roll, b.Roll, lerp),
                Yaw = Mathf.Lerp(a.Yaw, b.Yaw, lerp),
                Boost = lerp < 0.5f ? a.Boost : b.Boost
            };
        }

        public static PlayerInput Lerp(this IList<PlayerInput> stateBuffer, double time) {
            if (stateBuffer.Count == 0) {
                throw new ArgumentOutOfRangeException("stateBuffer", "is empty");
            }

            if (time < stateBuffer[0].Timestamp) {
                //Debug.LogWarning("Time is older than any state in buffer");
                return stateBuffer[0];
            }

            // Run through states starting with the oldest
            for (int i = 1; i < stateBuffer.Count; i++) {
                PlayerInput rhs = stateBuffer[i];
                if (time < rhs.Timestamp) {
                    PlayerInput lhs = stateBuffer[i - 1];
                    float lerp = (float)Mathd.InverseLerp(lhs.Timestamp, rhs.Timestamp, time);
                    PlayerInput result = Lerp(lhs, rhs, lerp);
                    return result;
                }
            }

            //        Debug.LogWarning("Time " + time + " is newer than newest state in buffer " + stateBuffer[stateBuffer.Count-1].Timestamp);
            return stateBuffer[stateBuffer.Count - 1];
        }

        public static void Serialize(this PlayerInput input, NetBuffer writer) {
            writer.Write(input.Timestamp);
            writer.Write(input.Thrust);
            writer.Write(input.Pitch);
            writer.Write(input.Roll);
            writer.Write(input.Yaw);
            writer.Write(input.Boost);
            writer.WritePadBits(7);
        }

        public static PlayerInput Deserialize(NetBuffer reader) {
            var input = new PlayerInput {
                Timestamp = reader.ReadDouble(),
                Thrust = reader.ReadSingle(),
                Pitch = reader.ReadSingle(),
                Roll = reader.ReadSingle(),
                Yaw = reader.ReadSingle(),
                Boost = reader.ReadBoolean(),
            };
            reader.SkipPadBits(7);
            return input;
        }
    }
}