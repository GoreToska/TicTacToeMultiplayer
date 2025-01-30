using System;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace UI
{
    public class PlayerUI : MonoBehaviour
    {
        [SerializeField] private GameObject crossArrowGameObject;
        [SerializeField] private GameObject circleArrowGameObject;
        [SerializeField] private GameObject crossYouTextGameObject;
        [SerializeField] private GameObject circleYouTextGameObject;
        [SerializeField] private GameObject crossImage;
        [SerializeField] private GameObject circleImage;
        [SerializeField] private TMP_Text leftScore;
        [SerializeField] private TMP_Text rightScore;

        private void Awake()
        {
            crossArrowGameObject.SetActive(false);
            circleArrowGameObject.SetActive(false);
            crossYouTextGameObject.SetActive(false);
            circleYouTextGameObject.SetActive(false);
        }

        private void Start()
        {
            GameManagerBase.Instance.OnGameStarted += UpdateUIContent;
            GameManagerBase.Instance.OnCurrentPlayablePlayerTypeChanged += ChangeCurrentPlayerUI;
            GameManagerBase.Instance.OnTeamChanged += ChangeTeamUI;
            GameManagerBase.Instance.OnGameWin += (sender, args) => { HideArrows(); };
            GameManagerBase.Instance.OnGameTie += (sender, args) => { HideArrows(); };
            GameManagerBase.Instance.OnScoreChanged += OnScoreChanged;

            leftScore.text = "0";
            rightScore.text = "0";
        }

        private void OnScoreChanged(object sender, EventArgs e)
        {
            leftScore.text = GameManagerBase.Instance.GetLeftPlayerScore().ToString();
            rightScore.text = GameManagerBase.Instance.GetRightPlayerScore().ToString();
        }

        private void HideArrows()
        {
            crossArrowGameObject.SetActive(false);
            circleArrowGameObject.SetActive(false);
        }

        private void ChangeTeamUI(object sender, EventArgs e)
        {
            (crossImage.transform.position, circleImage.transform.position) =
                (circleImage.transform.position, crossImage.transform.position);

            (crossArrowGameObject, circleArrowGameObject) = (circleArrowGameObject, crossArrowGameObject);
            UpdateCurrentPlayerArrow();
        }

        private void ChangeCurrentPlayerUI(object sender, EventArgs e)
        {
            UpdateCurrentPlayerArrow();
        }

        private void UpdateUIContent(object sender, EventArgs e)
        {
            SetTextPrompt();
            UpdateCurrentPlayerArrow();
        }

        private void SetTextPrompt()
        {
            if (GameManagerBase.Instance.GetLocalPlayerType() == PlayerType.Cross)
            {
                crossYouTextGameObject.SetActive(true);
            }
            else
            {
                circleYouTextGameObject.SetActive(true);
            }
        }

        private void UpdateCurrentPlayerArrow()
        {
            if (GameManagerBase.Instance.GetCurrentPlayablePlayerType() == PlayerType.None)
            {
                crossArrowGameObject.SetActive(false);
                circleArrowGameObject.SetActive(false);
                return;
            }

            if (GameManagerBase.Instance.GetCurrentPlayablePlayerType() == PlayerType.Cross)
            {
                crossArrowGameObject.SetActive(true);
                circleArrowGameObject.SetActive(false);
            }
            else
            {
                circleArrowGameObject.SetActive(true);
                crossArrowGameObject.SetActive(false);
            }
        }
    }
}