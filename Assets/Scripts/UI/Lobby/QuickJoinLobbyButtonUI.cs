using LobbySystem;
using UnityEngine;
using UnityEngine.UI;

namespace LobbySystem
{
    [RequireComponent(typeof(Button))]
    public class QuickJoinLobbyButtonUI : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(() => LobbyManager.Instance.QuickJoinLobby());
        }
    }
}