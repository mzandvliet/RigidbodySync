using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(GameObjectNetworkId))]
public class GameObjectNetworkIdEditor : Editor {

    public override void OnInspectorGUI() {
        var instance = target as GameObjectNetworkId;
        if (instance != null && instance.Id != null) {
            EditorGUILayout.LabelField("Id", instance.Id.ToString());    
        }
    }
}