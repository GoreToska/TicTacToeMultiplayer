using Managers;
using UnityEngine;

namespace Grid
{
    public class GridPosition : MonoBehaviour
    {
        [SerializeField] private Vector2Int coordinates;

        private void OnMouseDown()
        {
            GameManagerBase.Instance.ClickedOnGridPositionRPC(coordinates, GameManagerBase.Instance.GetLocalPlayerType());
        }
    }
}