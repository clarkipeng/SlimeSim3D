using UnityEngine;
using System.Collections;
using System.Diagnostics;

public class BenchmarkRunner : MonoBehaviour
{
    [Header("References")]
    public Simulation simulation;
    public OrbitCamera orbitCamera;
    public SlimeSettings benchmarkSettings;

    [Header("Config")]
    public KeyCode triggerKey = KeyCode.B;
    public int warmupFrames = 5;
    public int testFrames = 600;

    [Header("Orbit Animation")]
    public float startAngle = 0f;
    public float endAngle = 360f;
    public float startPitch = 0.0f;
    public float startRadius = 10f;
    public float endRadius = 1f;

    private float oldYaw, oldPitch, oldRadius;
    private SlimeSettings originalSettings;

    void Start()
    {
        if (simulation == null)
        {
            UnityEngine.Debug.LogError("[Benchmark] No Simulation assigned!");
        }
        if (orbitCamera == null)
        {
            UnityEngine.Debug.LogWarning("[Benchmark] No OrbitCamera assigned, camera animation disabled.");
        }
    }
    void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            StartCoroutine(RunBenchmark());
        }
    }

    IEnumerator RunBenchmark()
    {
        UnityEngine.Debug.Log("Benchmark Starting...");

        originalSettings = simulation.settings;
        oldYaw = orbitCamera.yaw;
        oldPitch = orbitCamera.pitch;
        oldRadius = orbitCamera.radius;

        int oldVSync = QualitySettings.vSyncCount;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;

        simulation.settings = benchmarkSettings;
        simulation.Init();

        if (orbitCamera != null)
        {
            orbitCamera.setYaw(startAngle);
            orbitCamera.setPitch(startPitch);
            orbitCamera.setRadius(startRadius);
        }

        for (int i = 0; i < warmupFrames; i++) yield return new WaitForEndOfFrame();

        Stopwatch sw = new Stopwatch();
        sw.Start();

        for (int i = 0; i < testFrames; i++)
        {
            float t = i / (testFrames - 1.0f);

            if (orbitCamera != null)
            {
                orbitCamera.yaw = Mathf.Lerp(startAngle, endAngle, t);
                orbitCamera.radius = Mathf.Lerp(startRadius, endRadius, t);
            }

            yield return new WaitForEndOfFrame();
        }

        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        UnityEngine.Debug.LogFormat("Benchmark {0} Frames in {1:F2}ms ({2:F0} FPS)", testFrames, totalMs, 1000.0 / (totalMs / testFrames));

        simulation.settings = originalSettings;
        simulation.Init();

        orbitCamera.setYaw(oldYaw);
        orbitCamera.setPitch(oldPitch);
        orbitCamera.setRadius(oldRadius);

        QualitySettings.vSyncCount = oldVSync;
    }
}