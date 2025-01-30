using System;
using Managers;
using UnityEngine;
using UnityEngine.UI;

namespace LobbySystem
{
    public class LobbyListUI : MonoBehaviour
    {
        public static LobbyListUI Instance { get; private set; }

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject objectToShowWhenClosed;
        [SerializeField] private Transform lobbyListMemberPrefab;
        [SerializeField] private Transform lobbyContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button createLobbyButton;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            refreshButton.onClick.AddListener(Refresh);
            createLobbyButton.onClick.AddListener(CreateLobby);
            closeButton.onClick.AddListener(Hide);
        }

        private void Start()
        {
            LobbyManager.Instance.OnLobbyListChanged += OnRefreshList;
            LobbyManager.Instance.OnJoinedLobby += OnJoinedLobby;
            LobbyManager.Instance.OnLeftLobby += OnLeftLobby;
            LobbyManager.Instance.OnKickedFromLobby += OnKickedFromLobby;

            Hide();
        }

        private void OnDestroy()
        {
            LobbyManager.Instance.OnLobbyListChanged -= OnRefreshList;
            LobbyManager.Instance.OnJoinedLobby -= OnJoinedLobby;
            LobbyManager.Instance.OnLeftLobby -= OnLeftLobby;
            LobbyManager.Instance.OnKickedFromLobby -= OnKickedFromLobby;
        }

        private void OnKickedFromLobby(object sender, EventHandlers.LobbyEventArgs e)
        {
            Hide();
            // TODO: show message that you was kicked
        }

        private void OnLeftLobby(object sender, EventArgs e)
        {
            Hide();
        }

        private void OnJoinedLobby(object sender, EventHandlers.LobbyEventArgs e)
        {
            Hide();
            LobbyUI.Instance.Show();
        }


        private void CreateLobby()
        {
            Hide();
            CreateLobbyUI.Instance.Show();
        }

        public void Hide()
        {
            root.SetActive(false);
            objectToShowWhenClosed.SetActive(true);
        }

        public void Show()
        {
            root.SetActive(true);
            objectToShowWhenClosed.SetActive(false);
        }

        private void Refresh()
        {
            LobbyManager.Instance.RefreshLobbyList();
        }

        private void OnRefreshList(object sender, EventHandlers.OnLobbyListChangedEventArgs e)
        {
            RefreshLobbyList(e);
        }

        private void RefreshLobbyList(EventHandlers.OnLobbyListChangedEventArgs e)
        {
            Clear();

            foreach (var item in e.lobbyList)
            {
                var lobby = Instantiate(lobbyListMemberPrefab, lobbyContainer);
                lobby.GetComponent<LobbyListMemberUI>().SetData(item);
            }
        }

        private void Clear()
        {
            foreach (Transform child in lobbyContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
}