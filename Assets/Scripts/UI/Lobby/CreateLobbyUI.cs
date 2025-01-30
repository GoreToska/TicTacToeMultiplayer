using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LobbySystem
{
    public class CreateLobbyUI : MonoBehaviour
    {
        [HideInInspector] public static CreateLobbyUI Instance { get; private set; }

        [SerializeField] private GameObject objectToShowWhenClosed;
        [SerializeField] private GameObject root;
        [SerializeField] private Button submitButton;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
            closeButton.onClick.AddListener(Hide);
            submitButton.onClick.AddListener(CreateLobby);
            Hide();
        }

        public void Show()
        {
            nameInput.text = "New lobby";
            root.SetActive(true);
            objectToShowWhenClosed.SetActive(false);
        }

        public void Hide()
        {
            objectToShowWhenClosed.SetActive(true);
            root.SetActive(false);
        }

        public void CreateLobby()
        {
            LobbyManager.Instance.CreateLobby(nameInput.text);
            root.SetActive(false);
            //Hide();
        }
    }
}