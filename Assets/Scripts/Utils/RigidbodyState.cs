using System;
using Lidgren.Network;
using RamjetAnvil.RamNet;
using RamjetAnvil.Unity.Utility;
using UnityEngine;


/* Todo: 
 * 
 * - Try various dead reckoning and correction techniques
 * - Resimulation technique will need to match game-specific physics properties as best as possible
 */


[Serializable]
public struct RigidbodyState {
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public Vector3 Acceleration;
    public Vector3 AngularVelocity;
    public Vector3 AngularAcceleration;

    public override string ToString() {
        return string.Format("Position: {1}, Rotation: {2}, Velocity: {3}, Acceleration: {4}, AngularVelocity: {5}, AngularAcceleration: {6}", Position, Rotation, Velocity, Acceleration, AngularVelocity, AngularAcceleration);
    }

    public static RigidbodyState Zero = new RigidbodyState {
        Rotation = Quaternion.identity
    };
}

public static class RigidbodyExtensions {
    public static void Reset(Rigidbody r) {
        r.velocity = Vector3.zero;
        r.angularVelocity = Vector3.zero;
    }

    public static RigidbodyState Lerp(RigidbodyState a, RigidbodyState b, float positionLerp, float rotationLerp) {
        return new RigidbodyState {
            Position = Vector3.Lerp(a.Position, b.Position, positionLerp),
            Velocity = Vector3.Lerp(a.Velocity, b.Velocity, positionLerp),
            Acceleration = Vector3.Lerp(a.Acceleration, b.Acceleration, positionLerp),

            Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, rotationLerp),
            AngularVelocity = Vector3.Lerp(a.AngularVelocity, b.AngularVelocity, rotationLerp),
            AngularAcceleration = Vector3.Lerp(a.AngularAcceleration, b.AngularAcceleration, rotationLerp)
        };
    }

    public static RigidbodyState ToImmutable(this Rigidbody body) {
        return new RigidbodyState {
            Position = body.position,
            Velocity = body.velocity,
            Acceleration = Vector3.zero,
            Rotation = body.rotation,
            AngularVelocity = body.angularVelocity,
            AngularAcceleration = Vector3.zero
        };
    }

    public static RigidbodyState ToImmutable(this Rigidbody body, Vector3 linearAcceleration, Vector3 angularAcceleration) {
        return new RigidbodyState {
            Position = body.position,
            Velocity = body.velocity,
            Acceleration = linearAcceleration,
            Rotation = body.rotation,
            AngularVelocity = body.angularVelocity,
            AngularAcceleration = angularAcceleration
        };
    }

    public static void ApplyTo(this RigidbodyState state, Rigidbody body) {
        body.position = state.Position;
        body.velocity = state.Velocity;
        body.rotation = state.Rotation;
        body.angularVelocity = state.AngularVelocity;
    }

    public static void Serialize(this RigidbodyState body, NetBuffer writer) {
        writer.Write(body.Position);
        writer.Write(body.Velocity);
        writer.Write(body.Acceleration);
        writer.WriteRotation(body.Rotation);
        writer.Write(body.AngularVelocity);
        writer.Write(body.AngularAcceleration);
    }

    public static RigidbodyState Deserialize(NetBuffer reader) {
        return new RigidbodyState {
            Position = reader.ReadVector3(),
            Velocity = reader.ReadVector3(),
            Acceleration = reader.ReadVector3(),
            Rotation = reader.ReadRotation(),
            AngularVelocity = reader.ReadVector3(),
            AngularAcceleration = reader.ReadVector3()
        };
    }
}

