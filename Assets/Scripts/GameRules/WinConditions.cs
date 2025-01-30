using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GameRules
{
    public class WinConditions
    {
        private static List<Line> lines = new()
        {
            // horizontal lines
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
                CenterOfLine = new Vector2Int(1, 0),
                Orientation = LineOrientation.Horizontal,
            },
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1) },
                CenterOfLine = new Vector2Int(1, 1),
                Orientation = LineOrientation.Horizontal,
            },
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2) },
                CenterOfLine = new Vector2Int(1, 2),
                Orientation = LineOrientation.Horizontal,
            },

            // vertical
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
                CenterOfLine = new Vector2Int(0, 1),
                Orientation = LineOrientation.Vertical,
            },
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2) },
                CenterOfLine = new Vector2Int(1, 1),
                Orientation = LineOrientation.Vertical,
            },
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2) },
                CenterOfLine = new Vector2Int(2, 1),
                Orientation = LineOrientation.Vertical,
            },

            // diagonal
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2) },
                CenterOfLine = new Vector2Int(1, 1),
                Orientation = LineOrientation.DiagonalA,
            },
            new Line
            {
                GridVector2IntList = new List<Vector2Int>
                    { new Vector2Int(0, 2), new Vector2Int(1, 1), new Vector2Int(2, 0) },
                CenterOfLine = new Vector2Int(1, 1),
                Orientation = LineOrientation.DiagonalB,
            }
        };

        public enum LineOrientation
        {
            Horizontal,
            Vertical,
            DiagonalA,
            DiagonalB,
        }

        public struct Line : INetworkSerializable
        {
            public List<Vector2Int> GridVector2IntList;
            public Vector2Int CenterOfLine;
            public LineOrientation Orientation;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref CenterOfLine);
                serializer.SerializeValue(ref Orientation);

                Vector2Int[] list = serializer.IsWriter ? GridVector2IntList.ToArray() : default;
                serializer.SerializeValue(ref list);

                if (serializer.IsReader)
                    GridVector2IntList = list.ToList();
            }
        }

        // horizontal lines

        public static bool CheckWinCondition(PlayerType[,] grid, out Line line)
        {
            foreach (var item in lines)
            {
                if (!CheckLine(grid[item.GridVector2IntList[0].x, item.GridVector2IntList[0].y],
                        grid[item.GridVector2IntList[1].x, item.GridVector2IntList[1].y],
                        grid[item.GridVector2IntList[2].x, item.GridVector2IntList[2].y])) continue;

                line = new Line
                {
                    GridVector2IntList = new List<Vector2Int>
                    {
                        new Vector2Int(item.GridVector2IntList[0].x, item.GridVector2IntList[0].y),
                        new Vector2Int(item.GridVector2IntList[1].x, item.GridVector2IntList[1].y),
                        new Vector2Int(item.GridVector2IntList[2].x, item.GridVector2IntList[2].y)
                    },
                    CenterOfLine = new Vector2Int(item.GridVector2IntList[1].x, item.GridVector2IntList[1].y),
                    Orientation = item.Orientation,
                };

                return true;
            }

            line = new Line();
            return false;
        }

        public static bool CheckTieCondition(PlayerType[,] grid)
        {
            return grid.Cast<PlayerType>().All(item => item != PlayerType.None);
        }

        private static bool CheckLine(PlayerType a, PlayerType b, PlayerType c)
        {
            return a != PlayerType.None && a == b && b == c;
        }
    }
}