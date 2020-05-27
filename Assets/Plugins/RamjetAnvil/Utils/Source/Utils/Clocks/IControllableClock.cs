using System;

namespace RamjetAnvil.Unity.Utility {
    public interface IControllableClock : IClock {
        double TimeScale { get; set; }
    }

    public static class ClockExtensions {
        public static void Pause(this IControllableClock clock) {
            clock.TimeScale = 0.0;
        }

        public static void Resume(this IControllableClock clock) {
            clock.TimeScale = 1.0;
        }
    }
}