using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadMenuLevel : MonoBehaviour
{
    private void Start()
    {
        MultiplayerSceneManager.Instance.LoadScene("StartScene", false);
    }
}