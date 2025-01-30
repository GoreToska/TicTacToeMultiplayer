using System;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class GameOverUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Color winColor;
        [SerializeField] private Color loseColor;
        [SerializeField] private Color tieColor;
        //[SerializeField] private Button rematchButton;

        private void Awake()
        {
            //rematchButton.onClick.AddListener(() => GameManagerBase.Instance.RematchRPC());
        }

        private void Start()
        {
            Hide();
            GameManagerBase.Instance.OnGameWin += OnGameWin;
            GameManagerBase.Instance.OnRematch += OnRematch;
            GameManagerBase.Instance.OnGameTie += OnGameTie;
        }

        private void OnGameTie(object sender, EventArgs e)
        {
            resultText.text = "Tie!";
            resultText.color = tieColor;
            Show();
        }

        private void OnRematch(object sender, EventArgs e)
        {
            Hide();
        }

        private void OnGameWin(object sender, GameManagerBase.OnGameWinEventArgs e)
        {
            if (e.WinPlayerType == GameManagerBase.Instance.GetLocalPlayerType())
            {
                resultText.text = "You won!";
                resultText.color = winColor;
            }
            else
            {
                resultText.text = "You lost!";
                resultText.color = loseColor;
            }

            Show();
        }

        private void Show()
        {
            gameObject.SetActive(true);
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}