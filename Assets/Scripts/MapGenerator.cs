﻿using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour {


	public const int mapChunkSize = 241;

	public float noiseScale;

	public int octaves;
	[Range(0,1)]
	public float persistance;
	public float lacunarity;

	public int seed;
	public Vector2 offset;

	public float meshHeightMultiplier;
	public AnimationCurve meshHeightCurve;

	public TerrainType[] regions;

	Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
	Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

	public void RequestMapData(Vector2 centre, Action<MapData> callback) {
		ThreadStart threadStart = delegate {
			MapDataThread (centre, callback);
		};

		new Thread (threadStart).Start ();
	}

	void MapDataThread(Vector2 centre, Action<MapData> callback) {
		MapData mapData = GenerateMapData (centre);
		lock (mapDataThreadInfoQueue) {
			mapDataThreadInfoQueue.Enqueue (new MapThreadInfo<MapData> (callback, mapData));
		}
	}

	public void RequestMeshData(MapData mapData, Action<MeshData> callback) {
		ThreadStart threadStart = delegate {
			MeshDataThread (mapData, callback);
		};

		new Thread (threadStart).Start ();
	}

	void MeshDataThread(MapData mapData, Action<MeshData> callback) {
		MeshData meshData = MeshGenerator.GenerateTerrainMesh (mapData.heightMap, meshHeightMultiplier, meshHeightCurve);
		lock (meshDataThreadInfoQueue) {
			meshDataThreadInfoQueue.Enqueue (new MapThreadInfo<MeshData> (callback, meshData));
		}
	}

	void Update() {
		if (mapDataThreadInfoQueue.Count > 0) {
			for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
				MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}

		if (meshDataThreadInfoQueue.Count > 0) {
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
				MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}
	}

    public void GenerateMeshData(MapData mapData) {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve);
    }

    public MapData GenerateMapData(Vector2 centre) {

		float[,] noiseMap = Noise.GenerateNoiseMap (mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, centre + offset);

		Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
		for (int y = 0; y < mapChunkSize; y++) {
			for (int x = 0; x < mapChunkSize; x++) {
				float currentHeight = noiseMap [x, y];
				for (int i = 0; i < regions.Length; i++) {
					if (currentHeight >= regions [i].height) {
						colourMap [y * mapChunkSize + x] = regions [i].colour;
					} else {
						break;
					}
				}
			}
		}


		return new MapData (noiseMap, colourMap);
	}

	struct MapThreadInfo<T> {
		public readonly Action<T> callback;
		public readonly T parameter;

		public MapThreadInfo (Action<T> callback, T parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}

	}

}

[System.Serializable]
public struct TerrainType {
	public string name;
	public float height;
	public Color colour;
}

public struct MapData {
	public readonly float[,] heightMap;
	public readonly Color[] colourMap;

	public MapData (float[,] heightMap, Color[] colourMap)
	{
		this.heightMap = heightMap;
		this.colourMap = colourMap;
	}
}
