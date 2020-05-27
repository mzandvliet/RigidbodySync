using System;
using RamjetAnvil.Unity.Utility;
using UnityEngine;

public abstract class AbstractUnityControllableClock : AbstractUnityClock, IControllableClock {
    public abstract double TimeScale { get; set; }
}