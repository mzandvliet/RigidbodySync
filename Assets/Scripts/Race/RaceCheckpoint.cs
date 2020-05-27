using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class RaceCheckpoint : MonoBehaviour {

    public event Action<Collider> OnPlayerHit;

    [SerializeField] private Color _highlightColor = Color.white;
    [SerializeField] private Renderer _renderer;

    private Color _initialColor;

    void Awake() {
        _initialColor = _renderer.material.color;
    }

    private void OnTriggerEnter(Collider other) {
        if (other.tag == "Player") {
            if (OnPlayerHit != null) {
                OnPlayerHit(other);
            }
        }
    }

    public void Highlight() {
        _renderer.material.color = _highlightColor;
    }

    public void Unhighlight() {
        _renderer.material.color = _initialColor;
    }
}
