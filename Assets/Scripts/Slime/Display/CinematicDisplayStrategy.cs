using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

// The Isosurface look staged like a film still: soft shadows, a glossy ground under a light pool,
// depth of field focused on the orbit target, and an ACES tonemap. Runs every frame.
[CreateAssetMenu(menuName = "Slime Settings/Display/Cinematic")]
public class CinematicDisplayStrategy : IsosurfaceDisplayStrategy
{
    [Header("Key Light")]
    public float keyIntensity = 1f;
    [Range(0, 1)] public float selfShadow = 0.6f;
    public float lightSize = 0.08f; // tangent of the light's angular radius: shadow softness

    [Header("Ground")]
    public Color groundColor = new Color(0.09f, 0.085f, 0.1f);
    public Color backgroundColor = new Color(0.03f, 0.025f, 0.045f);
    [Range(0, 1)] public float groundGloss = 0.35f;
    public float poolRadius = 1.2f;
    public float groundGap = 0.01f;

    [Header("Lens")]
    public float aperture = 0.07f;      // lens radius in world units (the slime sphere has radius 0.5)
    public float focusOffset = -0.35f;  // focus distance = distance to the orbit target + offset
    public float maxBlur = 0.022f;      // largest blur radius as a fraction of image height

    [Header("Grade")]
    public float exposure = 0.95f;
    [Range(0, 1)] public float vignette = 0.6f;

    const int OccLevels = 6;
    RenderTexture scene, occupancy;

    public override void Dispatch(
        RenderTexture sourceTrailMap,
        RenderTexture destinationScreen,
        ComputeBuffer agentsBuffer,
        SlimeSettings settings,
        Camera camera
    )
    {
        if (shader == null) return;
        int w = destinationScreen.width, h = destinationScreen.height;
        if (scene == null || !scene.IsCreated() || scene.width != w || scene.height != h)
        {
            OnDisable();
            ComputeHelper.CreateRenderTexture(ref scene, w, h, FilterMode.Point, GraphicsFormat.R16G16B16A16_SFloat);
        }

        if (occupancy == null || !occupancy.IsCreated() || occupancy.width != settings.resolution)
        {
            if (occupancy != null) occupancy.Release();
            int r = settings.resolution;
            occupancy = new RenderTexture(new RenderTextureDescriptor(r, r, GraphicsFormat.R8_UNorm, 0, OccLevels)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = r,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
            });
            occupancy.wrapMode = TextureWrapMode.Clamp;
            occupancy.Create();
        }

        int occ = shader.FindKernel("Occupancy");
        int trace = shader.FindKernel("Trace");
        int composite = shader.FindKernel("Composite");

        shader.SetInt("resolution", settings.resolution);
        shader.SetInt("boundaryRadius", settings.boundaryRadius);
        shader.SetVector("slicePlane", slicePlane);
        shader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
        shader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);

        shader.SetFloat("surfaceThreshold", surfaceThreshold);
        shader.SetFloat("shinyness", shinyness);
        shader.SetFloat("lightAmb", lightAmb);
        shader.SetVector("lightDir", lightDir.normalized);
        shader.SetVector("lightColor", lightColor);
        shader.SetVector("frontColor", primaryColor);
        shader.SetVector("backColor", secondaryColor);
        shader.SetFloat("keyIntensity", keyIntensity);
        shader.SetFloat("lightSize", lightSize);
        shader.SetFloat("selfShadow", selfShadow);

        shader.SetFloat("groundY", -(float)settings.boundaryRadius / settings.resolution - groundGap);
        shader.SetFloat("groundGloss", groundGloss);
        shader.SetFloat("poolRadius", poolRadius);
        shader.SetVector("groundColor", groundColor);
        shader.SetVector("bgColor", backgroundColor);

        // Thin lens: a point at depth d blurs to a disc of aperture*|d-f|/d world units on the focal
        // plane, which spans 2*f*tan(fov/2) world units over the image height.
        float focus = Mathf.Max(0.05f, Vector3.Dot(-camera.transform.position, camera.transform.forward) + focusOffset);
        float pixelsPerUnit = h / (2 * focus * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        shader.SetFloat("focusDist", focus);
        shader.SetFloat("cocScale", aperture * pixelsPerUnit);
        shader.SetFloat("maxCoc", aperture > 0 ? maxBlur * h : 0);
        shader.SetFloat("exposure", exposure);
        shader.SetFloat("vignette", vignette);

        shader.SetTexture(occ, "TrailMap", sourceTrailMap);
        shader.SetInt("occLevels", OccLevels);
        for (int level = 0; level < OccLevels; level++)
        {
            int n = Mathf.Max(1, settings.resolution >> level);
            shader.SetInt("occLevel", level);
            shader.SetTexture(occ, "OccOut", occupancy, level);
            ComputeHelper.Dispatch(shader, n, n, n, kernelIndex: occ);
        }

        shader.SetInt("width", w);
        shader.SetInt("height", h);
        shader.SetTexture(trace, "TrailMap", sourceTrailMap);
        shader.SetTexture(trace, "Scene", scene);
        shader.SetTexture(trace, "OccMap", occupancy);
        ComputeHelper.Dispatch(shader, w, h, 1, kernelIndex: trace);

        shader.SetTexture(composite, "SceneIn", scene);
        shader.SetTexture(composite, "Result", destinationScreen);
        ComputeHelper.Dispatch(shader, w, h, 1, kernelIndex: composite);
    }

    void OnDisable()
    {
        if (scene != null) scene.Release();
        if (occupancy != null) occupancy.Release();
        scene = occupancy = null;
    }
}
