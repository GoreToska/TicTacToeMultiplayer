using System;
using LobbySystem;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class QuickJoinLobbyButtonUI : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => LobbyManager.Instance.QuickJoinLobby());
    }
}