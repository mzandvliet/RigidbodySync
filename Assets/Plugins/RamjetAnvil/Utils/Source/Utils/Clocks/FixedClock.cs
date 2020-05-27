using UnityEngine;

public class FixedClock : AbstractUnityControllableClock {
    private long _frameCount;
    private double _currentTime;
    private bool _isRunning;

    void Awake() {
        _frameCount = 0;
    }

    void FixedUpdate() {
        if (!_isRunning) {
            return;
        }

        _currentTime += Time.fixedDeltaTime;
        _frameCount++;
    }

    void OnApplicationPause(bool isPaused) {
        _isRunning = !isPaused;
    }

    public override float DeltaTime {
        get { return Time.fixedDeltaTime; }
    }

    public override double CurrentTime {
        get { return _currentTime; }
    }

    public override long FrameCount {
        get { return _frameCount; }
    }

    public override double TimeScale {
        get { return Time.timeScale; }
        set { Time.timeScale = (float) value; }
    }


    // Todo: make pretty plz
    public static void PausePhysics() {
        Time.timeScale = 0f;
    }

    public static void ResumePhysics() {
        Time.timeScale = 1f;
    }
}
