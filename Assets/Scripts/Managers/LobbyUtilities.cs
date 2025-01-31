using System.Collections.Generic;
using System.Linq;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;

namespace LobbySystem
{
    public static class LobbyUtilities
    {
        private const string KeyPlayerName = "PlayerName";
        private const string StartedGameName = "KeyStartedGame";
        private const string RelayJoinCodeName = "KeyRelayJoinCode";

        public static List<QueryFilter> GetAvailableLobbies()
        {
            return new List<QueryFilter>
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };
        }

        public static List<QueryOrder> GetOrderByNewestCreated()
        {
            return new List<QueryOrder>
            {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };
        }

        public static Player CreatePlayer(string name)
        {
            return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject>
            {
                { KeyPlayerName, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, name) },
            });
        }

        public static bool IsLobbyHost(Lobby lobby)
        {
            return lobby != null && lobby.HostId == AuthenticationService.Instance.PlayerId;
        }

        public static bool IsInLobby(Lobby lobby)
        {
            if (lobby != null && lobby.Players != null)
            {
                return lobby.Players.Any(player => player.Id == AuthenticationService.Instance.PlayerId);
            }

            return false;
        }

        public static bool StartedGame(Lobby lobby)
        {
            return lobby.Data[StartedGameName].Value == "true";
        }

        public static bool EnoughPlayers(Lobby lobby, int minPlayersToStart)
        {
            return lobby.Players.Count < minPlayersToStart;
        }

        public static (string, DataObject) GetStartedGameParam(string value)
        {
            return (StartedGameName, new DataObject(DataObject.VisibilityOptions.Member, value));
        }

        public static (string, DataObject) GetRelayJoinCodeParam(string value)
        {
            return (RelayJoinCodeName, new DataObject(DataObject.VisibilityOptions.Member, value));
        }

        public static string GetLobbyRelayCode(Lobby lobby)
        {
            return lobby.Data[RelayJoinCodeName].Value;
        }
    }
}