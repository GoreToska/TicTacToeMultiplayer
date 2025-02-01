using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Managers;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace LobbySystem
{
    public class LobbyManager : MonoBehaviour
    {
        [HideInInspector] public static LobbyManager Instance { get; private set; }

        [SerializeField] private float timeToRefreshLobbyList = 5;

        public event EventHandler OnLeftLobby;
        public event EventHandler<EventHandlers.LobbyEventArgs> OnJoinedLobby;
        public event EventHandler<EventHandlers.LobbyEventArgs> OnKickedFromLobby;
        public event EventHandler<EventHandlers.LobbyEventArgs> OnCurrentLobbyUpdate;
        public event EventHandler<EventHandlers.LobbyEventArgs> OnLobbyStartGame;
        public event EventHandler<EventHandlers.OnLobbyListChangedEventArgs> OnLobbyListChanged;

        private Allocation relayAllocation = null;
        private Coroutine pollCoroutine = null;
        private Coroutine heartbeatCoroutine = null;
        private Coroutine refreshCoroutine = null;
        private Lobby joinedLobby;

        private string relayCode;
        private float heartbeatFrequency = 10;
        private string playerName;
        private int minPlayersToStart = 2;
        private bool startedRelay = false;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await Authenticate();
            OnLobbyStartGame += StartGameOnClients;
            OnKickedFromLobby += (sender, args) => { DiscardLobby(); };
            OnLeftLobby += (sender, args) => { DiscardLobby(); };
        }

        public void StartLobbiesRefreshRoutine()
        {
            if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshLobbyListRoutine());
        }

        public async void RefreshLobbyList()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions();
                options.Filters = LobbyUtilities.GetAvailableLobbies();
                options.Order = LobbyUtilities.GetOrderByNewestCreated();
                QueryResponse lobbyListQueryResponse = await LobbyService.Instance.QueryLobbiesAsync(options);

                OnLobbyListChanged?.Invoke(this,
                    new EventHandlers.OnLobbyListChangedEventArgs { lobbyList = lobbyListQueryResponse.Results });
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }

        public async void CreateLobby(string lobbyName, int maxPlayers = 2)
        {
            relayAllocation = null;

            try
            {
                startedRelay = true;
                Allocation allocation = await RelayUtilities.CreateRelayAllocation(maxPlayers);
                var joinCode = await RelayUtilities.GetRelayJoinCode(allocation.AllocationId);
                NetworkManager.Singleton.StartHost();

                var startGameParam = LobbyUtilities.GetStartedGameParam("false");
                var relayJoinCodeParam = LobbyUtilities.GetRelayJoinCodeParam(joinCode);

                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Player = LobbyUtilities.CreatePlayer(playerName),
                    Data = new Dictionary<string, DataObject>
                    {
                        { startGameParam.Item1, startGameParam.Item2 },
                        { relayJoinCodeParam.Item1, relayJoinCodeParam.Item2 }
                    }
                };

                joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                heartbeatCoroutine = StartCoroutine(PerformHeartBeat());
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());

                OnJoinedLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void QuickJoinLobby()
        {
            try
            {
                QuickJoinLobbyOptions options = new QuickJoinLobbyOptions
                    { Player = LobbyUtilities.CreatePlayer(playerName) };
                joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());

                OnJoinedLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                NoAvailableLobbiesUI.Instance.Show();
                throw;
            }
        }

        public async void JoinLobbyByCode(string lobbyCode)
        {
            try
            {
                JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
                    { Player = LobbyUtilities.CreatePlayer(playerName) };
                joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());

                OnJoinedLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void JoinLobbyByID(string lobbyID)
        {
            try
            {
                JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
                    { Player = LobbyUtilities.CreatePlayer(playerName) };
                joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyID, options);
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());

                OnJoinedLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void LeaveLobby()
        {
            try
            {
                startedRelay = false;
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);

                if (NetworkManager.Singleton.IsHost)
                    NetworkManager.Singleton.Shutdown();

                OnLeftLobby?.Invoke(this, EventArgs.Empty);
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void KickPlayer(string id)
        {
            try
            {
                if (!LobbyUtilities.IsLobbyHost(joinedLobby))
                    return;

                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, id);
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void DiscardLobby()
        {
            joinedLobby = null;

            if (heartbeatCoroutine != null)
                StopCoroutine(heartbeatCoroutine);

            if (pollCoroutine != null)
                StopCoroutine(pollCoroutine);
        }

        public async void StartGameOnHost()
        {
            try
            {
                if (LobbyUtilities.IsLobbyHost(joinedLobby) &&
                    LobbyUtilities.EnoughPlayers(joinedLobby, minPlayersToStart))
                {
                    Debug.LogWarning("Not enough players!");
                    return;
                }

                var startGameParam = LobbyUtilities.GetStartedGameParam("true");

                joinedLobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { startGameParam.Item1, startGameParam.Item2 }
                    }
                });

                StopCoroutine(pollCoroutine);
                StopCoroutine(refreshCoroutine);
                OnLobbyStartGame?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async void StartGameOnClients(object sender, EventHandlers.LobbyEventArgs lobbyEventArgs)
        {
            try
            {
                if (!LobbyUtilities.IsLobbyHost(joinedLobby))
                {
                    StopCoroutine(refreshCoroutine);
                    await RelayUtilities.StartClientRelay(joinedLobby);
                }

                MultiplayerSceneManager.Instance.LoadScene("GameScene", true);
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async Task Authenticate()
        {
            playerName = "New player";
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Player name - " + playerName);
        }

        public void SetPlayerName(string newName)
        {
            playerName = newName;
        }

        public string GetPlayerName()
        {
            return playerName;
        }

        public Lobby GetJoinedLobby()
        {
            return joinedLobby;
        }

        private async void CreateNewLobbyHost(Lobby lobby)
        {
            try
            {
                startedRelay = true;
                Allocation allocation = await RelayUtilities.CreateRelayAllocation(lobby.MaxPlayers);
                var joinCode = await RelayUtilities.GetRelayJoinCode(allocation.AllocationId);
                var newCodeAllocation = LobbyUtilities.GetRelayJoinCodeParam(joinCode);

                joinedLobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { newCodeAllocation.Item1, newCodeAllocation.Item2 }
                    }
                });

                NetworkManager.Singleton.StartHost();
            }
            catch (RelayServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private IEnumerator LobbyPollForUpdates()
        {
            while (true)
            {
                Task<Lobby> task = LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                yield return new WaitUntil(() => task.IsCompleted);

                joinedLobby = task.Result;

                if (!LobbyUtilities.IsInLobby(joinedLobby))
                {
                    OnKickedFromLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
                    joinedLobby = null;
                    yield break;
                }

                // If previous lobby host disconnected we should become new host  
                if (LobbyUtilities.IsLobbyHost(joinedLobby) && !NetworkManager.Singleton.IsHost && !startedRelay)
                {
                    heartbeatCoroutine ??= StartCoroutine(PerformHeartBeat());
                    CreateNewLobbyHost(joinedLobby);
                }

                relayCode = LobbyUtilities.GetLobbyRelayCode(joinedLobby);
                OnCurrentLobbyUpdate?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });

                if (LobbyUtilities.StartedGame(joinedLobby))
                {
                    relayCode = LobbyUtilities.GetLobbyRelayCode(joinedLobby);
                    OnLobbyStartGame?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
                    yield break;
                }

                yield return new WaitForSeconds(1);
            }
        }

        private IEnumerator PerformHeartBeat()
        {
            while (true)
            {
                yield return new WaitForSeconds(heartbeatFrequency);

                if (joinedLobby == null)
                    yield break;

                LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }

        private IEnumerator RefreshLobbyListRoutine()
        {
            while (true)
            {
                RefreshLobbyList();
                yield return new WaitForSeconds(timeToRefreshLobbyList);
            }

            yield break;
        }
    }
}