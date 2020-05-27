using UnityEngine;
using System.Collections;

public static class LayerUtil {
    public static void SetLayerRecursively(Transform transform, int layer) {
        transform.gameObject.layer = layer;
        for (int i = 0; i < transform.childCount; i++) {
            SetLayerRecursively(transform.GetChild(i), layer);
        }
    }
}
