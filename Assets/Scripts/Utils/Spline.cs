using UnityEngine;

public static class SplineUtil {
    public static Vector3 Hermite(Vector3 p0, Vector3 t0, Vector3 p1, Vector3 t1, float t) {
        return (2.0f * t * t * t - 3.0f * t * t + 1.0f) * p0
               + (t * t * t - 2.0f * t * t + t) * t0
               + (-2.0f * t * t * t + 3.0f * t * t) * p1
               + (t * t * t - t * t) * t1;
    }

    //    private static Vector3 GetHermiteInternal(int idxFirstPoint, float t) {
    //        float t2 = t * t;
    //        float t3 = t2 * t;
    //
    //        Vector3 P0 = mNodes[idxFirstPoint - 1].Point;
    //        Vector3 P1 = mNodes[idxFirstPoint].Point;
    //        Vector3 P2 = mNodes[idxFirstPoint + 1].Point;
    //        Vector3 P3 = mNodes[idxFirstPoint + 2].Point;
    //
    //        float tension = 0.5f;	// 0.5 equivale a catmull-rom
    //
    //        Vector3 T1 = tension * (P2 - P0);
    //        Vector3 T2 = tension * (P3 - P1);
    //
    //        float Blend1 = 2 * t3 - 3 * t2 + 1;
    //        float Blend2 = -2 * t3 + 3 * t2;
    //        float Blend3 = t3 - 2 * t2 + t;
    //        float Blend4 = t3 - t2;
    //
    //        return Blend1 * P1 + Blend2 * P2 + Blend3 * T1 + Blend4 * T2;
    //    }
}
