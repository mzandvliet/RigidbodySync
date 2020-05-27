using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RamjetAnvil.RamNet {

    public struct ConnectionId : IEquatable<ConnectionId> {
        public static readonly ConnectionId NoConnection = new ConnectionId(-1);

        public readonly int Value;

        public ConnectionId(int value) {
            Value = value;
        }

        public bool Equals(ConnectionId other) {
            return Value == other.Value;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ConnectionId && Equals((ConnectionId) obj);
        }

        public override int GetHashCode() {
            return Value;
        }

        public static bool operator ==(ConnectionId left, ConnectionId right) {
            return left.Equals(right);
        }

        public static bool operator !=(ConnectionId left, ConnectionId right) {
            return !left.Equals(right);
        }

        public override string ToString() {
            return string.Format("ConnectionId({0})", Value);
        }
    }
}
