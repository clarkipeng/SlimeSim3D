using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;
using UnityEngine.UI;

public class Simulation : MonoBehaviour
{
	public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

	int updateKernel;
	int diffuseMapKernel;
	int drawKernel;

	public ComputeShader compute;

	public SlimeSettings settings;

	[Header("Display Settings")]
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;
	public GraphicsFormat volumeFormat = GraphicsFormat.R8_UNorm;

	[Header("Resolution Settings")]
	public bool useFixedResolution = false;
	public Vector2Int fixedResolution = new Vector2Int(3840, 2160);

	[Header("Display Strategy")]
	public DisplayStrategy displayStrategy;

	[Header("Post Processing")]
	public bool postProcess = false;
	public Material ppMaterial;

	[SerializeField, HideInInspector] protected RenderTexture trailMap;
	[SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
	[SerializeField, HideInInspector] protected RenderTexture displayTexture;

	ComputeBuffer agentBuffer;

	public RawImage outputImage;

	// CAMERA INFO
	public Camera camera;

	public virtual void Start()
	{
		Init();
		outputImage.texture = displayTexture;
	}

	int GetRenderWidth()
	{
		return useFixedResolution ? fixedResolution.x : Screen.width;
	}
	int GetRenderHeight()
	{
		return useFixedResolution ? fixedResolution.y : Screen.height;
	}

	public void Init()
	{
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.resolution, settings.resolution, settings.resolution, filterMode, volumeFormat);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.resolution, settings.resolution, settings.resolution, filterMode, volumeFormat);

		ComputeHelper.CreateRenderTexture(ref displayTexture, GetRenderWidth(), GetRenderHeight(), filterMode, format);

		updateKernel = compute.FindKernel("Update");
		diffuseMapKernel = compute.FindKernel("Diffuse");

		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);

		// Create agents with initial positions and angles
		Agent[] agents = new Agent[settings.numAgents];
		for (int i = 0; i < agents.Length; i++)
		{
			Vector3 centre = new Vector3(settings.resolution / 2, settings.resolution / 2, settings.resolution / 2);
			Vector3 startPos = Vector3.zero;

			Vector3 randomDirection = Random.onUnitSphere;
			Vector3 direction = Vector3.zero;

			if (settings.spawnMode == SpawnMode.Point)
			{
				startPos = centre;
				direction = randomDirection;
			}
			else if (settings.spawnMode == SpawnMode.Random)
			{
				startPos = new Vector3(Random.Range(0, settings.resolution), Random.Range(0, settings.resolution), Random.Range(0, settings.resolution));
				direction = randomDirection;
			}
			else if (settings.spawnMode == SpawnMode.InwardCircle)
			{
				startPos = centre + Random.onUnitSphere * settings.boundaryRadius * 0.8f;
				direction = (centre - startPos).normalized;
			}
			else if (settings.spawnMode == SpawnMode.RandomCircle)
			{
				startPos = centre + Random.insideUnitSphere * settings.resolution * 0.15f;
				direction = randomDirection;
			}

			agents[i] = new Agent() { position = startPos, direction = direction, pad0 = 0.0f, rngState = (uint)Random.Range(1, 10000000) };
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);

		compute.SetInt("numAgents", settings.numAgents);

		compute.SetInt("resolution", settings.resolution);
		compute.SetInt("boundaryRadius", settings.boundaryRadius);
	}


	void UpdateCompute()
	{
		var s = settings.speciesSetting;

		compute.SetFloat("moveSpeed", s.moveSpeed);
		compute.SetFloat("turnSpeed", s.turnSpeed / 180f);
		compute.SetFloat("sensorOffsetDst", s.sensorOffsetDst);
		compute.SetInt("sensorSize", s.sensorSize);

		float angleRad = s.sensorAngleSpacing * Mathf.Deg2Rad;
		compute.SetFloat("cosAngle", Mathf.Cos(angleRad));
		compute.SetFloat("sinAngle", Mathf.Sin(angleRad));

		compute.SetVector("invSize", new Vector3(1.0f / settings.resolution, 1.0f / settings.resolution, 1.0f / settings.resolution));

		compute.SetFloat("trailWeight", settings.trailWeight);
	}

	void FixedUpdate()
	{
		UpdateCompute();
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}

	private int simFrame = 0;
	(RenderTexture source, RenderTexture destination) GetTrailMaps(bool iterateFrame)
	{
		bool isEvenFrame = simFrame % 2 == 0;

		if (iterateFrame) simFrame += 1;

		if (isEvenFrame)
		{
			return (trailMap, diffusedTrailMap);
		}
		else
		{
			return (diffusedTrailMap, trailMap);
		}
	}

	void LateUpdate()
	{
		if (displayTexture.width != GetRenderWidth() || displayTexture.height != GetRenderHeight())
		{
			// ComputeHelper.Release(displayTexture);
			displayTexture.Release();
			ComputeHelper.CreateRenderTexture(ref displayTexture, GetRenderWidth(), GetRenderHeight(), filterMode, format);
			outputImage.texture = displayTexture;
		}
		displayStrategy.Dispatch(
			GetTrailMaps(false).destination,
			displayTexture,
			agentBuffer,
			settings,
			camera
		);
		if (postProcess && ppMaterial != null)
		{
			RenderTexture temp = RenderTexture.GetTemporary(displayTexture.descriptor);
			Graphics.Blit(displayTexture, temp, ppMaterial);
			Graphics.Blit(temp, displayTexture);
			RenderTexture.ReleaseTemporary(temp);
		}
	}

	void RunSimulation()
	{
		float dt = Time.deltaTime;
		compute.SetFloat("deltaTime", dt);
		compute.SetFloat("time", Time.time);

		compute.SetFloat("diffuseFactor", Mathf.Clamp01(settings.diffuseRate * dt));
		compute.SetFloat("decayFactor", settings.decayRate * dt);
		compute.SetFloat("trailWeight", settings.trailWeight);

		var maps = GetTrailMaps(true);

		compute.SetTexture(diffuseMapKernel, "TrailMap", maps.source);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", maps.destination);
		ComputeHelper.Dispatch(compute, settings.resolution, settings.resolution, settings.resolution, kernelIndex: diffuseMapKernel);

		compute.SetTexture(updateKernel, "ReadTrailMap", maps.source);
		compute.SetTexture(updateKernel, "WriteTrailMap", maps.destination);
		ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
	}

	void OnDestroy()
	{
		ComputeHelper.Release(agentBuffer);
	}

	public struct Agent
	{
		public Vector3 position;
		public float pad0;

		public Vector3 direction;
		public uint rngState;
	}
}
