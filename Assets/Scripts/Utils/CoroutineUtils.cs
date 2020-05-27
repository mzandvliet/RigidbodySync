using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.Coroutine;
using UnityEngine;

namespace RamjetAnvil.Unity.Utility {
    public static class CoroutineUtils {

        public static IEnumerator<WaitCommand> OnSendRate(float sendRate, MonoBehaviour c, Action send) {
            yield return WaitCommand.WaitForNextFrame;
            while (c != null) {
                if (c.enabled) {
                    send();
                    yield return WaitCommand.WaitSeconds(1f / sendRate);
                } else {
                    yield return WaitCommand.WaitForNextFrame;
                }
            }
        }

        public static IEnumerator<WaitCommand> EveryFrame(MonoBehaviour c, Action work) {
            yield return WaitCommand.WaitForNextFrame;
            while (c != null) {
                if (c.enabled) {
                    work();
                    yield return WaitCommand.WaitForNextFrame;
                }
            }
        } 
    }
}
