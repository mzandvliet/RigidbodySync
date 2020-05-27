using RamjetAnvil.Coroutine;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using RamjetAnvil.Unity.Utility;
using UnityEngine;

// TODO Fix server disconnect events

[RequireComponent(typeof(PlayerSimulation))]
[NetworkRole(ObjectRole.Authority)]
public class PlayerAuthority : MonoBehaviour, IUpdate, IFixedUpdate {

    [Dependency("fixedClock"), SerializeField] private AbstractUnityClock _fixedClock;
    [SerializeField] private float _sendRate = 30f;
    [Dependency] private IObjectMessageSender _messageSender;
    [Dependency] private INetworkMessagePool<PlayerMessage.InputUpdate> _inputMessages; 
    [Dependency] private INetworkMessagePool<PlayerMessage.CollisionEvent> _collisionEventMessages;
    [Dependency] private INetworkMessagePool<PlayerMessage.StateUpdate> _stateUpdateMessages;

    private PlayerSimulation _simulation;

    private CoroutineScheduler _coroutineScheduler;

    void Awake() {
        _simulation = GetComponent<PlayerSimulation>();
        _coroutineScheduler = new CoroutineScheduler(initialCapacity: 1, growthStep: 1);
        _coroutineScheduler.Run(CoroutineUtils.OnSendRate(_sendRate, this, SendMessage));
        //_coroutineScheduler.Run(CoroutineUtils.EveryFrame(this, SendMessage));
    }

    public void OnFixedUpdate(IClock clock) {
        _simulation.SimulateUnity(clock.DeltaTime);
    }

    public void OnUpdate(IClock clock) {
        _coroutineScheduler.Update(clock.FrameCount, clock.CurrentTime); // Bug:
    }

    private void SendMessage() {
        var message = _stateUpdateMessages.Create();
        var bodyState = _simulation.UnityBody.State;
        message.Content.State = bodyState;
        _messageSender.Send(message, ObjectRoles.Everyone);
    }

    private void OnCollisionEnter(Collision _) {
        var message = _collisionEventMessages.Create();
        _messageSender.Send(message, ObjectRoles.Everyone);
    }

    [MessageHandler(ObjectRole.Owner)]
    private void OnInputReceived(PlayerMessage.InputUpdate message) {
        _simulation.SetInput(message.Input);

        // Route the input to other players
        var peerMessage = _inputMessages.Create();
        peerMessage.Content.Input = message.Input;
        _messageSender.Send(peerMessage, ObjectRole.Others);
    }
}
