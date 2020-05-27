using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using UnityEngine;

public class PlayerInitialState : MonoBehaviour {

    [SerializeField] private Color _color = Color.white;
    [SerializeField] private Renderer _wingsRenderer;
    [SerializeField] private TrailRenderer _trailRenderer;

    [InitialState]
    private void GetInitialColor(PlayerMessage.SetColor message) {
        message.Color = _color;
    }

    [MessageHandler(ObjectRole.Authority)]
    private void HandleSetColor(PlayerMessage.SetColor message) {
        _wingsRenderer.material.color = message.Color;
        _trailRenderer.material.SetColor("_TintColor", message.Color);
    }

    public Color Color {
        set { _color = value; }
    }
}
