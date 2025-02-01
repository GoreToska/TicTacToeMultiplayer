using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LobbySystem
{
    public static class RelayUtilities
    {
        public static async Task<string> GetRelayJoinCode(Guid allocationId)
        {
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocationId);
            return joinCode;
        }

        public static async Task<Allocation> CreateRelayAllocation(int maxPlayers)
        {
            Allocation relayAllocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            var relayServerData = relayAllocation.ToRelayServerData(GetConnectionType());
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);

            return relayAllocation;
        }

        public static async Task StartClientRelay(Lobby joinedLobby)
        {
            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(LobbyUtilities.GetLobbyRelayCode(joinedLobby));
            RelayServerData relayServerData = joinAllocation.ToRelayServerData(GetConnectionType());
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayServerData);
            NetworkManager.Singleton.StartClient();
        }

        public static string GetConnectionType()
        {
            return NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets ? "wss" : "dtls";
        }
    }
}