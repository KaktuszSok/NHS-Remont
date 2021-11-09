using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHSRemont.Environment
{
    /// <summary>
    /// Reduces terrain tiling by applying a larger, faint copy of the original texture on top of select textures.
    /// </summary>
    [RequireComponent(typeof(RuntimeTerrain))]
    public class TerrainTilingFix : MonoBehaviour
    {
        private bool applied = false;
        private Terrain terrain;
        private TerrainData terrainData;
        
        [SerializeField]
        private bool[] layersToApplyOn = {true};
        [SerializeField]
        private float sizeMultiplier = 16f;
        [SerializeField] [Range(0f, 1f)]
        private float opacityMultiplier = 0.33f;

        private void Start()
        {
            terrain = GetComponent<Terrain>();
            terrainData = terrain.terrainData;
            if (!applied)
            {
                Apply();
            }
            applied = true;
        }

        private void Apply()
        {
            TerrainLayer[] layers = terrainData.terrainLayers;
            //generate overlay layers
            Queue<TerrainLayer> layersToAdd = new Queue<TerrainLayer>();
            Dictionary<int, int> overlayIndexToOldIndexMap = new Dictionary<int, int>(); //map where key is index of the overlay layer and value is index of the corresponding original layer
            for (int i = 0; i < layersToApplyOn.Length; i++)
            {
                if(!layersToApplyOn[i]) continue;
                if (i >= layers.Length)
                {
                    Debug.LogWarning("TerrainTilingFix has " + layersToApplyOn.Length + " layers specified whether to be applied on, but the terrain only has " + layers.Length + " layers defined.");
                    break;
                }

                TerrainLayer overlayLayer = Instantiate(layers[i]);
                overlayLayer.tileSize *= sizeMultiplier;
                layersToAdd.Enqueue(overlayLayer);
                overlayIndexToOldIndexMap[layers.Length + overlayIndexToOldIndexMap.Count] = i; //new index is layers.Length + amount of layers we've planned to add so far
            }

            //expand layers array and add the overlay layers
            int originalLayersLength = layers.Length;
            Array.Resize(ref layers, layers.Length + layersToAdd.Count);
            for (int i = originalLayersLength; i < layers.Length; i++)
            {
                layers[i] = layersToAdd.Dequeue();
            }
            //apply layers to terrain
            terrainData.terrainLayers = layers;

            //apply overlay layers to terrain
            float[,,] map = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
            for (int i = originalLayersLength; i < map.GetLength(2); i++)
            {
                int originalIndex = overlayIndexToOldIndexMap[i];
                if(!layersToApplyOn[originalIndex]) continue;
                for (int z = 0; z < map.GetLength(1); z++)
                {
                    for (int x = 0; x < map.GetLength(0); x++)
                    {
                        map[x, z, i] = map[x, z, originalIndex] * opacityMultiplier;
                        map[x, z, originalIndex] *= (1 - opacityMultiplier);
                    }
                }
            }

            //apply changes to terrain
            terrainData.SetAlphamaps(0,0, map);
        }
    }
}