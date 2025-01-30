using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyMemberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text memberName;
    [SerializeField] private Button kickButton;

    private Player player;

    private void Awake()
    {
        kickButton.onClick.AddListener(KickPlayer);
    }

    private void KickPlayer()
    {
        if(player != null)
            LobbySystem.LobbyManager.Instance.KickPlayer(player.Id);
    }

    public void SetMemberData(Player player)
    {
        this.player = player;
        memberName.text = player.Data["PlayerName"].Value;
    }

    public void SetKickButtonVisible(bool value)
    {
        kickButton.gameObject.SetActive(value);
    }
}