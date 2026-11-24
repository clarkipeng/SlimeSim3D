using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sets up the Cinematic scene (the GradientCircle simulation shown with the Cinematic display
// strategy) and builds a Windows player of it and the other showcase scenes, for headless capture
// with LiveCapture. Existing assets are left as they are.
//   Unity.exe -batchmode -quit -projectPath . -executeMethod CinematicBuild.Build -buildPath C:/out/Slime.exe
public static class CinematicBuild
{
    const string StrategyPath = "Assets/Settings/Display/Cinematic.asset";
    const string ScenePath = "Assets/Scenes/Cinematic.unity";
    const string SourceScene = "Assets/Scenes/GradientCircle.unity";
    const string Palette = "Assets/Keyframes/GradientCircle/SceneIsosurface 3.asset"; // the README look
    static readonly string[] Scenes =
    {
        ScenePath, SourceScene, "Assets/Scenes/Slime3D.unity", "Assets/Scenes/DensityCircle.unity",
        "Assets/Scenes/AgentDir2Test.unity", "Assets/Scenes/ForwardTest.unity",
    };

    [MenuItem("Slime/Build Cinematic Player")]
    public static void Build()
    {
        var strategy = AssetDatabase.LoadAssetAtPath<CinematicDisplayStrategy>(StrategyPath) ?? CreateStrategy();
        if (!System.IO.File.Exists(ScenePath)) CreateScene(strategy);

        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-buildPath");
        string path = i >= 0 ? args[i + 1] : "Builds/Cinematic/Slime.exe";
        var report = BuildPipeline.BuildPlayer(Scenes, path, BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[build] {report.summary.result} {path} ({report.summary.totalErrors} errors)");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    static CinematicDisplayStrategy CreateStrategy()
    {
        var iso = AssetDatabase.LoadAllAssetsAtPath(Palette).OfType<IsosurfaceDisplayStrategy>().First();
        var s = ScriptableObject.CreateInstance<CinematicDisplayStrategy>();
        s.shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Scripts/Slime/Display/Cinematic.compute");
        s.surfaceThreshold = iso.surfaceThreshold;
        s.shinyness = iso.shinyness;
        s.lightColor = iso.lightColor;
        s.primaryColor = iso.primaryColor;
        s.secondaryColor = iso.secondaryColor;
        s.slicePlane = iso.slicePlane;
        s.lightAmb = 0.25f;
        s.lightDir = new Vector3(-0.5f, 1f, -0.35f);
        AssetDatabase.CreateAsset(s, StrategyPath);
        AssetDatabase.SaveAssets();
        return s;
    }

    static void CreateScene(CinematicDisplayStrategy strategy)
    {
        var scene = EditorSceneManager.OpenScene(SourceScene);
        var sim = UnityEngine.Object.FindObjectOfType<Simulation>();
        sim.displayStrategy = strategy;

        var sequencer = UnityEngine.Object.FindObjectOfType<CinematicSequencer>();
        if (sequencer != null) { sequencer.playOnStart = false; sequencer.recordOnStart = false; }

        // Frame the cover view: OrbitCamera reads its starting orbit from the transform.
        var orbit = UnityEngine.Object.FindObjectOfType<OrbitCamera>();
        var rotation = Quaternion.Euler(7, 30, 0);
        orbit.transform.SetPositionAndRotation(orbit.target.position + rotation * new Vector3(0, 0, -4.8f), rotation);
        sim.camera.fieldOfView = 17;
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
