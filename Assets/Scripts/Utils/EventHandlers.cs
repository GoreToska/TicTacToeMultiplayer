using System;
using System.Collections.Generic;
using GameRules;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Managers
{
    public class EventHandlers
    {
        public class OnClickedOnGridPositionEventArgs : EventArgs
        {
            public Vector2Int Position;
            public PlayerType PlayerType;
        }

        public class OnGameWinEventArgs : EventArgs, INetworkSerializable
        {
            public WinConditions.Line Line;
            public PlayerType WinPlayerType;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeNetworkSerializable(ref Line);
                serializer.SerializeValue(ref WinPlayerType);
            }
        }

        public class OnGameEnded : EventArgs, INetworkSerializable
        {
            public PlayerType WinPlayerType;
            public PlayerSide WinPlayerSide;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref WinPlayerType);
                serializer.SerializeValue(ref WinPlayerSide);
            }
        }
        
        public class LobbyEventArgs : EventArgs
        {
            public Lobby Lobby;
        }

        public class OnLobbyListChangedEventArgs : EventArgs
        {
            public List<Lobby> lobbyList;
        }
    }
}