using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.Unity.Utility;
using UnityEngine;

public class UpdateSource : MonoBehaviour {

    [Dependency("clock"), SerializeField] private AbstractUnityClock _updateClock;
    [Dependency("fixedClock"), SerializeField] private AbstractUnityClock _fixedUpdateClock;

    [SerializeField] private MonoBehaviour[] _updateables;

    void Update() {
        for (int i = 0; i < _updateables.Length; i++) {
            var updateable = _updateables[i] as IUpdate;
            if (updateable != null && _updateables[i] != null && _updateables[i].enabled) {
                updateable.OnUpdate(_updateClock);
            }
        }
    }

    void FixedUpdate() {
        for (int i = 0; i < _updateables.Length; i++) {
            var updateable = _updateables[i] as IFixedUpdate;
            if (updateable != null && _updateables[i] != null && _updateables[i].enabled) {
                updateable.OnFixedUpdate(_fixedUpdateClock);
            }
        }
    }

    void LateUpdate() {
        for (int i = 0; i < _updateables.Length; i++) {
            var updateable = _updateables[i] as ILateUpdate;
            if (updateable != null && _updateables[i] != null && _updateables[i].enabled) {
                updateable.OnLateUpdate(_updateClock);
            }
        }
    }

}
