using System;
using System.Collections;
using Managers;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndGameUI : MonoBehaviour
{
    [SerializeField] private GameObject content;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_Text resultText;

    [SerializeField] private float timeToWait = 3;
    [SerializeField] private string winText = "You win!";
    [SerializeField] private string loseText = "You lose!";

    private void Start()
    {
        GameManagerBase.Instance.OnGameOver += OnGameOver;
        content.SetActive(false);
        exitButton.onClick.AddListener(OnExitClick);
    }

    private void OnExitClick()
    {
        NetworkManager.Singleton.Shutdown();
        LobbySystem.LobbyManager.Instance.DiscardLobby();
        MultiplayerSceneManager.Instance.LoadScene("StartScene", false);
    }

    private void OnDestroy()
    {
        GameManagerBase.Instance.OnGameOver -= OnGameOver;
    }

    private void OnGameOver(object sender, EventHandlers.OnGameEnded e)
    {
        StartCoroutine(StartGameOverTimer(e));
    }

    private IEnumerator StartGameOverTimer(EventHandlers.OnGameEnded e)
    {
        yield return new WaitForSeconds(timeToWait);

        if (e.WinPlayerSide == GameManagerBase.Instance.GetLocalPlayerSide())
        {
            resultText.text = winText;
        }
        else
        {
            resultText.text = loseText;
        }

        content.SetActive(true);
    }
}