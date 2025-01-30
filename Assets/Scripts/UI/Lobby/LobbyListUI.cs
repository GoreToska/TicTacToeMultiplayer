using System;
using UnityEngine;
using UnityEngine.Serialization;
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
            LobbySystem.LobbyManager.Instance.OnLobbyListChanged += RefreshList;
            LobbySystem.LobbyManager.Instance.OnJoinedLobby += OnJoinedLobby;
            LobbySystem.LobbyManager.Instance.OnLeftLobby += OnLeftLobby;
            LobbySystem.LobbyManager.Instance.OnKickedFromLobby += OnKickedFromLobby;

            Hide();
        }

        private void OnDestroy()
        {
            LobbySystem.LobbyManager.Instance.OnLobbyListChanged -= RefreshList;
            LobbySystem.LobbyManager.Instance.OnJoinedLobby -= OnJoinedLobby;
            LobbySystem.LobbyManager.Instance.OnLeftLobby -= OnLeftLobby;
            LobbySystem.LobbyManager.Instance.OnKickedFromLobby -= OnKickedFromLobby;
        }

        private void OnKickedFromLobby(object sender, LobbyManager.LobbyEventArgs e)
        {
            Hide();
            //Show();
            // TODO: show message that you was kicked
        }

        private void OnLeftLobby(object sender, EventArgs e)
        {
            Hide();
            //Show();
        }

        private void OnJoinedLobby(object sender, LobbyManager.LobbyEventArgs e)
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

        private void RefreshList(object sender, LobbySystem.LobbyManager.OnLobbyListChangedEventArgs e)
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