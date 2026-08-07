using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 多层地形图中的稳定节点键。
    /// LayerId 区分逻辑地形层，Cell 保持 Tilemap 格坐标，二者共同作为寻路索引。
    /// </summary>
    public readonly struct TerrainNodeKey : IEquatable<TerrainNodeKey>
    {
        public const int DefaultLayerId = 0;

        public TerrainNodeKey(int layerId, Vector3Int cell)
        {
            LayerId = layerId;
            Cell = cell;
        }

        public int LayerId { get; }
        public Vector3Int Cell { get; }
        public bool IsDefaultLayer => LayerId == DefaultLayerId;

        public static TerrainNodeKey Default(Vector3Int cell)
        {
            return new TerrainNodeKey(DefaultLayerId, cell);
        }

        public bool Equals(TerrainNodeKey other)
        {
            return LayerId == other.LayerId && Cell == other.Cell;
        }

        public override bool Equals(object obj)
        {
            return obj is TerrainNodeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (LayerId * 397) ^ Cell.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"Layer={LayerId}, Cell={Cell}";
        }

        public static bool operator ==(TerrainNodeKey left, TerrainNodeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TerrainNodeKey left, TerrainNodeKey right)
        {
            return !left.Equals(right);
        }
    }
}
