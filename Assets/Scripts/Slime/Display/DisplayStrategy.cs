using UnityEngine;
using ComputeShaderUtility;

public abstract class DisplayStrategy : ScriptableObject
{
    protected virtual string KernelName => "Render";

    [Header("Shader Setup")]
    public ComputeShader shader;


    public abstract void Dispatch(
        RenderTexture sourceTrailMap,
        RenderTexture destinationScreen,
        ComputeBuffer agentsBuffer,
        SlimeSettings settings,
        Camera camera
    );
}