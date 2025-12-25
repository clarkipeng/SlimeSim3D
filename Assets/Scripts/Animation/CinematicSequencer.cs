using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;

public class CinematicSequencer : MonoBehaviour
{
    [Header("References")]
    public OrbitCamera orbitCamera;
    public Simulation simulation;

    [Header("Sequence")]
    public bool playOnStart = false;
    public bool recordOnStart = false;
    public bool loop = false;
    public float fps = 60;
    public List<KeyFrame> timeline = new List<KeyFrame>();

    [Header("Resolution")]
    public int renderWidth = 2560;
    public int renderHeight = 1440;

    private DisplayStrategy tempStartSettings;
    private List<FieldInfo> floatFields = new List<FieldInfo>();
    private List<FieldInfo> vec3Fields = new List<FieldInfo>();
    private List<FieldInfo> vec4Fields = new List<FieldInfo>();
    private List<FieldInfo> intFields = new List<FieldInfo>();
    private List<FieldInfo> colorFields = new List<FieldInfo>();

    private string sceneName;
    private RecorderController recorderController;
    private DisplayStrategy activeSettings;

    void Start()
    {
        activeSettings = simulation.displayStrategy;
        sceneName = SceneManager.GetActiveScene().name;

        System.Type type = activeSettings.GetType();
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.FieldType == typeof(float)) floatFields.Add(f);
            else if (f.FieldType == typeof(Vector3)) vec3Fields.Add(f);
            else if (f.FieldType == typeof(Vector4)) vec4Fields.Add(f);
            else if (f.FieldType == typeof(Color)) colorFields.Add(f);
            else if (f.FieldType == typeof(int)) intFields.Add(f);
        }

        tempStartSettings = (DisplayStrategy)ScriptableObject.CreateInstance(type);

        if (playOnStart && timeline.Count > 0)
        {
            PlaySequence(recordOnStart);
            StartCoroutine(RunSequence());
        }
    }

    [ContextMenu("Play Sequence")]
    public void PlaySequence() => PlaySequence(true);

    private void PlaySequence(bool shouldRecord = true)
    {
        StopAllCoroutines();
#if UNITY_EDITOR
        StopRecording();
        if (shouldRecord) StartRecording();
#endif
        StartCoroutine(RunSequence());
    }

#if UNITY_EDITOR
    [ContextMenu("Take Screenshot")]
    public void TakeScreenshot()
    {
        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var recorderSettings = ScriptableObject.CreateInstance<ImageRecorderSettings>();

        recorderSettings.name = "Screenshot";
        recorderSettings.Enabled = true;
        recorderSettings.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
        recorderSettings.CaptureAlpha = false;

        SetupRecorderDefaults(recorderSettings, "Shot", "png");

        recorderSettings.imageInputSettings = CreateInputSettings(); 

        controllerSettings.AddRecorderSettings(recorderSettings);
        controllerSettings.SetRecordModeToSingleFrame(0); 

        var controller = new RecorderController(controllerSettings);
        controller.PrepareRecording();
        controller.StartRecording();

        Debug.Log($"<color=cyan>Screenshot Saved:</color> {recorderSettings.OutputFile}.png");
    }

    public void StartRecording()
    {
        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var recorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();

        recorderSettings.name = "Video Recorder";
        recorderSettings.Enabled = true;

        // var encoderSettings = new ProResEncoderSettings();
        // encoderSettings.OutputFormat = ProResEncoderSettings.OutputFormat.ProRes422;
        // recorderSettings.EncoderSettings = encoderSettings;
        var encoderSettings = new CoreEncoderSettings
        {
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        recorderSettings.EncoderSettings = encoderSettings;

        SetupRecorderDefaults(recorderSettings, "Video", "mov");

        recorderSettings.ImageInputSettings = CreateInputSettings();

        controllerSettings.AddRecorderSettings(recorderSettings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = fps;
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;

        RecorderOptions.VerboseMode = false;

        recorderController = new RecorderController(controllerSettings);
        recorderController.PrepareRecording();
        recorderController.StartRecording();

        Debug.Log($"Started Recording to: {recorderSettings.OutputFile}");
    }

    public void StopRecording()
    {
        if (recorderController != null && recorderController.IsRecording())
        {
            recorderController.StopRecording();
            Debug.Log("Stopped Recording.");
        }
        recorderController = null;
        simulation.useFixedResolution = false;
    }

    private GameViewInputSettings CreateInputSettings()
    {
        simulation.useFixedResolution = true;
        simulation.fixedResolution = new Vector2Int(renderWidth, renderHeight);

        return new GameViewInputSettings
        {
            OutputWidth = renderWidth,
            OutputHeight = renderHeight,
        };
    }

    private void SetupRecorderDefaults(RecorderSettings settings, string suffix, string ext)
    {
        string baseFolder = System.IO.Path.Combine(Application.dataPath, "../Recordings");
        if (!System.IO.Directory.Exists(baseFolder)) System.IO.Directory.CreateDirectory(baseFolder);

        int tryCnt = 0;
        while (System.IO.File.Exists(System.IO.Path.Combine(baseFolder, $"{sceneName}_{suffix}_{tryCnt}.{ext}")))
        {
            tryCnt++;
        }
        settings.OutputFile = System.IO.Path.Combine(baseFolder, $"{sceneName}_{suffix}_{tryCnt}");
    }
#endif

    IEnumerator RunSequence()
    {
        if (orbitCamera == null) yield break;
        orbitCamera.Disable();

        float startYaw = orbitCamera.yaw;
        float startPitch = orbitCamera.pitch;
        float startRadius = orbitCamera.radius;

        CopySettings(activeSettings, tempStartSettings);

        int index = 0;

        while (index < timeline.Count)
        {
            KeyFrame target = timeline[index];
            float timer = 0f;

            while (timer < target.duration)
            {
                timer += Time.deltaTime;
                float progress = Mathf.Clamp01(timer / Mathf.Max(0.01f, target.duration));
                float t = ApplyEasing(progress, target.easing);

                orbitCamera.yaw = Mathf.Lerp(startYaw, target.yaw, t);
                orbitCamera.pitch = Mathf.Lerp(startPitch, target.pitch, t);
                orbitCamera.radius = Mathf.Lerp(startRadius, target.radius, t);

                InterpolateSettings(tempStartSettings, target.displaySettings, activeSettings, t);
                yield return null;
            }

            orbitCamera.yaw = target.yaw;
            orbitCamera.pitch = target.pitch;
            orbitCamera.radius = target.radius;
            InterpolateSettings(target.displaySettings, target.displaySettings, activeSettings, 1.0f);

            if (target.holdTime > 0) yield return new WaitForSeconds(target.holdTime);

            startYaw = orbitCamera.yaw;
            startPitch = orbitCamera.pitch;
            startRadius = orbitCamera.radius;
            CopySettings(activeSettings, tempStartSettings);

            index++;
            if (index >= timeline.Count)
            {
                if (loop) index = 0;
                else
                {
                    orbitCamera.Enable();
#if UNITY_EDITOR
                    StopRecording();
#endif
                    yield break;
                }
            }
        }
        orbitCamera.Enable();
    }

    float ApplyEasing(float t, KeyFrame.EasingType type)
    {
        switch (type)
        {
            case KeyFrame.EasingType.Linear: return t;
            case KeyFrame.EasingType.SmoothStep: return t * t * (3f - 2f * t);
            case KeyFrame.EasingType.SmootherStep: return t * t * t * (t * (t * 6f - 15f) + 10f);
            case KeyFrame.EasingType.EaseInQuad: return t * t;
            case KeyFrame.EasingType.EaseOutQuad: return 1f - (1f - t) * (1f - t);
            case KeyFrame.EasingType.EaseInCubic: return t * t * t;
            case KeyFrame.EasingType.EaseOutCubic: return 1f - Mathf.Pow(1f - t, 3f);
            default: return t;
        }
    }

    void InterpolateSettings(DisplayStrategy a, DisplayStrategy b, DisplayStrategy target, float t)
    {
        if (a == null || b == null) return;
        foreach (var f in floatFields) f.SetValue(target, Mathf.Lerp((float)f.GetValue(a), (float)f.GetValue(b), t));
        foreach (var f in vec3Fields) f.SetValue(target, Vector3.Lerp((Vector3)f.GetValue(a), (Vector3)f.GetValue(b), t));
        foreach (var f in vec4Fields) f.SetValue(target, Vector4.Lerp((Vector4)f.GetValue(a), (Vector4)f.GetValue(b), t));
        foreach (var f in colorFields) f.SetValue(target, Color.Lerp((Color)f.GetValue(a), (Color)f.GetValue(b), t));
        foreach (var f in intFields) f.SetValue(target, (int)Mathf.Lerp((int)f.GetValue(a), (int)f.GetValue(b), t));
    }

    void CopySettings(DisplayStrategy source, DisplayStrategy dest)
    {
        if (source == null || dest == null) return;
        foreach (var f in floatFields) f.SetValue(dest, f.GetValue(source));
        foreach (var f in vec3Fields) f.SetValue(dest, f.GetValue(source));
        foreach (var f in vec4Fields) f.SetValue(dest, f.GetValue(source));
        foreach (var f in colorFields) f.SetValue(dest, f.GetValue(source));
        foreach (var f in intFields) f.SetValue(dest, f.GetValue(source));
    }

    private bool AreSettingsIdentical(DisplayStrategy source, DisplayStrategy dest)
    {
        if (source == null || dest == null) return false;
        foreach (var f in floatFields) if (!f.GetValue(dest).Equals(f.GetValue(source))) return false;
        foreach (var f in vec3Fields) if (!f.GetValue(dest).Equals(f.GetValue(source))) return false;
        foreach (var f in vec4Fields) if (!f.GetValue(dest).Equals(f.GetValue(source))) return false;
        foreach (var f in colorFields) if (!f.GetValue(dest).Equals(f.GetValue(source))) return false;
        foreach (var f in intFields) if (!f.GetValue(dest).Equals(f.GetValue(source))) return false;
        return true;
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        StopRecording();
#endif
    }
#if UNITY_EDITOR
    [ContextMenu("Snapshot Keyframe")]
    public void SnapshotKeyframe()
    {
        if (orbitCamera == null) { Debug.LogError("Assign OrbitCamera!"); return; }
        string folderPath = $"Assets/Keyframes/{sceneName}";
        if (!System.IO.Directory.Exists(folderPath)) { System.IO.Directory.CreateDirectory(folderPath); UnityEditor.AssetDatabase.Refresh(); }

        KeyFrame shot = ScriptableObject.CreateInstance<KeyFrame>();
        shot.name = "Shot_" + (timeline.Count + 1);

        bool createNewSetting = true;
        for (int prev = 0; prev < timeline.Count; prev++)
        {
            if (AreSettingsIdentical(timeline[prev].displaySettings, activeSettings))
            {
                shot.displaySettings = timeline[prev].displaySettings;
                createNewSetting = false;
                break;
            }
        }
        if (createNewSetting)
        {
            shot.displaySettings = Instantiate(activeSettings);
            shot.displaySettings.name = shot.name + "_Settings";
        }

        shot.yaw = orbitCamera.yaw;
        shot.pitch = orbitCamera.pitch;
        shot.radius = orbitCamera.radius;
        shot.duration = 5.0f;
        shot.easing = KeyFrame.EasingType.SmootherStep;

        string fullPath = $"{folderPath}/{shot.name}.asset";
        UnityEditor.AssetDatabase.CreateAsset(shot, fullPath);
        UnityEditor.AssetDatabase.AddObjectToAsset(shot.displaySettings, shot);
        UnityEditor.AssetDatabase.SaveAssets();
        timeline.Add(shot);
    }
#endif
}