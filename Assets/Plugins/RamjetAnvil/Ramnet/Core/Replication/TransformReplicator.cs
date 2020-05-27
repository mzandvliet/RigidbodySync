using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.RamNet;
using UnityEngine;

public class TransformReplicator : MonoBehaviour {

    [InitialState]
    public void ConstructInitialTransform(BasicMessage.SetTransform message) {
        message.Position = transform.position;
        message.Rotation = transform.rotation;
    }

    [MessageHandler(ObjectRole.Authority)]
    private void SetTransform(BasicMessage.SetTransform message) {
        transform.position = message.Position;
        transform.rotation = message.Rotation;
    }
}
