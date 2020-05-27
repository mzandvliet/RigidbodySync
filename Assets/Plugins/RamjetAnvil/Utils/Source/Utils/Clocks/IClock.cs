using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RamjetAnvil.Unity.Utility {
    public interface IClock {
        float DeltaTime { get; }
        double CurrentTime { get; }
        long FrameCount { get; }
    }
}
