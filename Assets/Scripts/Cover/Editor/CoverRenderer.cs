using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using ComputeShaderUtility;

// Batchmode cover stills: runs the real simulation (same compute + settings as a scene) at a fixed
// timestep, then path-traces the Isosurface look through a thin-lens camera over a dark ground.
// Unity.exe -batchmode -projectPath . -executeMethod CoverRenderer.Run -coverConfig cover.json -logFile -
public static class CoverRenderer
{
    [Serializable] public class View
    {
        public string name;
        public float yaw, pitch, distance = 3, fovY = 25;
        public Vector3 target;
        public float focusOffset;      // focus = distance + focusOffset
        public float aperture = 0.03f; // lens radius in world units (sphere radius is 0.5)
        public Vector4 slicePlane;
        public float roll;
    }

    [Serializable] public class Config
    {
        public string simSettings = "Assets/Settings/Simulation/SimG.asset";
        public string simCompute = "Assets/Scripts/Slime/SlimeSimFor.compute";
        public string isoKeyframe = "Assets/Keyframes/GradientCircle/SceneIsosurface 3.asset";
        public int seed = 1;
        public bool linearVolume = false; // the scene samples the volume with point filtering
        public int[] snapshotFrames = { 1500 };
        public int width = 2400, height = 1600, samples = 64;
        public string outDir = "CoverOut";
        public Vector3 lightDir = new Vector3(-0.5f, 1f, 0.35f);
        public float lightRadius = 0.08f, keyIntensity = 1.2f, selfShadow = 0.6f;
        public float ambOverride = -1;
        public Color groundColor = new Color(0.06f, 0.055f, 0.07f);
        public Color bgColor = new Color(0.012f, 0.01f, 0.018f);
        public float groundGloss = 0.35f, poolRadius = 1.2f, groundGap = 0.01f;
        public float exposure = 1.0f, vignette = 0.6f, tonemap = 1;
        public View[] views;
    }

    public static void Run()
    {
        int code = 0;
        try { Render(LoadConfig()); }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    static Config LoadConfig()
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-coverConfig");
        if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("-coverConfig <file.json> required");
        return JsonUtility.FromJson<Config>(File.ReadAllText(args[i + 1]));
    }

    static void Render(Config cfg)
    {
        var s = AssetDatabase.LoadAssetAtPath<SlimeSettings>(cfg.simSettings);
        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(cfg.simCompute);
        var iso = Array.Find(AssetDatabase.LoadAllAssetsAtPath(cfg.isoKeyframe), o => o is IsosurfaceDisplayStrategy) as IsosurfaceDisplayStrategy;
        if (s == null || sim == null || iso == null) throw new FileNotFoundException($"missing asset: sim={s} compute={sim} iso={iso}");
        var cover = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Scripts/Cover/CoverRender.compute");
        Directory.CreateDirectory(cfg.outDir);
        Debug.Log($"[cover] GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}); sim {s.name} res {s.resolution} agents {s.numAgents}");

        // --- Simulation: mirrors Simulation.Init / FixedUpdate / RunSimulation ---
        RenderTexture a = null, b = null;
        var fmt = GraphicsFormat.R8_UNorm;
        ComputeHelper.CreateRenderTexture(ref a, s.resolution, s.resolution, s.resolution, FilterMode.Point, fmt);
        ComputeHelper.CreateRenderTexture(ref b, s.resolution, s.resolution, s.resolution, FilterMode.Point, fmt);
        int update = sim.FindKernel("Update"), diffuse = sim.FindKernel("Diffuse");

        UnityEngine.Random.InitState(cfg.seed);
        var agents = new Simulation.Agent[s.numAgents];
        Vector3 centre = Vector3.one * (s.resolution / 2);
        for (int i = 0; i < agents.Length; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere, pos = centre;
            switch (s.spawnMode)
            {
                case Simulation.SpawnMode.Random: pos = new Vector3(UnityEngine.Random.Range(0, s.resolution), UnityEngine.Random.Range(0, s.resolution), UnityEngine.Random.Range(0, s.resolution)); break;
                case Simulation.SpawnMode.InwardCircle: pos = centre + UnityEngine.Random.onUnitSphere * s.boundaryRadius * 0.8f; dir = (centre - pos).normalized; break;
                case Simulation.SpawnMode.RandomCircle: pos = centre + UnityEngine.Random.insideUnitSphere * s.resolution * 0.15f; break;
            }
            agents[i] = new Simulation.Agent { position = pos, direction = dir, rngState = (uint)UnityEngine.Random.Range(1, 10000000) };
        }
        ComputeBuffer agentBuffer = null;
        ComputeHelper.CreateAndSetBuffer(ref agentBuffer, agents, sim, "agents", update);
        sim.SetInt("numAgents", s.numAgents);
        sim.SetInt("resolution", s.resolution);
        sim.SetInt("boundaryRadius", s.boundaryRadius);
        var sp = s.speciesSetting;
        sim.SetFloat("moveSpeed", sp.moveSpeed);
        sim.SetFloat("turnSpeed", sp.turnSpeed / 180f);
        sim.SetFloat("sensorOffsetDst", sp.sensorOffsetDst);
        sim.SetInt("sensorSize", sp.sensorSize);
        sim.SetFloat("cosAngle", Mathf.Cos(sp.sensorAngleSpacing * Mathf.Deg2Rad));
        sim.SetFloat("sinAngle", Mathf.Sin(sp.sensorAngleSpacing * Mathf.Deg2Rad));
        sim.SetVector("invSize", Vector3.one / s.resolution);
        sim.SetFloat("trailWeight", s.trailWeight);

        const float dt = 0.02f; // ProjectSettings fixed timestep
        int simFrame = 0, frame = 0;
        RenderTexture latest = a;
        var tmp = new Simulation.Agent[1];
        foreach (int snapshot in cfg.snapshotFrames)
        {
            for (; frame < snapshot; frame++)
            {
                for (int k = 0; k < s.stepsPerFrame; k++)
                {
                    sim.SetFloat("deltaTime", dt);
                    sim.SetFloat("time", (frame + 1) * dt);
                    sim.SetFloat("diffuseFactor", Mathf.Clamp01(s.diffuseRate * dt));
                    sim.SetFloat("decayFactor", s.decayRate * dt);
                    var (src, dst) = simFrame % 2 == 0 ? (a, b) : (b, a);
                    simFrame++;
                    sim.SetTexture(diffuse, "TrailMap", src);
                    sim.SetTexture(diffuse, "DiffusedTrailMap", dst);
                    ComputeHelper.Dispatch(sim, s.resolution, s.resolution, s.resolution, kernelIndex: diffuse);
                    sim.SetTexture(update, "ReadTrailMap", src);
                    sim.SetTexture(update, "WriteTrailMap", dst);
                    ComputeHelper.Dispatch(sim, s.numAgents, 1, 1, kernelIndex: update);
                    latest = dst;
                }
                if (frame % 50 == 0) agentBuffer.GetData(tmp, 0, 0, 1); // keep the GPU queue bounded
            }
            Debug.Log($"[cover] simulated {frame} fixed frames ({frame * dt:F1} s sim time)");
            latest.filterMode = cfg.linearVolume ? FilterMode.Bilinear : FilterMode.Point;
            foreach (var v in cfg.views) RenderView(cfg, cover, iso, s, latest, v, $"{v.name}_f{frame}");
        }
        agentBuffer.Release(); a.Release(); b.Release();
    }

    static void RenderView(Config cfg, ComputeShader cs, IsosurfaceDisplayStrategy iso, SlimeSettings s, RenderTexture vol, View v, string file)
    {
        int trace = cs.FindKernel("Trace"), resolve = cs.FindKernel("Resolve");
        var accum = new RenderTexture(cfg.width, cfg.height, 0, GraphicsFormat.R32G32B32A32_SFloat) { enableRandomWrite = true };
        var output = new RenderTexture(cfg.width, cfg.height, 0, GraphicsFormat.R8G8B8A8_UNorm) { enableRandomWrite = true };
        accum.Create(); output.Create();
        var clear = new CommandBuffer();
        clear.SetRenderTarget(accum); clear.ClearRenderTarget(false, true, Color.clear);
        Graphics.ExecuteCommandBuffer(clear);

        Quaternion rot = Quaternion.Euler(v.pitch, v.yaw, v.roll);
        Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right, up = rot * Vector3.up;
        Vector3 pos = v.target - fwd * v.distance;

        cs.SetInt("resolution", s.resolution);
        cs.SetInt("boundaryRadius", s.boundaryRadius);
        cs.SetVector("slicePlane", v.slicePlane);
        cs.SetVector("camPos", pos); cs.SetVector("camFwd", fwd); cs.SetVector("camRight", right); cs.SetVector("camUp", up);
        cs.SetFloat("tanHalfFovY", Mathf.Tan(v.fovY * 0.5f * Mathf.Deg2Rad));
        cs.SetFloat("aspect", cfg.width / (float)cfg.height);
        cs.SetFloat("focusDist", v.distance + v.focusOffset);
        cs.SetFloat("apertureRadius", v.aperture);
        cs.SetFloat("surfaceThreshold", iso.surfaceThreshold);
        cs.SetFloat("shinyness", iso.shinyness);
        cs.SetFloat("lightAmb", cfg.ambOverride >= 0 ? cfg.ambOverride : iso.lightAmb);
        cs.SetVector("lightDir", cfg.lightDir.normalized);
        cs.SetFloat("lightRadius", cfg.lightRadius);
        cs.SetFloat("keyIntensity", cfg.keyIntensity);
        cs.SetFloat("selfShadow", cfg.selfShadow);
        cs.SetVector("lightColor", iso.lightColor);
        cs.SetVector("frontColor", iso.primaryColor);
        cs.SetVector("backColor", iso.secondaryColor);
        cs.SetFloat("groundY", -(float)s.boundaryRadius / s.resolution - cfg.groundGap);
        cs.SetVector("groundColor", cfg.groundColor);
        cs.SetFloat("groundGloss", cfg.groundGloss);
        cs.SetFloat("poolRadius", cfg.poolRadius);
        cs.SetVector("bgColor", cfg.bgColor);
        cs.SetInt("width", cfg.width); cs.SetInt("height", cfg.height);
        cs.SetTexture(trace, "TrailMap", vol);
        cs.SetTexture(trace, "Accum", accum);

        var t0 = DateTime.Now;
        for (int i = 0; i < cfg.samples; i++)
        {
            cs.SetInt("frameIndex", i);
            ComputeHelper.Dispatch(cs, cfg.width, cfg.height, 1, kernelIndex: trace);
            if (i % 4 == 3) { var r = AsyncGPUReadback.Request(accum, 0, 0, 1, 0, 1, 0, 1); r.WaitForCompletion(); }
        }
        cs.SetFloat("exposure", cfg.exposure);
        cs.SetFloat("invSamples", 1f / cfg.samples);
        cs.SetFloat("vignette", cfg.vignette);
        cs.SetFloat("tonemap", cfg.tonemap);
        cs.SetTexture(resolve, "Accum", accum);
        cs.SetTexture(resolve, "Output", output);
        ComputeHelper.Dispatch(cs, cfg.width, cfg.height, 1, kernelIndex: resolve);

        var tex = new Texture2D(cfg.width, cfg.height, TextureFormat.RGBA32, false, true);
        RenderTexture.active = output;
        tex.ReadPixels(new Rect(0, 0, cfg.width, cfg.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        string path = Path.Combine(cfg.outDir, file + ".png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        accum.Release(); output.Release();
        Debug.Log($"[cover] wrote {path} ({cfg.samples} spp, {(DateTime.Now - t0).TotalSeconds:F1} s)");
    }
}
