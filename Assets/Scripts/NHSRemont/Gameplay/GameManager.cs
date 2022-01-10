using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NHSRemont.Environment.Terrain;
using NHSRemont.Networking;
using NHSRemont.Utility;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace NHSRemont.Gameplay
{
    //TODO split this into smaller classes with this one as the "hub" to access them through
    public class GameManager : MonoBehaviourPunCallbacks
    {
        /// <summary>
        /// When syncing terrain events effects, we will sync explosions until this amount of time has been spent on them in the current frame.
        /// Then, wait for next frame and repeat until all events synced.
        /// </summary>
        private const int maxMillisecondsPerFrameForTerrainEventsSync = 4;
        private readonly Stopwatch terrainEventsProcessingStopwatch = new Stopwatch();

        public static GameManager instance;

        public GameplayReferences gameplayReferences;

        //private static MapPersistence testPersistence = new MapPersistence();
        public MapPersistence persistence = new MapPersistence();
        // {
        //     get => testPersistence;
        //     set => testPersistence = value;
        // }

        private bool mapAlreadySynchronised = false;
        private bool canBeginProcessingEvents = false;

        private ReactiveTerrain[] terrains;
        private readonly Queue<(ITerrainEvent terrainEvent, bool isNewEvent)> terrainEventsQueue = new(); //isNewEvent is false for events added from the MapPersistence terrain event history

        private void Awake()
        {
            if (!PhotonNetwork.IsConnected)
            {
                NetworkingController.settings.mapIndex = SceneManager.GetActiveScene().buildIndex;
                ReturnToMenu();
                return;
            }
            
            instance = this;
        }

        private void Start()
        {
            terrains = FindObjectsOfType<ReactiveTerrain>();
            GameObject playerGO = PhotonNetwork.Instantiate("Player", Vector3.zero, Quaternion.identity);
            PhotonNetwork.LocalPlayer.TagObject = playerGO.GetComponent<Entity.Player>();
            RespawnPlayerRandomly();

            if (PhotonNetwork.IsMasterClient)
            {
                SynchroniseMap(persistence);
            }
        }

        private void Update()
        {
            if(terrainEventsQueue.Count == 0 || !canBeginProcessingEvents)
                return;
            
            int eventsThisFrame = 0;
            terrainEventsProcessingStopwatch.Restart();
            while (terrainEventsQueue.TryDequeue(out (ITerrainEvent terrainEvent, bool isNewEvent) entry))
            {
                int idx = entry.terrainEvent.AffectedTerrain();
                bool didSomething = entry.terrainEvent.Apply(terrains[idx]);
                if (didSomething && entry.isNewEvent)
                {
                    persistence.terrainEventsHistory.Add(entry.terrainEvent);
                }

                eventsThisFrame++;
                if (terrainEventsProcessingStopwatch.ElapsedMilliseconds > maxMillisecondsPerFrameForTerrainEventsSync)
                {
                    terrainEventsProcessingStopwatch.Stop();
                    Debug.Log("events this frame: " + eventsThisFrame);
                    break; //enough events for this frame.
                }
            }
            terrainEventsProcessingStopwatch.Stop();
        }

        /// <summary>
        /// Respawns the local player randomly
        /// </summary>
        public void RespawnPlayerRandomly()
        {
            Entity.Player player = (Entity.Player)PhotonNetwork.LocalPlayer.TagObject;
            var spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
            player.Respawn(spawnPoints.ChooseRandom().transform);
        }

        /// <summary>
        /// Synchronises the loaded map to match the given persistence state
        /// </summary>
        [PunRPC]
        public void SynchroniseMap(MapPersistence mapPersistence)
        {
            if (mapAlreadySynchronised)
            {
                Debug.LogWarning("Map is already synchronised! Not synchronising again.");
                return;
            }
            mapAlreadySynchronised = true;
            
            persistence = mapPersistence;
            StartCoroutine(SynchroniseMapAfterInitialisation());
        }

        /// <summary>
        /// Waits for Awake, Start, etc. to be called before synchronising the map
        /// </summary>
        private IEnumerator SynchroniseMapAfterInitialisation()
        {
            yield return new WaitForFixedUpdate();

            //sync terrain events
            var enqueuedBeforeSync = new Queue<(ITerrainEvent terrainEvent, bool isNewEvent)>(terrainEventsQueue);
            terrainEventsQueue.Clear();
            foreach (ITerrainEvent terrainEvent in persistence.terrainEventsHistory)
            {
                terrainEventsQueue.Enqueue((terrainEvent, false));
            }
            foreach (var entry in enqueuedBeforeSync)
            {
                terrainEventsQueue.Enqueue(entry);
            }

            canBeginProcessingEvents = true;
        }

        public void EnqueueTerrainEvent(ITerrainEvent terrainEvent)
        {
            terrainEventsQueue.Enqueue((terrainEvent, true));
        }

        public static void ReturnToMenu()
        {
            SceneManager.LoadScene("Menu");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        #region Callbacks

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            base.OnPlayerEnteredRoom(newPlayer);
            photonView.RPC(nameof(SynchroniseMap), newPlayer,
                persistence);
        }

        #endregion
    }
}
