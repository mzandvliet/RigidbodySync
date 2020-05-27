using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.DependencyInjection;
using RamjetAnvil.RamNet;
using RamjetAnvil.RigidbodyParty;
using UnityEngine;

public class RaceCommon : MonoBehaviour {

    [SerializeField] private RaceCheckpoint[] _checkpoints;
    [Dependency] private IReplicatedObjectDatabase _database;

    private IList<ReplicatedObject> _racers;
    private ReplicatedObject _ownRacer;

    private float _winTime;
    private ObjectId? _winner;

    void Awake() {
        _racers = new List<ReplicatedObject>();
    }
        
    [MessageHandler(ObjectRole.Authority)]
    void RaceStarted(RaceMessage.RaceStarted message) {
        ResetState();
        _database.FindObjects(ObjectTypes.Player, _racers);
        _ownRacer = _database.FindObject(ObjectTypes.Player, ObjectRole.Owner);

        // Position all racers
        for (int i = 0; i < message.RacerPositions.Count; i++) {
            var racerPosition = message.RacerPositions[i];
            var racer = _database.Find(racerPosition.RacerId);
            racer.GameObject.transform.position = racerPosition.Position;
            racer.GameObject.transform.rotation = racerPosition.Rotation;
        }

        // Highlight first ring
        HighlightCheckpoint(0);
    }

    private GUIStyle _raceNotificationStyle = new GUIStyle();
    void OnGUI() {
        if (_ownRacer != null && _winner.HasValue) {
            string raceOverStr;
            if (_ownRacer.Id == _winner.Value) {
                raceOverStr = "You win!";
            } else {
                raceOverStr = "You lose!";
            }

            if (Time.time < _winTime + 3f) {
                _raceNotificationStyle.fontSize = 50;
                GUI.color = Color.white;
                GUI.Label(new Rect((Screen.width / 2) - 100, (Screen.height / 2) - 50, 200, 100), raceOverStr, _raceNotificationStyle);
            }
        }
    }

    [MessageHandler(ObjectRole.Authority)]
    void HandleCheckpointHit(RaceMessage.CheckpointHit message) {
        Debug.Log("Checkpoint " + message.CheckpointId + " was hit by player(" + message.RacerId + ")");
        if (_ownRacer != null && _ownRacer.Id == message.RacerId) {
            HighlightCheckpoint(message.CheckpointId + 1);    
        }
    }

    [MessageHandler(ObjectRole.Authority)]
    void HandleFinishRace(RaceMessage.RaceFinished message) {
        Debug.Log("Race was won by " + message.Winner);
        ResetState();
        _winTime = Time.time;
        _winner = message.Winner;
    }
    
    private void HighlightCheckpoint(uint checkpointId) {
        for (int i = 0; i < _checkpoints.Length; i++) {
            var checkpoint = _checkpoints[i];
            if (i == checkpointId) {
                checkpoint.Highlight();
            } else {
                checkpoint.Unhighlight();
            }
        }
    }

    private void ResetState() {
        for (int i = 0; i < _checkpoints.Length; i++) {
            var checkpoint = _checkpoints[i];
            checkpoint.Unhighlight();
        }
        _racers.Clear();
        _winTime = 0f;
        _winner = null;
    }

    public RaceCheckpoint[] Checkpoints {
        get { return _checkpoints; }
    }
}
