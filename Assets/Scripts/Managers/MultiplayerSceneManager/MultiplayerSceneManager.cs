using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Managers;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerSceneManager : NetworkBehaviour
{
    [HideInInspector] public static MultiplayerSceneManager Instance { get; private set; }

    [SerializeField] private ClientLoadingScreen clientLoadingScreen;
    [SerializeField] private LoadingProgressManager loadingProgressManager;

    private bool IsNetworkSceneManagementEnabled => NetworkManager != null && NetworkManager.SceneManager != null &&
                                                    NetworkManager.NetworkConfig.EnableSceneManagement;

    public event EventHandler OnAllPlayersLoaded;

    private string sceneToLoad;
    private Transform loadingScreen;
    private bool isInitialized;
    private bool usingNetwork = false;

    public class SceneLoadingEventArgs : EventArgs
    {
        public string SceneName;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        DontDestroyOnLoad(this);
    }

    public virtual void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        NetworkManager.OnServerStarted += OnNetworkingSessionStarted;
        NetworkManager.OnClientStarted += OnNetworkingSessionStarted;
        NetworkManager.OnServerStopped += OnNetworkingSessionEnded;
        NetworkManager.OnClientStopped += OnNetworkingSessionEnded;
    }

    public override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (NetworkManager != null)
        {
            NetworkManager.OnServerStarted -= OnNetworkingSessionStarted;
            NetworkManager.OnClientStarted -= OnNetworkingSessionStarted;
            NetworkManager.OnServerStopped -= OnNetworkingSessionEnded;
            NetworkManager.OnClientStopped -= OnNetworkingSessionEnded;
        }

        base.OnDestroy();
    }

    /// <summary>
    /// Loads a scene asynchronously using the specified loadSceneMode, with NetworkSceneManager if on a listening
    /// server with SceneManagement enabled, or SceneManager otherwise. If a scene is loaded via SceneManager, this
    /// method also triggers the start of the loading screen.
    /// </summary>
    /// <param name="sceneName">Name or path of the Scene to load.</param>
    /// <param name="useNetworkSceneManager">If true, uses NetworkSceneManager, else uses SceneManager</param>
    /// <param name="loadSceneMode">If LoadSceneMode.Single then all current Scenes will be unloaded before loading.</param>
    public virtual void LoadScene(string sceneName, bool useNetworkSceneManager,
        LoadSceneMode loadSceneMode = LoadSceneMode.Single)
    {
        usingNetwork = useNetworkSceneManager;
        if (useNetworkSceneManager)
        {
            if (IsSpawned && IsNetworkSceneManagementEnabled && !NetworkManager.ShutdownInProgress)
            {
                if (NetworkManager.IsServer)
                {
                    // If is active server and NetworkManager uses scene management, load scene using NetworkManager's SceneManager
                    NetworkManager.SceneManager.LoadScene(sceneName, loadSceneMode);
                }
            }
        }
        else
        {
            // Load using SceneManager
            var loadOperation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);

            if (loadSceneMode == LoadSceneMode.Single)
            {
                clientLoadingScreen.StartLoadingScreen(sceneName);
                loadingProgressManager.LocalLoadOperation = loadOperation;
            }
        }
    }

    public void LoadScene(int sceneIndex, bool useNetworkSceneManager,
        LoadSceneMode loadSceneMode = LoadSceneMode.Single)
    {
        this.LoadScene(SceneManager.GetSceneByBuildIndex(sceneIndex).name, useNetworkSceneManager, loadSceneMode);
    }

    private void OnNetworkingSessionStarted()
    {
        // Prevents this to be called twice on a host, which receives both OnServerStarted and OnClientStarted callbacks
        if (!isInitialized)
        {
            if (IsNetworkSceneManagementEnabled)
            {
                NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
            }

            isInitialized = true;
        }
    }

    private void OnNetworkingSessionEnded(bool obj)
    {
        if (isInitialized)
        {
            if (IsNetworkSceneManagementEnabled)
            {
                NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            }

            isInitialized = false;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.Load: // Server told client to load a scene
                // Only executes on client or host
                if (NetworkManager.IsClient)
                {
                    // Only start a new loading screen if scene loaded in Single mode, else simply update
                    if (sceneEvent.LoadSceneMode == LoadSceneMode.Single)
                    {
                        Debug.Log("Started loading");
                        clientLoadingScreen.StartLoadingScreen(sceneEvent.SceneName);
                        loadingProgressManager.LocalLoadOperation = sceneEvent.AsyncOperation;
                    }
                    else
                    {
                        clientLoadingScreen.UpdateLoadingScreen(sceneEvent.SceneName);
                        loadingProgressManager.LocalLoadOperation = sceneEvent.AsyncOperation;
                    }
                }

                break;

            case SceneEventType.LoadEventCompleted: // Server told client that all clients finished loading a scene
                // Only executes on client or host
                if (NetworkManager.IsClient)
                {
                    clientLoadingScreen.CompleteLoading();
                }

                break;

            case SceneEventType.Synchronize: // Server told client to start synchronizing scenes
            {
                // Only executes on client that is not the host
                if (NetworkManager.IsClient && !NetworkManager.IsHost)
                {
                    if (NetworkManager.SceneManager.ClientSynchronizationMode == LoadSceneMode.Single)
                    {
                        // If using the Single ClientSynchronizationMode, unload all currently loaded additive
                        // scenes. In this case, we want the client to only keep the same scenes loaded as the
                        // server. Netcode For GameObjects will automatically handle loading all the scenes that the
                        // server has loaded to the client during the synchronization process. If the server's main
                        // scene is different to the client's, it will start by loading that scene in single mode,
                        // unloading every additively loaded scene in the process. However, if the server's main
                        // scene is the same as the client's, it will not automatically unload additive scenes, so
                        // we do it manually here.
                        UnloadAdditiveScenes();
                    }
                }

                break;
            }
            case SceneEventType.SynchronizeComplete: // Client told server that they finished synchronizing
                // Only executes on server
                if (NetworkManager.IsServer)
                {
                    // Send client RPC to make sure the client stops the loading screen after the server handles what it needs to after the client finished synchronizing, for example character spawning done server side should still be hidden by loading screen.
                    ClientStopLoadingScreenRpc(RpcTarget.Group(new[] { sceneEvent.ClientId }, RpcTargetUse.Temp));
                }

                break;
        }
    }

    private void UnloadAdditiveScenes()
    {
        var activeScene = SceneManager.GetActiveScene();
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene != activeScene)
            {
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ClientStopLoadingScreenRpc(RpcParams clientRpcParams = default)
    {
        clientLoadingScreen.CompleteLoading();
    }

    private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
    {
        if (!IsSpawned || NetworkManager.ShutdownInProgress || !usingNetwork)
        {
            clientLoadingScreen.HideWaitingScreen();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void AllPlayersLoadedRPC()
    {
        OnAllPlayersLoaded?.Invoke(this, EventArgs.Empty);
    }
}