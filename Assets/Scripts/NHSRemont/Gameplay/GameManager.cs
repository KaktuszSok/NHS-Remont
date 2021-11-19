using System.Collections;
using System.Diagnostics;
using NHSRemont.Entity;
using NHSRemont.Environment.Fractures;
using NHSRemont.Environment.Terrain;
using NHSRemont.Networking;
using NHSRemont.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace NHSRemont.Gameplay
{
    public class GameManager : NetworkBehaviour
    {
        /// <summary>
        /// When syncing explosion effects, we will sync explosions until this amount of time has been spent on them in the current frame.
        /// Then, wait for next frame and repeat until all explosions synced.
        /// </summary>
        private const int maxMillisecondsPerFrameForExplosionsSync = 4;
        
        public static GameManager instance;

        private static MapPersistence testPersistence = new MapPersistence();
        public MapPersistence persistence //= new MapPersistence();
        {
            get => testPersistence;
            set => testPersistence = value;
        }

        private bool mapAlreadySynchronised = false;

        private void Awake()
        {
            if (NetworkManager.Singleton == null)
            {
                NetworkingController.settings.mapName = SceneManager.GetActiveScene().name;
                ReturnToMenu();
                return;
            }
            
            instance = this;
        }

        private void Start()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                PhysicsManager.instance.onExplosion += e => persistence.explosionsHistory.Add(e);
            }

            RespawnPlayerRandomly();
        }

        /// <summary>
        /// Respawns the local player randomly
        /// </summary>
        public void RespawnPlayerRandomly()
        {
            NetworkObject playerGO = NetworkManager.SpawnManager.GetLocalPlayerObject();
            var spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
            playerGO.GetComponent<Player>().Respawn(spawnPoints.ChooseRandom().transform);
        }

        /// <summary>
        /// Synchronises the loaded map to match the given persistence state
        /// </summary>
        [ClientRpc]
        public void SynchroniseMapClientRpc(MapPersistence mapPersistence, ClientRpcParams parameters)
        {
            if (mapAlreadySynchronised)
            {
                Debug.LogWarning("Map is already synchronised! Not synchronising again.");
                return;
            }
            mapAlreadySynchronised = true;
            
            //persistence = mapPersistence;
            StartCoroutine(SynchroniseMapAfterInitialisation());
        }

        /// <summary>
        /// Waits for Awake, Start, etc. to be called before synchronising the map
        /// </summary>
        private IEnumerator SynchroniseMapAfterInitialisation()
        {
            yield return new WaitForFixedUpdate();

            persistence.ApplyLoadedSceneState();
            StartCoroutine(SyncExplosionsDistributed(persistence.explosionsHistory.ToArray()));
        }

        private IEnumerator SyncExplosionsDistributed(ExplosionInfo[] explosions)
        {
            var terrains = FindObjectsOfType<ReactiveTerrain>();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int x = 0;
            foreach (ExplosionInfo explosionInfo in explosions)
            {
                foreach (ReactiveTerrain reactiveTerrain in terrains)
                {
                    reactiveTerrain.OnExplosion(explosionInfo);
                    x++;
                    if (stopwatch.ElapsedMilliseconds > maxMillisecondsPerFrameForExplosionsSync)
                    {
                        stopwatch.Stop();
                        Debug.Log("explosions this frame: " + x);
                        x = 0;
                        yield return null; //enough explosions for this frame.
                        stopwatch.Restart();
                    }
                }
            }
            stopwatch.Stop();
        }
        
        public static void ReturnToMenu()
        {
            SceneManager.LoadScene("Menu");
        }
    }
}
