using System;
using Managers;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace LobbySystem
{
    public class LobbyUI : MonoBehaviour
    {
        public static LobbyUI Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject objectToShowWhenClosed;
        [SerializeField] private Transform playerLobbyMemberPrefab;
        [SerializeField] private Transform playersContainer;
        [SerializeField] private TMP_Text lobbyNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private Button startGameButton;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            leaveLobbyButton.onClick.AddListener(() => { LobbyManager.Instance.LeaveLobby(); });
        }

        private void TryToStartGame()
        {
            LobbyManager.Instance.StartGameOnHost();
        }

        private void Start()
        {
            SubscribeToAllEvents();
            Hide();
        }

        private void SubscribeToAllEvents()
        {
            LobbyManager.Instance.OnJoinedLobby += UpdateLobby;
            LobbyManager.Instance.OnCurrentLobbyUpdate += UpdateLobby;
            LobbyManager.Instance.OnLeftLobby += OnLeftLobby;
            LobbyManager.Instance.OnKickedFromLobby += OnLeftLobby;
        }

        private void UpdateLobby(object sender, EventHandlers.LobbyEventArgs e)
        {
            UpdateLobby();
        }

        private void OnDestroy()
        {
            UnsubscribeAllEvents();
        }

        private void UnsubscribeAllEvents()
        {
            LobbyManager.Instance.OnJoinedLobby -= UpdateLobby;
            LobbyManager.Instance.OnCurrentLobbyUpdate -= UpdateLobby;
            LobbyManager.Instance.OnLeftLobby -= OnLeftLobby;
            LobbyManager.Instance.OnKickedFromLobby -= OnLeftLobby;
        }

        private void OnLeftLobby(object sender, EventArgs e)
        {
            ClearLobbyUI();
            Hide();
        }

        public void UpdateLobby()
        {
            UpdateLobby(LobbyManager.Instance.GetJoinedLobby());
        }

        private void UpdateLobby(Lobby lobby)
        {
            ClearLobbyUI();

            foreach (Player player in lobby.Players)
            {
                Transform playerSingleTransform = Instantiate(playerLobbyMemberPrefab, playersContainer);
                LobbyMemberUI lobbyMemberUI = playerSingleTransform.GetComponent<LobbyMemberUI>();
                SetKickButtonVisibility(player, lobbyMemberUI);
            }

            lobbyNameText.text = lobby.Name;
            playerCountText.text = lobby.Players.Count + " / " + lobby.MaxPlayers;

            if (!LobbyUtilities.IsLobbyHost(LobbyManager.Instance.GetJoinedLobby()))
            {
                startGameButton.gameObject.SetActive(false);
            }
            else
            {
                startGameButton.gameObject.SetActive(true);
                startGameButton.onClick.RemoveAllListeners();
                startGameButton.onClick.AddListener(TryToStartGame);
            }

            Show();
        }

        private static void SetKickButtonVisibility(Player player, LobbyMemberUI lobbyMemberUI)
        {
            bool isNotHostAndNotSelf = LobbyUtilities.IsLobbyHost(LobbyManager.Instance.GetJoinedLobby()) &&
                                       player.Id != AuthenticationService.Instance.PlayerId;
            lobbyMemberUI.SetKickButtonVisible(isNotHostAndNotSelf);
            lobbyMemberUI.SetMemberData(player);
        }

        private void ClearLobbyUI()
        {
            foreach (Transform child in playersContainer)
            {
                Destroy(child.gameObject);
            }
        }

        public void Hide()
        {
            root.SetActive(false);
            objectToShowWhenClosed.SetActive(true);
        }

        public void Show()
        {
            LobbyManager.Instance.StartLobbiesRefreshRoutine();
            root.SetActive(true);
            objectToShowWhenClosed.SetActive(false);
        }
    }
}