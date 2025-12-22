using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Slime Settings/Simulation")]
public class SlimeSettings : ScriptableObject
{
	[Header("Simulation Settings")]
	[Min(1)] public int stepsPerFrame = 1;
	public int resolution = 300;
	public int boundaryRadius = 150;
	public int numAgents = 100;
	public Simulation.SpawnMode spawnMode;

	[Header("Trail Settings")]
	public float trailWeight = 1;
	public float decayRate = 1;
	public float diffuseRate = 1;

	public SpeciesSettings speciesSetting;

	[System.Serializable]
	public struct SpeciesSettings
	{
		[Header("Movement Settings")]
		public float moveSpeed;
		public float turnSpeed;

		[Header("Sensor Settings")]
		public float sensorAngleSpacing;
		public float sensorOffsetDst;
		public int sensorSize;
	}
}
