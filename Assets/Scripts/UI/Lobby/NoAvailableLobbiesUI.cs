using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace LobbySystem
{
    [RequireComponent(typeof(Animator))]
    public class NoAvailableLobbiesUI : MonoBehaviour
    {
        [HideInInspector] public static NoAvailableLobbiesUI Instance { get; private set; }

        private Animator animator;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            animator = GetComponent<Animator>();
        }

        public void Show()
        {
            animator.Play("LobbyAssert");
        }
    }
}