using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using UnityEngine;

[NetworkRole(ObjectRole.Authority)]
public class RaceAuthority : MonoBehaviour {

    [SerializeField] private Transform _startTransform;
    [SerializeField] private RaceCommon _common;

    [Dependency] private IReplicatedObjectDatabase _objectDatabase;
    [Dependency] private INetworkMessagePool<RaceMessage.RaceFinished> _raceFinishedMessages;
    [Dependency] private INetworkMessagePool<RaceMessage.RaceStarted> _raceStartedMessages;
    [Dependency] private INetworkMessagePool<RaceMessage.CheckpointHit> _checkpointHitMessages;
    [Dependency] private IObjectMessageSender _messageSender;

    // Keep track of state, which racer is at which checkpoint
    private bool _isRaceStarted;
    private IDictionary<ObjectId, RacerState> _racerStates; 

    private IList<ReplicatedObject> _racers;

    void Awake() {
        _racers = new List<ReplicatedObject>();
        _racerStates = new Dictionary<ObjectId, RacerState>();
    }

    void Start() {
        // Loop over checkpoints
        var checkpoints = _common.Checkpoints;
        for (int i = 0; i < checkpoints.Length; i++) {
            var checkpoint = checkpoints[i];
            var checkpointId = i;
            checkpoint.OnPlayerHit += collider => {
                var networkInfo = collider.gameObject.GetComponent<GameObjectNetworkInfo>();
                OnCheckpointHit(networkInfo.ObjectId, checkpointId);   
            };
        }
    }

    [MessageHandler(ObjectRoles.NonAuthoritive)]
    void RaceRequested(RaceMessage.RequestRace _) {
        ResetState();

        _racers.Clear();
        _objectDatabase.FindObjects(ObjectTypes.Player, _racers);

        // Reset state, put all planes on the start position
        var raceStarted = _raceStartedMessages.Create();
        raceStarted.Content.RacerPositions.Clear();
        for (int i = 0; i < _racers.Count; i++) {
            var racer = _racers[i];
            raceStarted.Content.RacerPositions.Add(new RaceMessage.RacerPosition {
                Position = _startTransform.position + new Vector3(6, 0, 0) * i,
                Rotation = _startTransform.rotation,
                RacerId = racer.Id
            });
            _racerStates.Add(racer.Id, new RacerState {
                CurrentCheckpoint = -1
            });
        }
        _messageSender.Send(raceStarted, ObjectRoles.Everyone);

        _isRaceStarted = true;
    }

    void OnCheckpointHit(ObjectId racerId, int checkpointId) {
        if (_isRaceStarted) {
            var racerState = _racerStates[racerId];
            if (racerState.CurrentCheckpoint + 1 == checkpointId && _isRaceStarted) {
                racerState.CurrentCheckpoint = checkpointId;
                _racerStates[racerId] = racerState;

                var hitMessage = _checkpointHitMessages.Create();
                hitMessage.Content.RacerId = racerId;
                hitMessage.Content.CheckpointId = (uint) checkpointId;
                _messageSender.Send(hitMessage, ObjectRoles.Everyone);

                var isRaceFinished = checkpointId == _common.Checkpoints.Length - 1;
                if (isRaceFinished) {
                    var finishMessage = _raceFinishedMessages.Create();
                    finishMessage.Content.Winner = racerId;
                    _messageSender.Send(finishMessage, ObjectRoles.Everyone);
                    ResetState();
                }
            }
        }
    }

    void ResetState() {
        _isRaceStarted = false;
        _racerStates.Clear();
    }

    private struct RacerState {
        public int CurrentCheckpoint;
    }

}
