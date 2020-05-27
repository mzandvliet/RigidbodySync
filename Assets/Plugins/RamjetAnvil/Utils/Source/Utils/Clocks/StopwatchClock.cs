using System;
using System.Diagnostics;
using RamjetAnvil.Unity.Utility;
using UnityEngine;

public class StopwatchClock : AbstractUnityControllableClock {

    [SerializeField] private double _timeScale = 1.0;
    [SerializeField] private double _targetInterpolationSpeed = 1.0;

    private Stopwatch _stopwatch;

    private long _lastFrameTimeInTicks;
    private long _deltaTimeInTicks;
    private long _scaledDeltaTimeInTicks;
    private long _frameCount;
    private long _elapsedTimeInTicks;
    private long _timeOffsetInTicks;
    private long _timeOffsetTargetInTicks;

    void Awake() {
        _stopwatch = new Stopwatch();
        _stopwatch.Start();

        UnityEngine.Debug.Log("Stopwatch clock, high resolution: " + Stopwatch.IsHighResolution);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playmodeStateChanged += () => {
            if (UnityEditor.EditorApplication.isPaused) {
                _stopwatch.Stop();
            }
            else {
                _stopwatch.Start();
            }
        };
#endif
    }

    void Update() {
        var elapsedTicks = _stopwatch.ElapsedTicks;
        _deltaTimeInTicks = elapsedTicks - _lastFrameTimeInTicks;
        _lastFrameTimeInTicks = elapsedTicks;

        _timeOffsetInTicks = (long)Mathd.Lerp(_timeOffsetInTicks, _timeOffsetTargetInTicks, _deltaTimeInTicks * _targetInterpolationSpeed); // Todo: is this precise enough?

        _scaledDeltaTimeInTicks = (long) (_deltaTimeInTicks * _timeScale);
        _elapsedTimeInTicks += _scaledDeltaTimeInTicks;

        // TODO Is this really neccesary?
        //        if (_stopwatch.IsRunning && Math.Abs(_timeScale - 1.0) > double.Epsilon) {
        //            var additionalDeltaTime = (long) (_deltaTimeInTicks * _timeScale) - _deltaTimeInTicks;
        //            _elapsedTimeInTicks += additionalDeltaTime;
        //        }
        _frameCount++;
    }

    void OnEnable() {
        _stopwatch.Start();
    }

    void OnDisable() {
        _stopwatch.Stop();
    }

    void OnApplicationPause(bool isPaused) {
        if (isPaused) {
            _stopwatch.Stop();
        }
        else {
            _stopwatch.Start();
        }
    }

    void OnDestroy() {
        _stopwatch.Stop();
    }

    public override float DeltaTime {
        get { return (float) (_scaledDeltaTimeInTicks / (double)TimeSpan.TicksPerSecond); }
    }

    public override double CurrentTime {
        get {
            var totalElapsedTicks = _elapsedTimeInTicks + _timeOffsetInTicks;
            return totalElapsedTicks / (double)TimeSpan.TicksPerSecond; }
    }

    public override long FrameCount {
        get { return _frameCount; }
    }

    public double TargetTimeInterpolationSpeed {
        get { return _targetInterpolationSpeed; }
        set { _targetInterpolationSpeed = value; }
    }

    public void SetCurrentTime(double time) {
        var ticks = (long) (time * TimeSpan.TicksPerSecond);
        _timeOffsetInTicks = ticks - _elapsedTimeInTicks;
        _timeOffsetTargetInTicks = _timeOffsetInTicks;
    }

    public void SetCurrentTimeInterpolated(double time) {
        var ticks = (long)(time * TimeSpan.TicksPerSecond);
        _timeOffsetTargetInTicks = ticks - _elapsedTimeInTicks;
    }

    public override double TimeScale {
        get { return _timeScale; }
        set {
            _timeScale = value;
//            var isCloseToZero = Math.Abs(_timeScale) < double.Epsilon;
//            if (isCloseToZero) {
//                _stopwatch.Stop();
//            } else {
//                if (!_stopwatch.IsRunning) {
//                    _stopwatch.Start();    
//                }
//                _timeScale = value;
//            }
        }
    }
}
