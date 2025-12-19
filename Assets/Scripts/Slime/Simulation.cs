using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;
using UnityEngine.UI;

public class Simulation : MonoBehaviour
{
	public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

	int updateKernel;
	int diffuseMapKernel;
	int colorKernel;
	int drawKernel;

	public ComputeShader compute;
	// public ComputeShader drawAgentsCS;
	public ComputeShader drawShader;

	public SlimeSettings settings;

	[Header("Display Settings")]
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;


	[Header("Render Settings")]
	[Range(0, 1)]
	public float opacity = 1.0f;

	[SerializeField, HideInInspector] protected RenderTexture trailMap;
	[SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
	[SerializeField, HideInInspector] protected RenderTexture colorTexture;
	[SerializeField, HideInInspector] protected RenderTexture displayTexture;

	ComputeBuffer agentBuffer;
	ComputeBuffer settingsBuffer;
	// Texture3D colorMapTexture;

	public RawImage outputImage;

	// CAMERA INFO
	public Camera camera;

	protected virtual void Start()
	{
		Init();
		outputImage.texture = displayTexture;
	}


	void Init()
	{
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, settings.depth, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, settings.depth, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref colorTexture, settings.width, settings.height, settings.depth, filterMode, format);

		ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);


		updateKernel = compute.FindKernel("Update");
		diffuseMapKernel = compute.FindKernel("Diffuse");
		colorKernel = compute.FindKernel("UpdateColorMap");
		drawKernel = drawShader.FindKernel("CSMain");

		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);
		compute.SetTexture(colorKernel, "ColorMap", colorTexture);
		compute.SetTexture(colorKernel, "TrailMap", trailMap);

		drawShader.SetTexture(drawKernel, "ColorMap", colorTexture);
		drawShader.SetTexture(drawKernel, "TrailMap", trailMap);
		drawShader.SetTexture(drawKernel, "Result", displayTexture);

		// Create agents with initial positions and angles
		Agent[] agents = new Agent[settings.numAgents];
		for (int i = 0; i < agents.Length; i++)
		{
			Vector3 centre = new Vector3(settings.width / 2, settings.height / 2, settings.depth / 2);
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
				startPos = new Vector3(Random.Range(0, settings.width), Random.Range(0, settings.height), Random.Range(0, settings.depth));
				direction = randomDirection;
			}
			else if (settings.spawnMode == SpawnMode.InwardCircle)
			{
				startPos = centre + Random.insideUnitSphere * Mathf.Min(settings.height, settings.width, settings.depth) * 0.5f;
				direction = (centre - startPos).normalized;
			}
			else if (settings.spawnMode == SpawnMode.RandomCircle)
			{
				startPos = centre + Random.insideUnitSphere * Mathf.Min(settings.height, settings.width, settings.depth) * 0.15f;
				direction = randomDirection;
			}

			Vector3Int speciesMask;
			int speciesIndex = 0;
			int numSpecies = settings.speciesSettings.Length;

			if (numSpecies == 1)
			{
				speciesMask = Vector3Int.one;
			}
			else
			{
				int species = Random.Range(1, numSpecies + 1);
				speciesIndex = species - 1;
				speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
			}

			agents[i] = new Agent() { position = startPos, padding0 = 0, direction = direction, padding1 = 0, speciesMask = speciesMask, speciesIndex = speciesIndex };
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);

		compute.SetInt("numAgents", settings.numAgents);
		// drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
		// drawAgentsCS.SetInt("numAgents", settings.numAgents);

		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);
		compute.SetInt("depth", settings.depth);
	}

	void FixedUpdate()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}

	void LateUpdate()
	{
		if (displayTexture.width != Screen.width || displayTexture.height != Screen.height)
		{
			ComputeHelper.CreateRenderTexture(ref displayTexture, Screen.width, Screen.height, filterMode, format);
			drawShader.SetTexture(drawKernel, "ColorMap", colorTexture);
			drawShader.SetTexture(drawKernel, "TrailMap", trailMap);
			drawShader.SetTexture(drawKernel, "Result", displayTexture);
			outputImage.texture = displayTexture;
		}
		drawShader.SetTexture(drawKernel, "ColorMap", colorTexture);
		drawShader.SetTexture(drawKernel, "TrailMap", trailMap);

		var speciesSettings = settings.speciesSettings;
		drawShader.SetFloat("opacity", opacity);
		drawShader.SetInt("numSpecies", speciesSettings.Length);
		drawShader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
		drawShader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);
		// drawShader.SetTexture(drawKernel, "ColorMap", colorTexture);
		// drawShader.SetTexture(drawKernel, "TrailMap", trailMap);

		ComputeHelper.Dispatch(drawShader, Screen.width, Screen.height, 1, kernelIndex: drawKernel);
	}

	void RunSimulation()
	{
		var speciesSettings = settings.speciesSettings;
		ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
		compute.SetBuffer(updateKernel, "speciesSettings", settingsBuffer);
		compute.SetBuffer(colorKernel, "speciesSettings", settingsBuffer);

		// Assign settings
		compute.SetFloat("deltaTime", Time.fixedDeltaTime);
		compute.SetFloat("time", Time.fixedTime);

		compute.SetFloat("trailWeight", settings.trailWeight);
		compute.SetFloat("decayRate", settings.decayRate);
		compute.SetFloat("diffuseRate", settings.diffuseRate);
		compute.SetInt("numSpecies", speciesSettings.Length);


		ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
		ComputeHelper.Dispatch(compute, settings.width, settings.height, settings.depth, kernelIndex: diffuseMapKernel);
		ComputeHelper.Dispatch(compute, settings.width, settings.height, settings.depth, kernelIndex: colorKernel);

		ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
	}

	void OnDestroy()
	{

		ComputeHelper.Release(agentBuffer, settingsBuffer);
	}

	public struct Agent
	{
		public Vector3 position;
		public float padding0;

		public Vector3 direction;
		public float padding1;

		public Vector3Int speciesMask;
		public int speciesIndex;

		// 48 bytes
	}
}
