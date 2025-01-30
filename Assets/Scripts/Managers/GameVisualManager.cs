using System;
using System.Collections.Generic;
using GameRules;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace Managers
{
    public class GameVisualManager : NetworkBehaviour
    {
        [SerializeField] private Transform crossPrefab;
        [SerializeField] private Transform circlePrefab;
        [SerializeField] private Vector2Int gridSize = new Vector2Int(3, 3);
        [SerializeField] private Transform winnerLinePrefab;

        private List<GameObject> visualGameObjects;

        private void Awake()
        {
            visualGameObjects = new List<GameObject>();
        }

        private void Start()
        {
            GameManagerBase.Instance.OnClickedOnGridPosition += OnClickedOnGrid;
            GameManagerBase.Instance.OnGameWin += OnGameWin;
            GameManagerBase.Instance.OnRematch += OnRematch;
        }

        private void OnRematch(object sender, EventArgs e)
        {
            foreach (var item in visualGameObjects)
            {
                Destroy(item);
            }

            visualGameObjects.Clear();
        }

        private void OnGameWin(object sender, EventHandlers.OnGameWinEventArgs e)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            Transform winnerLine = Instantiate(winnerLinePrefab, GetGridWorldPosition(e.Line.CenterOfLine),
                Quaternion.Euler(0, 0, GetLineRotation(e.Line)));
            Debug.Log(GetLineRotation(e.Line));
            winnerLine.GetComponent<NetworkObject>().Spawn(true);
            visualGameObjects.Add(winnerLine.gameObject);
        }

        private float GetLineRotation(WinConditions.Line line)
        {
            return line.Orientation switch
            {
                WinConditions.LineOrientation.Horizontal => 0f,
                WinConditions.LineOrientation.Vertical => 90f,
                WinConditions.LineOrientation.DiagonalA => 45f,
                WinConditions.LineOrientation.DiagonalB => -45f,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private void OnClickedOnGrid(object sender, EventHandlers.OnClickedOnGridPositionEventArgs e)
        {
            Debug.Log("Spawn Object!");
            SpawnObjectRPC(e.Position, e.PlayerType);
        }

        [Rpc(SendTo.Server)]
        private void SpawnObjectRPC(Vector2Int position, PlayerType playerType)
        {
            Transform prefab = GetPrefabToSpawn(playerType);
            
            if(!prefab)
                return;
            
            Transform spawnedCross = Instantiate(GetPrefabToSpawn(playerType), GetGridWorldPosition(position),
                quaternion.identity);
            spawnedCross.GetComponent<NetworkObject>().Spawn(true);
            visualGameObjects.Add(spawnedCross.gameObject);
        }

        private Transform GetPrefabToSpawn(PlayerType playerType)
        {
            return playerType switch
            {
                PlayerType.None => null,
                PlayerType.Cross => crossPrefab,
                PlayerType.Circle => circlePrefab,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private Vector2 GetGridWorldPosition(Vector2Int position)
        {
            return new Vector2Int(-gridSize.x + position.x * gridSize.x, -gridSize.y + position.y * gridSize.y);
        }
    }
}