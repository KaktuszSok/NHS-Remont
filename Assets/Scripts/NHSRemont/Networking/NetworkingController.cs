using NHSRemont.Gameplay;
using NHSRemont.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkingController : MonoBehaviour
{
    private NetworkManager network;

    public static ServerSettings settings = new ServerSettings("TestFlat");

    private void Awake()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton != GetComponent<NetworkManager>())
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        network = NetworkManager.Singleton;

        network.OnServerStarted += InitialiseServer;
        network.OnClientDisconnectCallback += OnClientDisconnected;
        network.OnClientConnectedCallback += OnClientConnected;
    }
    
    private void Update() //TODO make buttons for this instead of this temporary hotkeys
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (!network.IsClient && !network.IsServer)
            {
                network.StartHost();
            }
        }
        
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (!network.IsClient && !network.IsServer)
            {
                network.StartClient();
            }
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            ShutdownServer();
        }
    }

    public void ShutdownServer()
    {
        if(GameManager.instance)
            GameManager.instance.persistence.PersistLoadedSceneState();
        
        network.Shutdown();
        GameManager.ReturnToMenu();
    }

    private void InitialiseServer()
    {
        network.SceneManager.OnSynchronizeComplete += OnSceneSynchronised;
        network.SceneManager.OnLoadComplete += (id, sceneName, mode) =>
        {
            OnSceneSynchronised(id);
        };
        network.SceneManager.LoadScene(settings.mapName, LoadSceneMode.Single);
    }

    private void OnClientConnected(ulong id)
    {
        Debug.Log("client connected with id " + id);
    }

    private void OnClientDisconnected(ulong id)
    {
        Debug.Log("id=" + id + "/us=" + network.LocalClientId + " or " + network.ServerClientId);
        if(network.LocalClientId == id) //we have disconnected
            GameManager.ReturnToMenu();
    }

    private void OnSceneSynchronised(ulong id)
    {
        if (network.IsServer)
        {
            GameManager.instance.SynchroniseMapClientRpc(GameManager.instance.persistence, NetworkingUtils.SendToSingleClient(id));
        }
    }
}
