using System;

namespace RamjetAnvil.Unity.Utility {
    public static class Mathd {
        public static double ToMillis(double seconds) {
            return Math.Round(seconds * 1000.0, 2);
        }

        public static double Lerp(double a, double b, double t) {
            return a + (b - a) * Clamp01(t);
        }

        public static double InverseLerp(double a, double b, double value) {
            if (a != b)
                return Clamp01((value - a) / (b - a));
            return 0.0;
        }

        public static double Clamp01(double value) {
            if (value < 0.0)
                return 0.0;
            if (value > 1.0)
                return 1.0;
            return value;
        }
    }
}
