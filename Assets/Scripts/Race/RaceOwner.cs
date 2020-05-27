using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using UnityEngine;

[NetworkRole(ObjectRoles.NonAuthoritive)]
public class RaceOwner : MonoBehaviour {

    [Dependency] private INetworkMessagePool<RaceMessage.RequestRace> _requestRaceMessages;
    [Dependency] private IObjectMessageSender _messageSender;

    void Update() {
        if (Input.GetKeyDown(KeyCode.Y)) {
            var requestRace = _requestRaceMessages.Create();
            _messageSender.Send(requestRace, ObjectRole.Authority);
        }
    }
}
