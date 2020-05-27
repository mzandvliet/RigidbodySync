using System;
using System.Collections.Generic;
using RamjetAnvil.RamNet;
using UnityEngine;
using Guid = RamjetAnvil.Util.Guid;

[ExecuteInEditMode]
public class GameObjectNetworkId : MonoBehaviour {

    public static readonly HashSet<Guid> TakenGuids = new HashSet<Guid>();
        
    [SerializeField] public Guid Id;

    void Awake() {
#if UNITY_EDITOR
        if (TakenGuids.Contains(Id) || Id == null || Id.Equals(Guid.Empty)) {
            Debug.Log("Creating new id for " + GetInstanceID());
            Id = Guid.RandomGuid();
        } 
        TakenGuids.Add(Id);
#endif

        if (Id == null || Id.Equals(Guid.Empty)) {
            Debug.LogError("Failed to initialize network id for object " + gameObject.name);
        }
    }

#if UNITY_EDITOR
    void OnDestroy() {
        TakenGuids.Remove(Id);
    }
#endif
}
