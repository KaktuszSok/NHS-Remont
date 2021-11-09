using UnityEngine;

namespace NHSRemont.Environment
{
    /// <summary>
    /// Copies terrain data on awake so as to not mess up the terrain files after exiting play mode
    /// </summary>
    [RequireComponent(typeof(Terrain))]
    public class RuntimeTerrain : MonoBehaviour
    {
        private Terrain terrain;
        
        private void Awake()
        {
            terrain = GetComponent<Terrain>();
            terrain.terrainData = Instantiate(terrain.terrainData);
            GetComponent<TerrainCollider>().terrainData = terrain.terrainData;
        }
    }
}