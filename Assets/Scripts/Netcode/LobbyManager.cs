using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace LobbySystem
{
    public class LobbyManager : MonoBehaviour
    {
        [HideInInspector] public static LobbyManager Instance { get; private set; }

        public event EventHandler OnLeftLobby;
        public event EventHandler<LobbyEventArgs> OnJoinedLobby;
        public event EventHandler<LobbyEventArgs> OnKickedFromLobby;
        public event EventHandler<LobbyEventArgs> OnCurrentLobbyUpdate;
        public event EventHandler<LobbyEventArgs> OnLobbyStartGame;
        public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;

        public const string KeyPlayerName = "PlayerName";
        public const string RelayJoinCodeName = "KeyRelayJoinCode";
        public const string StartedGameName = "KeyStartedGame";

        private Allocation relayAllocation = null;
        private bool isStartedGame = false;

        private Coroutine pollCoroutine = null;

        public class LobbyEventArgs : EventArgs
        {
            public Lobby Lobby;
        }

        public class OnLobbyListChangedEventArgs : EventArgs
        {
            public List<Lobby> lobbyList;
        }

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

        public async void Authenticate(string playerName)
        {
            this.playerName = playerName;
            InitializationOptions initializationOptions = new InitializationOptions();
            initializationOptions.SetProfile(playerName);

            await UnityServices.InitializeAsync(initializationOptions);

            AuthenticationService.Instance.SignedIn += () =>
            {
                // do nothing
                Debug.Log("Signed in! " + AuthenticationService.Instance.PlayerId);

                RefreshLobbyList();
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
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

                QueryResponse lobbyListQueryResponse = await Lobbies.Instance.QueryLobbiesAsync(options);

                OnLobbyListChanged?.Invoke(this,
                    new OnLobbyListChangedEventArgs { lobbyList = lobbyListQueryResponse.Results });
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
                relayAllocation = await AllocateRelay(maxPlayers);
                string relayJoinCode = await GetRelayJoinCode(relayAllocation);
                this.maxPlayers = maxPlayers;
                NetworkManager.Singleton.GetComponent<UnityTransport>()
                    .SetRelayServerData(new RelayServerData(relayAllocation, "dtls"));

                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Player = CreatePlayer(),
                    Data = new Dictionary<string, DataObject>
                    {
                        { StartedGameName, new DataObject(DataObject.VisibilityOptions.Member, "false") },
                        { RelayJoinCodeName, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                };

                hostLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, this.maxPlayers, options);
                joinedLobby = hostLobby;
                StartCoroutine(PerformHeartBeat());
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
                Debug.Log($"Creating lobby - {hostLobby != null}");
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private async Task<Allocation> AllocateRelay(int maxPlayers = 2)
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
                return allocation;
            }
            catch (RelayServiceException e)
            {
                Console.WriteLine(e);
                return default;
            }
        }

        private async Task<string> GetRelayJoinCode(Allocation allocation)
        {
            try
            {
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                return joinCode;
            }
            catch (RelayServiceException e)
            {
                Console.WriteLine(e);
                return default;
            }
        }

        private async Task<JoinAllocation> JoinRelay(string joinCode)
        {
            try
            {
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                return allocation;
            }
            catch (RelayServiceException e)
            {
                Console.WriteLine(e);
                return default;
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

                QueryResponse response = await Lobbies.Instance.QueryLobbiesAsync(options);
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
                PrintPlayers(joinedLobby);
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void JoinLobbyByCode(string lobbyCode)
        {
            try
            {
                JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions { Player = CreatePlayer() };
                joinedLobby = await Lobbies.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
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
                joinedLobby = await Lobbies.Instance.JoinLobbyByIdAsync(lobbyID, options);
                pollCoroutine = StartCoroutine(LobbyPollForUpdates());
                OnJoinedLobby?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void PrintPlayers(Lobby lobby)
        {
            Debug.Log("Players in a lobby - " + lobby.Name + " players count - " + lobby.Players.Count);

            foreach (var player in lobby.Players)
            {
                Debug.Log(player.Id + " - id " + player.Data["PlayerName"].Value + " name");
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

                OnCurrentLobbyUpdate?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
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

        public async void MigrateLobbyHost(int playerId)
        {
            try
            {
                if (!IsLobbyHost())
                    return;

                hostLobby = await Lobbies.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
                {
                    HostId = hostLobby.Players[playerId].Id,
                });

                joinedLobby = hostLobby;
                hostLobby = null;
            }
            catch (LobbyServiceException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void DeleteLobby()
        {
            try
            {
                if (!IsLobbyHost())
                    return;

                await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
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
                hostLobby = await Lobbies.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { StartedGameName, new DataObject(DataObject.VisibilityOptions.Member, "true") }
                    }
                });

                joinedLobby = hostLobby;
                StopCoroutine(pollCoroutine);
                OnLobbyStartGame?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void StartGameOnEachClient(object sender, LobbyEventArgs lobbyEventArgs)
        {
            try
            {
                Debug.Log("Starting game on each client!");
                if (joinedLobby.Players.Count < minPlayersToStart)
                {
                    Debug.LogWarning("Not enough players!");
                    return;
                }

                if (joinedLobby.HostId == AuthenticationService.Instance.PlayerId)
                {
                    var result = NetworkManager.Singleton.StartHost();
                    Debug.Log(result);
                }
                else
                {
                    string relayJoinCode = joinedLobby.Data[RelayJoinCodeName].Value;
                    JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);
                    NetworkManager.Singleton.GetComponent<UnityTransport>()
                        .SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
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
                    OnKickedFromLobby?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
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
                OnCurrentLobbyUpdate?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });

                Debug.Log($"Checking game for start - {joinedLobby.Data[StartedGameName].Value}");

                if (joinedLobby.Data[StartedGameName].Value == "true")
                {
                    OnLobbyStartGame?.Invoke(this, new LobbyEventArgs { Lobby = joinedLobby });
                    Debug.Log("Break!!!!!!!");
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
    }
}