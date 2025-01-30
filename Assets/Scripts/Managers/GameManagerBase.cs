using System;
using System.Collections;
using GameRules;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public enum PlayerType
{
    None = 0,
    Cross = 1,
    Circle = 2,
}

public enum PlayerSide
{
    None = 0,
    Left = 1,
    Right = 2,
}

namespace Managers
{
    public class GameManagerBase : NetworkBehaviour
    {
        public static GameManagerBase Instance { get; private set; }

        [SerializeField] private int winScore = 2;
        [SerializeField] private float timeBetweenGames = 2;
        private PlayerType[,] playerFiguresArray;

        public event EventHandler<OnClickedOnGridPositionEventArgs> OnClickedOnGridPosition;
        public event EventHandler OnGameStarted;
        public event EventHandler<OnGameWinEventArgs> OnGameWin;
        public event EventHandler OnGameTie;
        public event EventHandler OnCurrentPlayablePlayerTypeChanged;
        public event EventHandler OnRematch;
        public event EventHandler OnTeamChanged;
        public event EventHandler OnScoreChanged;
        public event EventHandler OnPlacedObject;
        public event EventHandler<OnGameEnded> OnGameOver;

        private NetworkVariable<int> leftPlayerScore = new NetworkVariable<int>();
        private NetworkVariable<int> rightPlayerScore = new NetworkVariable<int>();

        private PlayerType winnerPlayerType;
        private PlayerSide winnerPlayerSide;
        private bool isEnded = false;

        public class OnClickedOnGridPositionEventArgs : EventArgs
        {
            public Vector2Int Position;
            public PlayerType PlayerType;
        }

        public class OnGameWinEventArgs : EventArgs, INetworkSerializable
        {
            public WinConditions.Line Line;
            public PlayerType WinPlayerType;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeNetworkSerializable(ref Line);
                serializer.SerializeValue(ref WinPlayerType);
            }
        }

        public class OnGameEnded : EventArgs, INetworkSerializable
        {
            public PlayerType WinPlayerType;
            public PlayerSide WinPlayerSide;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref WinPlayerType);
                serializer.SerializeValue(ref WinPlayerSide);
            }
        }

        private PlayerType localPlayerType;
        private PlayerSide localPlayerSide;

        // default constructor - all can read, server can write
        private NetworkVariable<PlayerType> currentPlayablePlayerType = new NetworkVariable<PlayerType>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Trying to create another Game Manager.");
                Destroy(gameObject);
            }

            playerFiguresArray = new PlayerType[3, 3];
            rightPlayerScore.Value = 0;
            leftPlayerScore.Value = 0;
        }

        private void Start()
        {
            MultiplayerSceneManager.Instance.OnAllPlayersLoaded += StartGame;
        }

        public override void OnDestroy()
        {
            MultiplayerSceneManager.Instance.OnAllPlayersLoaded -= StartGame;
        }

        private void StartGame(object sender, EventArgs e)
        {
            StartGame();
        }

        public void StartGame()
        {
            if (NetworkManager.Singleton.LocalClientId == 0)
            {
                localPlayerType = PlayerType.Cross;
                localPlayerSide = PlayerSide.Right;
            }
            else
            {
                localPlayerType = PlayerType.Circle;
                localPlayerSide = PlayerSide.Left;
            }

            currentPlayablePlayerType.OnValueChanged += OnPlayablePlayerTypeChanged;


            leftPlayerScore.OnValueChanged += InvokeScoreChanged;
            rightPlayerScore.OnValueChanged += InvokeScoreChanged;

            if (IsServer || IsHost)
            {
                currentPlayablePlayerType.Value = PlayerType.Cross;
                TriggerOnGameStartedRPC();
            }
        }

        private void OnPlayablePlayerTypeChanged(PlayerType value, PlayerType newValue)
        {
            OnCurrentPlayablePlayerTypeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InvokeScoreChanged(int previousvalue, int newvalue)
        {
            OnScoreChanged?.Invoke(this, EventArgs.Empty);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerGameOverRPC(PlayerSide playerSide)
        {
            OnGameOver?.Invoke(this,
                new OnGameEnded { WinPlayerSide = playerSide });
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerOnGameStartedRPC()
        {
            Debug.Log("StartGame");
            OnGameStarted?.Invoke(this, EventArgs.Empty);
        }

        [Rpc(SendTo.Server)]
        public void ClickedOnGridPositionRPC(Vector2Int position, PlayerType playerType)
        {
            if (isEnded)
                return;

            if (playerType != currentPlayablePlayerType.Value)
            {
                // TODO: VFX and SFX maybe?
                Debug.Log("Not your turn");
                return;
            }

            if (playerFiguresArray[position.x, position.y] != PlayerType.None)
            {
                // TODO: VFX and SFX maybe?
                Debug.Log("Already occupied!");
                return;
            }

            playerFiguresArray[position.x, position.y] = playerType;
            OnClickedOnGridPosition?.Invoke(this,
                new OnClickedOnGridPositionEventArgs { Position = position, PlayerType = playerType });
            TriggerOnPlaceObjectEventRPC();

            if (WinConditions.CheckWinCondition(playerFiguresArray, out var line))
            {
                // TODO: end screen, call rpc
                OnGameWinEventArgs args = new OnGameWinEventArgs
                    { Line = line, WinPlayerType = currentPlayablePlayerType.Value };
                isEnded = true;
                IncreaseScore(currentPlayablePlayerType.Value);
                TriggerWinEventRPC(args);

                return;
            }

            if (WinConditions.CheckTieCondition(playerFiguresArray))
            {
                TriggerTieEventRPC();
                isEnded = true;

                return;
            }

            SwitchPlayablePlayerTypeRPC(playerType);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerOnPlaceObjectEventRPC()
        {
            OnPlacedObject?.Invoke(this, EventArgs.Empty);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerTieEventRPC()
        {
            OnGameTie?.Invoke(this, EventArgs.Empty);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerWinEventRPC(OnGameWinEventArgs args)
        {
            OnGameWin?.Invoke(this, args);
        }

        [Rpc(SendTo.Server)]
        private void SwitchPlayablePlayerTypeRPC(PlayerType playerType)
        {
            switch (playerType)
            {
                case PlayerType.None:
                    break;
                case PlayerType.Cross:
                    currentPlayablePlayerType.Value = PlayerType.Circle;
                    break;
                case PlayerType.Circle:
                    currentPlayablePlayerType.Value = PlayerType.Cross;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(playerType), playerType, null);
            }
        }

        private void IncreaseScore(PlayerType winPlayer)
        {
            if (winPlayer == localPlayerType)
                rightPlayerScore.Value += 1;
            else
                leftPlayerScore.Value += 1;

            CheckForEndOfGameRPC(winPlayer);

            if (winnerPlayerSide == PlayerSide.None)
                StartCoroutine(WaitAndRematch());
        }

        [Rpc(SendTo.Server)]
        private void CheckForEndOfGameRPC(PlayerType winPlayer)
        {
            if (rightPlayerScore.Value >= winScore)
            {
                winnerPlayerSide = PlayerSide.Right;
                winnerPlayerType = winPlayer;
                TriggerGameOverRPC(PlayerSide.Right);
                return;
            }

            if (leftPlayerScore.Value >= winScore)
            {
                winnerPlayerSide = PlayerSide.Left;
                winnerPlayerType = winPlayer;
                TriggerGameOverRPC(PlayerSide.Left);
                return;
            }
        }

        [Rpc(SendTo.Server)]
        public void RematchRPC()
        {
            isEnded = false;
            ClearBoard();
            currentPlayablePlayerType.Value = PlayerType.Cross;
            ChangeTeamRPC();
            TriggerOnRematchEventRPC();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void TriggerOnRematchEventRPC()
        {
            OnRematch?.Invoke(this, EventArgs.Empty);
        }

        private void ClearBoard()
        {
            for (int x = 0; x < playerFiguresArray.GetLength(0); ++x)
            {
                for (int y = 0; y < playerFiguresArray.GetLength(1); ++y)
                {
                    playerFiguresArray[x, y] = PlayerType.None;
                }
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ChangeTeamRPC()
        {
            if (localPlayerType == PlayerType.Circle)
                localPlayerType = PlayerType.Cross;
            else
                localPlayerType = PlayerType.Circle;

            OnTeamChanged?.Invoke(this, EventArgs.Empty);
        }

        public int GetLeftPlayerScore()
        {
            return leftPlayerScore.Value;
        }

        public int GetRightPlayerScore()
        {
            return rightPlayerScore.Value;
        }

        public PlayerType GetLocalPlayerType()
        {
            return localPlayerType;
        }

        public PlayerSide GetLocalPlayerSide()
        {
            return localPlayerSide;
        }

        public PlayerType GetCurrentPlayablePlayerType()
        {
            return currentPlayablePlayerType.Value;
        }

        private IEnumerator WaitAndRematch()
        {
            yield return new WaitForSeconds(timeBetweenGames);
            RematchRPC();
            yield break;
        }
    }
}