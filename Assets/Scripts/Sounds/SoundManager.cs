using System;
using Managers;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sounds
{
    public class SoundManager : MonoBehaviour
    {
        [SerializeField] private Transform placeFXPrefab;
        [SerializeField] private Transform wrongFXPrefab;
        [SerializeField] private Transform winFXPrefab;
        [SerializeField] private Transform loseFXPrefab;

        private void Start()
        {
            GameManagerBase.Instance.OnPlacedObject += SpawnSFX;
            GameManagerBase.Instance.OnGameWin += OnGameWin;
        }

        private void OnGameWin(object sender, GameManagerBase.OnGameWinEventArgs e)
        {
            if (GameManagerBase.Instance.GetLocalPlayerType() == e.WinPlayerType)
            {
                Transform sfx = Instantiate(winFXPrefab);
                Destroy(sfx.gameObject, 6f);
            }
            else
            {
                Transform sfx = Instantiate(loseFXPrefab);
                Destroy(sfx.gameObject, 6f);
            }
        }

        private void SpawnSFX(object sender, EventArgs e)
        {
            Transform sfx = Instantiate(placeFXPrefab);
            Destroy(sfx.gameObject, 5f);
        }
    }
}