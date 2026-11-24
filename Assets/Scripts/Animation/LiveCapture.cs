using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Headless frame capture of a running scene, started only by command-line arguments:
//   Slime.exe -batchmode -capture C:/out [-scene Name] [-seconds 24] [-fps 30] [-size 1920x1080]
//             [-orbit yaw,yawPerSecond,pitch,radius,fov] [-seed 3] [-fixedStep] [-speed 1] [-keyframe N]
//             [-sim field=value;...] [-look field=value;...] [-warmup seconds]
// Default is a live capture: the scene runs in real time and frames are read back at the capture
// rate, stamped with wall-clock time, followed by a frame-time report and an uncapped benchmark.
// With -fixedStep every frame advances exactly 1/fps of game time (times -speed) and is saved as
// PNG, for masters whose timing must not depend on machine load; -warmup runs the sim that
// long (in capture seconds) before the first saved frame. -keyframe shows the look of the
// scene sequencer's Nth keyframe instead of playing the sequence. -sim and -look override fields of
// copies of the simulation settings and the display strategy (e.g. -look "colorLow=0,0.2,0.3,1"),
// so a look can be tried without touching the project's assets. The player quits when done.
[DefaultExecutionOrder(1000)]
public class LiveCapture : MonoBehaviour
{
    Simulation simulation;
    OrbitCamera orbitCamera;

    string outDir;
    float seconds = 24, fps = 30, speed = 1, benchSeconds = 6;
    bool fixedStep, orbit;
    Vector2Int size = new Vector2Int(1920, 1080);
    float yaw, yawSpeed, pitch, radius, fov;

    RenderTexture frame;
    float startTime, nextCapture;
    int captured, frameLimit;
    int steps, warmupFrames;
    readonly SortedDictionary<int, int> stepHistogram = new SortedDictionary<int, int>();
    readonly List<float> frameTimes = new List<float>();
    readonly List<float> stamps = new List<float>();
    readonly List<Task> writes = new List<Task>();
    bool benchmarking;
    int benchFrames;
    Stopwatch bench;

    static string Arg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, name);
        return i < 0 ? null : i + 1 < args.Length ? args[i + 1] : "";
    }
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Arg("-capture") == null) return;
        string scene = Arg("-scene");
        if (scene != null && SceneManager.GetActiveScene().name != scene)
        {
            SceneManager.sceneLoaded += (s, m) => Attach();
            SceneManager.LoadScene(scene);
            return;
        }
        Attach();
    }

    static void Attach()
    {
        if (FindObjectOfType<LiveCapture>() != null) return;
        string seed = Arg("-seed");
        if (seed != null) Random.InitState(int.Parse(seed)); // before Simulation.Start spawns agents
        new GameObject("LiveCapture").AddComponent<LiveCapture>();
    }

    void Awake()
    {
        outDir = Arg("-capture");
        if (Arg("-seconds") != null) seconds = F(Arg("-seconds"));
        if (Arg("-fps") != null) fps = F(Arg("-fps"));
        if (Arg("-speed") != null) speed = F(Arg("-speed"));
        if (Arg("-size") != null) { var s = Arg("-size").Split('x'); size = new Vector2Int(int.Parse(s[0]), int.Parse(s[1])); }
        fixedStep = Arg("-fixedStep") != null;
        if (Arg("-warmup") != null) warmupFrames = Mathf.RoundToInt(F(Arg("-warmup")) * fps); // fixed-step frames run before recording
        if (Arg("-orbit") != null)
        {
            var o = Arg("-orbit").Split(',');
            orbit = true;
            yaw = F(o[0]); yawSpeed = F(o[1]); pitch = F(o[2]); radius = F(o[3]); fov = F(o[4]);
        }

        simulation = FindObjectOfType<Simulation>();
        orbitCamera = FindObjectOfType<OrbitCamera>();
        foreach (var sequencer in FindObjectsOfType<CinematicSequencer>())
        {
            sequencer.enabled = false;
            if (Arg("-keyframe") != null) simulation.displayStrategy = sequencer.timeline[int.Parse(Arg("-keyframe"))].displaySettings;
        }
        if (Arg("-sim") != null) simulation.settings = Override(simulation.settings, Arg("-sim"));
        if (Arg("-look") != null) simulation.displayStrategy = Override(simulation.displayStrategy, Arg("-look"));

        Directory.CreateDirectory(outDir);
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        if (fixedStep)
        {
            Time.captureFramerate = Mathf.RoundToInt(fps);
            Time.timeScale = speed;
            // A whole number of equal simulation steps per frame, close to the project's step:
            // a frame that alternates between 2 and 3 steps visibly judders.
            int stepsPerFrame = Mathf.Max(1, Mathf.RoundToInt(speed / fps / Time.fixedDeltaTime));
            Time.fixedDeltaTime = speed / fps / stepsPerFrame;
            Time.maximumDeltaTime = 2 * speed / fps; // never drop simulation steps, however coarse the capture
            Application.targetFrameRate = -1;
        }
        else Application.targetFrameRate = 60;
        frameLimit = Mathf.RoundToInt(seconds * fps);

        simulation.useFixedResolution = true;
        simulation.fixedResolution = size;
        simulation.camera.aspect = (float)size.x / size.y;
        if (orbit) { simulation.camera.fieldOfView = fov; orbitCamera.Disable(); }
        frame = new RenderTexture(size.x, size.y, 0, GraphicsFormat.R8G8B8A8_SRGB);
    }

    // A copy of the asset with "a=1;b.c=0,0.5,1,1" applied: ints, floats, enums, colours and vectors,
    // with dots reaching into nested serializable fields.
    static T Override<T>(T asset, string assignments) where T : ScriptableObject
    {
        T copy = Instantiate(asset);
        foreach (string assignment in assignments.Split(';'))
        {
            if (assignment.Trim() == "") continue;
            string[] kv = assignment.Split('=');
            string[] path = kv[0].Trim().Split('.');
            object target = copy;
            var stack = new List<(object, System.Reflection.FieldInfo)>();
            for (int i = 0; i < path.Length; i++)
            {
                var field = target.GetType().GetField(path[i]) ?? throw new System.ArgumentException($"no field {kv[0]} on {asset.name}");
                stack.Add((target, field));
                if (i < path.Length - 1) target = field.GetValue(target);
            }
            var (owner, leaf) = stack[stack.Count - 1];
            leaf.SetValue(owner, Parse(leaf.FieldType, kv[1].Trim()));
            for (int i = stack.Count - 2; i >= 0; i--) stack[i].Item2.SetValue(stack[i].Item1, stack[i + 1].Item1); // write back structs
        }
        return copy;
    }

    static object Parse(System.Type type, string value)
    {
        if (type == typeof(int)) return int.Parse(value);
        if (type == typeof(float)) return F(value);
        if (type.IsEnum) return System.Enum.Parse(type, value);
        float[] v = System.Array.ConvertAll(value.Split(','), F);
        if (type == typeof(Color)) return new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1);
        if (type == typeof(Vector3)) return new Vector3(v[0], v[1], v[2]);
        if (type == typeof(Vector4)) return new Vector4(v[0], v[1], v[2], v[3]);
        throw new System.ArgumentException($"cannot parse {type.Name}");
    }

    void Start() => startTime = nextCapture = Time.realtimeSinceStartup;

    void Update()
    {
        if (!orbit) return;
        float t = fixedStep ? Mathf.Max(0, captured - warmupFrames) / fps : Time.realtimeSinceStartup - startTime;
        orbitCamera.yaw = yaw + yawSpeed * t;
        orbitCamera.pitch = pitch;
        orbitCamera.radius = radius;
    }

    void FixedUpdate() => steps++;

    void LateUpdate()
    {
        if (benchmarking) { Benchmark(); return; }
        float now = Time.realtimeSinceStartup;
        if (fixedStep ? captured >= warmupFrames + frameLimit : now - startTime > seconds) { Finish(); return; }

        if (!fixedStep)
        {
            frameTimes.Add(Time.unscaledDeltaTime);
            if (now < nextCapture) return;
            nextCapture += 1 / fps;
            if (nextCapture < now) nextCapture = now; // fell behind: keep wall-clock spacing honest
            stamps.Add(now - startTime);
        }
        else
        {
            if (captured > 0) stepHistogram[steps] = stepHistogram.TryGetValue(steps, out int c) ? c + 1 : 1;
            steps = 0;
            if (captured < warmupFrames) { captured++; return; }
        }
        Save(captured++ - warmupFrames);
    }

    void Save(int index)
    {
        Graphics.Blit(simulation.DisplayTexture, frame);
        int w = size.x, h = size.y;
        bool png = fixedStep;
        AsyncGPUReadback.Request(frame, 0, TextureFormat.RGBA32, request =>
        {
            if (request.hasError) { UnityEngine.Debug.LogError($"[capture] readback failed for frame {index}"); return; }
            byte[] pixels = request.GetData<byte>().ToArray();
            string path = Path.Combine(outDir, $"frame_{index:D5}.{(png ? "png" : "jpg")}");
            writes.Add(Task.Run(() => File.WriteAllBytes(path, png
                ? ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h)
                : ImageConversion.EncodeArrayToJPG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h, 0, 95))));
        });
    }

    void Finish()
    {
        AsyncGPUReadback.WaitAllRequests();
        Task.WaitAll(writes.ToArray());
        string timing = Path.Combine(outDir, "timing.txt");
        if (fixedStep)
        {
            var histogram = new List<string>();
            foreach (var kv in stepHistogram) histogram.Add($"{kv.Key} steps x{kv.Value}");
            File.WriteAllText(timing, $"fixed step: {captured - warmupFrames} frames (after {warmupFrames} warmup) at {fps} fps, " +
                $"speed {speed}x, {size.x}x{size.y}, sim step {Time.fixedDeltaTime * 1000:F2} ms; frames: {string.Join(", ", histogram)}\n");
            UnityEngine.Debug.Log("[capture] " + File.ReadAllText(timing));
            Application.Quit();
            return;
        }

        var lines = new List<string>();
        for (int i = 0; i < stamps.Count; i++) lines.Add($"{i} {stamps[i]:F4}");
        File.WriteAllLines(Path.Combine(outDir, "stamps.txt"), lines);

        frameTimes.RemoveAt(0);
        frameTimes.Sort();
        float mean = 0; foreach (float f in frameTimes) mean += f; mean /= frameTimes.Count;
        File.WriteAllText(timing,
            $"captured {captured} frames over {seconds}s at {size.x}x{size.y}, target 60 fps\n" +
            $"frames rendered {frameTimes.Count + 1}, mean {mean * 1000:F2} ms ({1 / mean:F1} fps), " +
            $"p95 {frameTimes[(int)(frameTimes.Count * 0.95f)] * 1000:F2} ms, worst {frameTimes[frameTimes.Count - 1] * 1000:F2} ms\n");
        UnityEngine.Debug.Log("[capture] " + File.ReadAllText(timing));

        // Uncapped benchmark of the same scene, no capture overhead.
        Application.targetFrameRate = -1;
        benchmarking = true;
        bench = Stopwatch.StartNew();
    }

    void Benchmark()
    {
        if (benchFrames == 0)
        {
            if (bench.Elapsed.TotalSeconds < 1) return; // let the frame rate settle
            bench.Restart();
        }
        if (++benchFrames < 2 || bench.Elapsed.TotalSeconds < benchSeconds) return;
        string line = $"uncapped: {benchFrames} frames in {bench.Elapsed.TotalSeconds:F2}s = {benchFrames / bench.Elapsed.TotalSeconds:F1} fps\n";
        File.AppendAllText(Path.Combine(outDir, "timing.txt"), line);
        UnityEngine.Debug.Log("[capture] " + line);
        Application.Quit();
    }
}
