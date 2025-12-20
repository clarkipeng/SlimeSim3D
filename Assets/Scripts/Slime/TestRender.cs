using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using ComputeShaderUtility;

public class TestRender : MonoBehaviour
{
    const int updateKernel = 0;
    const int diffuseMapKernel = 1;
    const int colourKernel = 2;

    [Header("Volume Settings")]
    [Range(2, 128)]
    public int resolution = 32;
    [Range(0, 1)]
    public float sparsity = 0.9f;


    [Header("Render Settings")]
    [Range(0, 1)]
    public float opacity = 1.0f;

    private Texture3D _volumeTexture;
    private Texture2D colourMapTexture;

    public FilterMode filterMode = FilterMode.Point;
    public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;

    public RawImage outputImage;

    // CAMERA INFO
    public Camera _camera;
    public ComputeShader CameraShader;

    [SerializeField, HideInInspector] protected RenderTexture displayTexture;

    void Init()
    {
        ComputeHelper.CreateRenderTexture(ref displayTexture, Screen.width, Screen.height, filterMode, format);

        _volumeTexture = CreateRandomTexture3D(resolution, sparsity);
        CameraShader.SetTexture(0, "InputTexture", _volumeTexture);

        CameraShader.SetTexture(0, "Result", displayTexture);
    }
    protected virtual void Start()
    {
        Init();
        if (outputImage != null) outputImage.texture = displayTexture;
    }
    Texture3D CreateRandomTexture3D(int size, float sparsity)
    {
        Texture3D tex = new Texture3D(size, size, size, TextureFormat.RGBAFloat, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] cols = new Color[size * size * size];
        for (int i = 0; i < cols.Length; i++)
        {
            float alpha = (Random.value > sparsity) ? 1.0f : 0.0f;
            cols[i] = new Color(Random.value, Random.value, Random.value, alpha);
        }

        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    void LateUpdate()
    {
        CameraShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        CameraShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        CameraShader.SetFloat("_Opacity", opacity);
        ComputeHelper.Dispatch(CameraShader, Screen.width, Screen.height, 1, 0);
    }

}
