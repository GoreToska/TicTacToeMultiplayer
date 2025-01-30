using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSettingsUI : MonoBehaviour
{
    [HideInInspector] public static PlayerSettingsUI Instance { get; private set; }

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

        submitButton.onClick.AddListener(ApplyNameChange);
        closeButton.onClick.AddListener(Hide);
        Hide();
    }

    private void ApplyNameChange()
    {
        LobbySystem.LobbyManager.Instance.SetPlayerName(nameInput.text);
        nameInput.text = LobbySystem.LobbyManager.Instance.GetPlayerName();
        Hide();
    }


    public void Hide()
    {
        root.SetActive(false);
        objectToShowWhenClosed.SetActive(true);
    }

    public void Show()
    {
        nameInput.text = LobbySystem.LobbyManager.Instance.GetPlayerName();
        root.SetActive(true);
        objectToShowWhenClosed.SetActive(false);
    }
}