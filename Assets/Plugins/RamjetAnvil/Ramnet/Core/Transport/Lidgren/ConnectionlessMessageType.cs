using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RamjetAnvil.RamNet {
    public enum ConnectionlessMessageType : byte {
        RequestNatPunchConfirmation = 0,
        NatPunchConfirmed = 1
    }
}
