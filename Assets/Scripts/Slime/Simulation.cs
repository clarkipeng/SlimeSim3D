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
	// public ComputeShader drawAgentsCS;
	public ComputeShader drawShader;

	public SlimeSettings settings;

	[Header("Display Settings")]
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;
	public GraphicsFormat volumeFormat = GraphicsFormat.R16_SFloat;


	[Header("Render Settings")]
	[Range(0, 1)]
	public float opacity = 1.0f;

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


	public void Init()
	{
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, settings.depth, filterMode, volumeFormat);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, settings.depth, filterMode, volumeFormat);

		ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);

		updateKernel = compute.FindKernel("Update");
		diffuseMapKernel = compute.FindKernel("Diffuse");
		drawKernel = drawShader.FindKernel("CSMain");

		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);

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

			agents[i] = new Agent() { position = startPos, direction = direction };
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);

		compute.SetInt("numAgents", settings.numAgents);
		// drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
		// drawAgentsCS.SetInt("numAgents", settings.numAgents);

		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);
		compute.SetInt("depth", settings.depth);
	}


	void UpdateCompute()
	{
		var s = settings.speciesSetting;

		compute.SetFloat("moveSpeed", s.moveSpeed);
		compute.SetFloat("turnSpeed", s.turnSpeed / 180f);
		compute.SetFloat("sensorOffsetDst", s.sensorOffsetDst);
		compute.SetInt("sensorSize", s.sensorSize);

		// Precompute trig once per frame
		float angleRad = s.sensorAngleSpacing * Mathf.Deg2Rad;
		compute.SetFloat("cosAngle", Mathf.Cos(angleRad));
		compute.SetFloat("sinAngle", Mathf.Sin(angleRad));

		compute.SetFloat("trailWeight", settings.trailWeight);
		compute.SetFloat("decayRate", settings.decayRate);
		compute.SetFloat("diffuseRate", settings.diffuseRate);
	}

	void FixedUpdate()
	{
		UpdateCompute();
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
			drawShader.SetTexture(drawKernel, "Result", displayTexture);
			outputImage.texture = displayTexture;
		}
		drawShader.SetInt("width", settings.width);
		drawShader.SetInt("height", settings.height);
		drawShader.SetInt("depth", settings.depth);

		drawShader.SetFloat("opacity", opacity);
		drawShader.SetVector("color", settings.speciesSetting.color);
		drawShader.SetMatrix("cameraToWorld", camera.cameraToWorldMatrix);
		drawShader.SetMatrix("cameraInverseProjection", camera.projectionMatrix.inverse);

		drawShader.SetTexture(drawKernel, "TrailMap", trailMap);

		ComputeHelper.Dispatch(drawShader, Screen.width, Screen.height, 1, kernelIndex: drawKernel);
	}

	void RunSimulation()
	{
		compute.SetFloat("deltaTime", Time.deltaTime);
		compute.SetFloat("time", Time.time);

		ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
		ComputeHelper.Dispatch(compute, settings.width, settings.height, settings.depth, kernelIndex: diffuseMapKernel);

		ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);
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
		public float pad1;
	}
}
