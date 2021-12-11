using System.Collections.Generic;
using ExitGames.Client.Photon;
using NHSRemont.Gameplay;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NHSRemont.Networking
{
    public class NetworkingController : MonoBehaviour, IMatchmakingCallbacks, IConnectionCallbacks
    {
        public static NetworkingController instance;
        public static NHSRoomSettings settings = new NHSRoomSettings(3);

        private void Awake()
        {
            if (instance != null)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.AddCallbackTarget(this);
            PhotonPeer.RegisterType(typeof(MapPersistence), MapPersistence.typeId, MapPersistence.Serialise, MapPersistence.Deserialise);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            PhotonNetwork.ConnectUsingSettings();
        }

        private void Update() //TODO make buttons for this instead of this temporary hotkeys
        {
            if (Input.GetKeyDown(KeyCode.J))
            {
                if (!PhotonNetwork.IsConnected)
                {
                    PhotonNetwork.ConnectUsingSettings();
                }
            }

            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                PhotonNetwork.OfflineMode = !PhotonNetwork.OfflineMode;
            }
        
            if (Input.GetKeyDown(KeyCode.H))
            {
                QuickPlay();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                ShutdownServerOrClient();
            }
        }

        public void ShutdownServerOrClient()
        {
            if(PhotonNetwork.IsMasterClient && GameManager.instance != null)
                GameManager.instance.persistence.PersistLoadedSceneState();
        
            PhotonNetwork.Disconnect();
            GameManager.ReturnToMenu();
        }

        public void QuickPlay()
        {
            if (!PhotonNetwork.InRoom)
            {
                PhotonNetwork.JoinOrCreateRoom("testroom", new RoomOptions(), TypedLobby.Default);
            }
        }

        #region Callbacks
        public void OnCreatedRoom()
        {
            Debug.Log("created room!");
            PhotonNetwork.LoadLevel(settings.mapIndex);
        }
    
        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.Log("Shit");
        }

        public void OnJoinedRoom()
        {
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.Log("Shit");
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log("Shit");
        }

        public void OnLeftRoom()
        {
        }

        public void OnConnected()
        {
            Debug.Log("Connected!");
        }

        public void OnConnectedToMaster()
        {
            QuickPlay();
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log("Disconnected.");
            SceneManager.LoadScene("Menu");
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
        }
        #endregion
    }
}
