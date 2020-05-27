using RamjetAnvil.Coroutine;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Unity.Utility;
using UnityEngine;
using UnityEngine.Networking;

/* This just polls input and either sends it as Rpc (if non-listener) or directly (if listener).
 */
[NetworkRole(ObjectRole.Owner)]
public class PlayerOwner : MonoBehaviour, IFixedUpdate {

    [Dependency] private IObjectMessageSender _messageSender;
    [Dependency] private INetworkMessagePool<PlayerMessage.InputUpdate> _inputMessages; 

    private int _lastSampledFrame;

    void Awake() {
        _lastSampledFrame = -1;
    }
    
    public void OnFixedUpdate(IClock clock) {
//        Debug.Log("owner fixed update " + clock.FrameCount);
        /* Sample input once per render frame, but do it here instead of in Update to avoid frame lag.
         * Send to local simulation and to server */
        if (Time.renderedFrameCount > _lastSampledFrame) {
            var inputMessage = _inputMessages.Create();
            inputMessage.Content.Input = SampleInput(clock);
            _messageSender.Send(inputMessage, ObjectRole.Authority | ObjectRole.Owner);

            _lastSampledFrame = Time.renderedFrameCount;
        }
    }

    private static PlayerInput SampleInput(IClock clock) {
        return new PlayerInput {
            Timestamp = clock.CurrentTime,
            Thrust = Input.GetAxis("Thrust"),
            Pitch = Input.GetAxis("Pitch"),
            Roll = Input.GetAxis("Roll"),
            Yaw = Input.GetAxis("Yaw"),
            Boost = Input.GetButton("Boost")
        };
    }
}
