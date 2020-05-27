using System;
using UnityEngine;
using UnityEngine.Networking;

namespace RamjetAnvil.Networking {

    public static class Compression {



        public static class Rotation {
            private const float Minimum = -1.0f / 1.414214f; // note: 1.0f / sqrt(2)
            private const float Maximum = +1.0f / 1.414214f;

            public static uint Compress(Quaternion rotation) {
                float absX = Mathf.Abs(rotation.x),
                    absY = Mathf.Abs(rotation.y),
                    absZ = Mathf.Abs(rotation.z),
                    absW = Mathf.Abs(rotation.w);

                var largestComponent = new LargestComponent(ComponentType.X, absX);
                if (absY > largestComponent.Value) {
                    largestComponent.Value = absY;
                    largestComponent.ComponentType = ComponentType.Y;
                }
                if (absZ > largestComponent.Value) {
                    largestComponent.Value = absZ;
                    largestComponent.ComponentType = ComponentType.Z;
                }
                if (absW > largestComponent.Value) {
                    largestComponent.Value = absW;
                    largestComponent.ComponentType = ComponentType.W;
                }

                float a, b, c;
                switch (largestComponent.ComponentType) {
                    case ComponentType.X:
                        if (rotation.x >= 0) {
                            a = rotation.y;
                            b = rotation.z;
                            c = rotation.w;
                        } else {
                            a = -rotation.y;
                            b = -rotation.z;
                            c = -rotation.w;
                        }
                        break;
                    case ComponentType.Y:
                        if (rotation.y >= 0) {
                            a = rotation.x;
                            b = rotation.z;
                            c = rotation.w;
                        } else {
                            a = -rotation.x;
                            b = -rotation.z;
                            c = -rotation.w;
                        }
                        break;
                    case ComponentType.Z:
                        if (rotation.z >= 0) {
                            a = rotation.x;
                            b = rotation.y;
                            c = rotation.w;
                        } else {
                            a = -rotation.x;
                            b = -rotation.y;
                            c = -rotation.w;
                        }
                        break;
                    case ComponentType.W:
                        if (rotation.w >= 0) {
                            a = rotation.x;
                            b = rotation.y;
                            c = rotation.z;
                        } else {
                            a = -rotation.x;
                            b = -rotation.y;
                            c = -rotation.z;
                        }
                        break;
                    default:
                        // Should never happen!
                        throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                              largestComponent.ComponentType);
                }

                float normalizedA = (a - Minimum) / (Maximum - Minimum),
                    normalizedB = (b - Minimum) / (Maximum - Minimum),
                    normalizedC = (c - Minimum) / (Maximum - Minimum);

                uint integerA = (uint) Mathf.FloorToInt(normalizedA * 1024.0f + 0.5f),
                    integerB = (uint) Mathf.FloorToInt(normalizedB * 1024.0f + 0.5f),
                    integerC = (uint) Mathf.FloorToInt(normalizedC * 1024.0f + 0.5f);

                return (((uint) largestComponent.ComponentType) << 30) | (integerA << 20) | (integerB << 10) | integerC;
            }

            public static Quaternion Decompress(uint compressedRotation) {
                var largestComponentType = (ComponentType) (compressedRotation >> 30);
                uint integerA = (compressedRotation >> 20) & ((1 << 10) - 1),
                    integerB = (compressedRotation >> 10) & ((1 << 10) - 1),
                    integerC = compressedRotation & ((1 << 10) - 1);

                float a = integerA / 1024.0f * (Maximum - Minimum) + Minimum,
                    b = integerB / 1024.0f * (Maximum - Minimum) + Minimum,
                    c = integerC / 1024.0f * (Maximum - Minimum) + Minimum;

                Quaternion rotation;
                switch (largestComponentType) {
                    case ComponentType.X:
                        // (?) y z w
                        rotation.y = a;
                        rotation.z = b;
                        rotation.w = c;
                        rotation.x = Mathf.Sqrt(1 - rotation.y * rotation.y
                                                   - rotation.z * rotation.z
                                                   - rotation.w * rotation.w);
                        break;
                    case ComponentType.Y:
                        // x (?) z w
                        rotation.x = a;
                        rotation.z = b;
                        rotation.w = c;
                        rotation.y = Mathf.Sqrt(1 - rotation.x * rotation.x
                                                   - rotation.z * rotation.z
                                                   - rotation.w * rotation.w);
                        break;
                    case ComponentType.Z:
                        // x y (?) w
                        rotation.x = a;
                        rotation.y = b;
                        rotation.w = c;
                        rotation.z = Mathf.Sqrt(1 - rotation.x * rotation.x
                                                   - rotation.y * rotation.y
                                                   - rotation.w * rotation.w);
                        break;
                    case ComponentType.W:
                        // x y z (?)
                        rotation.x = a;
                        rotation.y = b;
                        rotation.z = c;
                        rotation.w = Mathf.Sqrt(1 - rotation.x * rotation.x
                                                   - rotation.y * rotation.y
                                                   - rotation.z * rotation.z);
                        break;
                    default:
                        // Should never happen!
                        throw new ArgumentOutOfRangeException("Unknown rotation component type: " +
                                                              largestComponentType);
                }

                return rotation;
            }

            private enum ComponentType : uint {
                X = 0,
                Y = 1,
                Z = 2,
                W = 3
            }

            private struct LargestComponent {
                public ComponentType ComponentType;
                public float Value;

                public LargestComponent(ComponentType componentType, float value) {
                    ComponentType = componentType;
                    Value = value;
                }
            }
        }
    }
}