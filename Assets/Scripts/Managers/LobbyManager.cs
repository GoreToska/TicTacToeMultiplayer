using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Managers;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
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

        public const string KeyPlayerName = "PlayerName";
        public const string RelayJoinCodeName = "KeyRelayJoinCode";
        public const string StartedGameName = "KeyStartedGame";

        private Allocation relayAllocation = null;
        private string relayCode;
        private Coroutine pollCoroutine = null;
        private Coroutine refreshCoroutine = null;

        private Lobby hostLobby;
        private Lobby joinedLobby;
        private float timeToWait = 10;
        private string playerName;
        private int maxPlayers = 2;
        private int minPlayersToStart = 2;

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
            playerName = "New player";
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("Player name - " + playerName);
            OnLobbyStartGame += StartGameOnEachClient;
        }

        public void StartLobbiesRefreshRoutine()
        {
            refreshCoroutine = StartCoroutine(RefreshLobbyListRoutine());
        }

        public async void RefreshLobbyList()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions();

                // show only available lobbies (that have one empty slot) 
                options.Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op: QueryFilter.OpOptions.GT,
                        value: "0")
                };

                // Order by newest lobbies first
                options.Order = new List<QueryOrder>
                {
                    new QueryOrder(
                        asc: false,
                        field: QueryOrder.FieldOptions.Created)
                };

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
                this.maxPlayers = maxPlayers;
                string connectionType = "dtls";
                relayAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
                var relayServerData = relayAllocation.ToRelayServerData(connectionType);
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(relayAllocation.AllocationId);
                NetworkManager.Singleton.StartHost();
                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Player = CreatePlayer(),
                    Data = new Dictionary<string, DataObject>
                    {
                        { StartedGameName, new DataObject(DataObject.VisibilityOptions.Member, "false") },
                        { RelayJoinCodeName, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                    }
                };

                hostLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, this.maxPlayers, options);
                joinedLobby = hostLobby;
                StartCoroutine(PerformHeartBeat());
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());
                OnJoinedLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void GetLobbiesList()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions()
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    },
                    Order = new List<QueryOrder>
                    {
                        new QueryOrder(false, QueryOrder.FieldOptions.Created)
                    }
                };

                QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
                Debug.Log("Lobbies found - " + response.Results.Count);

                foreach (var lobby in response.Results)
                {
                    Debug.Log(lobby.Name + " " + lobby.MaxPlayers);
                }
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
                QuickJoinLobbyOptions options = new QuickJoinLobbyOptions { Player = CreatePlayer() };
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
                JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions { Player = CreatePlayer() };
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
                JoinLobbyByIdOptions options = new JoinLobbyByIdOptions { Player = CreatePlayer() };
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

        public async void UpdateLobbyPlayerName(string newName)
        {
            try
            {
                playerName = newName;
                await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId,
                    new UpdatePlayerOptions
                    {
                        Data = new Dictionary<string, PlayerDataObject>
                        {
                            { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, newName) }
                        }
                    });

                OnCurrentLobbyUpdate?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void SetPlayerName(string newName)
        {
            playerName = newName;
        }

        public string GetPlayerName()
        {
            return playerName;
        }

        public async void LeaveLobby()
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                if (hostLobby != null)
                    NetworkManager.Singleton.Shutdown();

                joinedLobby = null;
                hostLobby = null;
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
                if (!IsLobbyHost())
                    return;

                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, id);
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public Player CreatePlayer()
        {
            return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject>
            {
                { KeyPlayerName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
            });
        }

        public void DiscardLobby()
        {
            hostLobby = null;
            joinedLobby = null;
        }

        public bool IsLobbyHost()
        {
            return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
        }

        private bool IsPlayerInLobby()
        {
            if (joinedLobby != null && joinedLobby.Players != null)
            {
                return joinedLobby.Players.Any(player => player.Id == AuthenticationService.Instance.PlayerId);
            }

            return false;
        }

        public Lobby GetJoinedLobby()
        {
            return joinedLobby;
        }

        public async void HostStartGame()
        {
            try
            {
                Debug.Log("Host game method");
                hostLobby = await LobbyService.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { StartedGameName, new DataObject(DataObject.VisibilityOptions.Member, "true") }
                    }
                });

                joinedLobby = hostLobby;
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

        public async void StartGameOnEachClient(object sender, EventHandlers.LobbyEventArgs lobbyEventArgs)
        {
            try
            {
                if (joinedLobby.Players.Count < minPlayersToStart)
                {
                    Debug.LogWarning("Not enough players!");
                    return;
                }

                if (joinedLobby.HostId != AuthenticationService.Instance.PlayerId)
                {
                    StopCoroutine(refreshCoroutine);
                    string connectionType = "dtls";
                    Debug.Log(relayCode);
                    var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
                    var relayServerData = AllocationUtils.ToRelayServerData(joinAllocation, connectionType);
                    NetworkManager.Singleton.GetComponent<UnityTransport>()
                        .SetRelayServerData(relayServerData);
                    var result = NetworkManager.Singleton.StartClient();
                    Debug.Log(result);
                }

                MultiplayerSceneManager.Instance.LoadScene("GameScene", true);
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private IEnumerator LobbyPollForUpdates()
        {
            while (true)
            {
                yield return new WaitForSeconds(1);

                if (!IsPlayerInLobby())
                {
                    Debug.Log("Kicked from Lobby!");
                    OnKickedFromLobby?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
                    joinedLobby = null;
                    yield break;
                }

                Task<Lobby> task = LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                yield return new WaitUntil(() => task.IsCompleted);

                if (joinedLobby == null || LobbyService.Instance == null)
                {
                    Debug.LogError("Something is wrong");
                    yield break;
                }

                joinedLobby = task.Result;
                relayCode = joinedLobby.Data[RelayJoinCodeName].Value;
                OnCurrentLobbyUpdate?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });


                if (joinedLobby.Data[StartedGameName].Value == "true")
                {
                    relayCode = joinedLobby.Data[RelayJoinCodeName].Value;
                    OnLobbyStartGame?.Invoke(this, new EventHandlers.LobbyEventArgs { Lobby = joinedLobby });
                    yield break;
                }
            }
        }

        private IEnumerator PerformHeartBeat()
        {
            while (hostLobby != null)
            {
                yield return new WaitForSeconds(timeToWait);

                if (hostLobby != null)
                    LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
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