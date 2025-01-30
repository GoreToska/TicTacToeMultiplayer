using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace LobbySystem
{
    public class LobbyListMemberUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text lobbyName;
        [SerializeField] private TMP_Text playersCount;
        [SerializeField] private Button joinButton;

        private Lobby _lobby;

        public void SetData(Lobby lobby)
        {
            Debug.Log(lobby.Name);
            lobbyName.text = lobby.Name;
            playersCount.text = lobby.Players.Count + " / " + lobby.MaxPlayers;
            _lobby = lobby;
            joinButton.onClick.AddListener(Connect);
        }

        public void Connect()
        {
            LobbyManager.Instance.JoinLobbyByID(_lobby.Id);
        }
    }
}