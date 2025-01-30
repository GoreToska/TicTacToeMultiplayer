using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine.Serialization;

public class LoadingProgressManager : NetworkBehaviour
{
    [SerializeField] private GameObject progressTrackerPrefab;
    [SerializeField] private int playersCountToWait = 2;

    public Dictionary<ulong, NetworkedLoadingProgressTracker> ProgressTrackers { get; } =
        new Dictionary<ulong, NetworkedLoadingProgressTracker>();

    private AsyncOperation localLoadOperation;
    float localProgress;
    bool isLoading;

    private int playersLoaded = 0;
    public event EventHandler OnLoadingEnded;
    public event EventHandler OnAllPlayersLoaded;
    public event Action onTrackersUpdated;

    public AsyncOperation LocalLoadOperation
    {
        set
        {
            isLoading = true;
            LocalProgress = 0;
            localLoadOperation = value;
        }
    }

    public float LocalProgress
    {
        get
        {
            return IsSpawned && ProgressTrackers.ContainsKey(NetworkManager.LocalClientId)
                ? ProgressTrackers[NetworkManager.LocalClientId].Progress.Value
                : localProgress;
        }
        private set
        {
            if (IsSpawned && ProgressTrackers.ContainsKey(NetworkManager.LocalClientId) &&
                ProgressTrackers[NetworkManager.LocalClientId].IsSpawned)
            {
                ProgressTrackers[NetworkManager.LocalClientId].Progress.Value = value;
            }
            else
            {
                localProgress = value;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += AddLoadingTracker;
            NetworkManager.OnClientDisconnectCallback += RemoveLoadingTracker;
            AddLoadingTracker(NetworkManager.LocalClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback -= AddLoadingTracker;
            NetworkManager.OnClientDisconnectCallback -= RemoveLoadingTracker;
        }

        ProgressTrackers.Clear();
        onTrackersUpdated?.Invoke();
    }

    private void Update()
    {
        if (localLoadOperation == null || !isLoading)
            return;

        Debug.Log($"Players count = {NetworkManager.ConnectedClientsList.Count}");

        LocalProgress = localLoadOperation.progress;

        if (isLoading && localLoadOperation.isDone)
        {
            isLoading = false;
            LocalProgress = 1;
            OnLoadingEnded?.Invoke(this, EventArgs.Empty);
            ClientLoadedRPC();
        }
    }

    [Rpc(SendTo.Server)]
    private void ClientLoadedRPC()
    {
        playersLoaded++;

        if (playersLoaded >= playersCountToWait)
        {
            AllClientsLoadedRPC();
            MultiplayerSceneManager.Instance.AllPlayersLoadedRPC();
            playersLoaded = 0;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void AllClientsLoadedRPC()
    {
        OnAllPlayersLoaded?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveLoadingTracker(ulong clientId)
    {
        if (IsServer && !NetworkManager.ShutdownInProgress)
        {
            if (!ProgressTrackers.ContainsKey(clientId))
                return;

            NetworkedLoadingProgressTracker tracker = ProgressTrackers[clientId];
            ProgressTrackers.Remove(clientId);
            if (NetworkObject && tracker.NetworkObject.IsSpawned)
                tracker.NetworkObject.Despawn();
            UpdateTrackersRPC();
        }
    }

    private void AddLoadingTracker(ulong clientId)
    {
        if (IsServer)
        {
            GameObject tracker = Instantiate(progressTrackerPrefab);
            NetworkObject networkObject = tracker.GetComponent<NetworkObject>();
            networkObject.SpawnWithOwnership(clientId);
            ProgressTrackers[clientId] = tracker.GetComponent<NetworkedLoadingProgressTracker>();
            UpdateTrackersRPC();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateTrackersRPC()
    {
        if (!IsHost)
        {
            ProgressTrackers.Clear();

            // find objects of type is bad, but it is not gonna be called often, so it's okay... i think?
            foreach (var tracker in FindObjectsByType<NetworkedLoadingProgressTracker>(FindObjectsSortMode.None))
            {
                if (!tracker.IsSpawned)
                    continue;

                ProgressTrackers[tracker.OwnerClientId] = tracker;

                if (tracker.OwnerClientId != NetworkManager.LocalClientId)
                    continue;

                LocalProgress = Mathf.Max(localProgress, LocalProgress);
            }
        }

        onTrackersUpdated?.Invoke();
    }
}