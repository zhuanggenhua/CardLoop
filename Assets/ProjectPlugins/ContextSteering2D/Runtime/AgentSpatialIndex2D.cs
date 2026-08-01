using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextSteering2D
{
    internal sealed class AgentSpatialIndex2D
    {
        private readonly Dictionary<long, List<int>> m_cells = new();
        private readonly List<List<int>> m_cellPool = new();
        private Vector2[] m_positions = Array.Empty<Vector2>();
        private float m_cellSize = 1.0f;

        public float MaxRadius { get; set; }

        public void Build(Vector2[] positions, int count, float cellSize)
        {
            RecycleCells();
            m_positions = positions ?? throw new ArgumentNullException(nameof(positions));
            m_cellSize = Mathf.Max(cellSize, 0.01f);
            MaxRadius = m_cellSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                long key = GetKey(positions[i]);
                if (!m_cells.TryGetValue(key, out List<int> cell))
                {
                    cell = RentCell();
                    m_cells.Add(key, cell);
                }
                cell.Add(i);
            }
        }

        public void Collect(Vector2 center, float radius, List<int> results)
        {
            results.Clear();
            int minX = Mathf.FloorToInt((center.x - radius) / m_cellSize);
            int maxX = Mathf.FloorToInt((center.x + radius) / m_cellSize);
            int minY = Mathf.FloorToInt((center.y - radius) / m_cellSize);
            int maxY = Mathf.FloorToInt((center.y + radius) / m_cellSize);
            float radiusSq = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!m_cells.TryGetValue(ComposeKey(x, y), out List<int> cell)) continue;
                    for (int i = 0; i < cell.Count; i++)
                    {
                        int index = cell[i];
                        if ((m_positions[index] - center).sqrMagnitude <= radiusSq)
                        {
                            results.Add(index);
                        }
                    }
                }
            }
        }

        private long GetKey(Vector2 position)
        {
            int x = Mathf.FloorToInt(position.x / m_cellSize);
            int y = Mathf.FloorToInt(position.y / m_cellSize);
            return ComposeKey(x, y);
        }

        private static long ComposeKey(int x, int y) => ((long)x << 32) ^ (uint)y;

        private List<int> RentCell()
        {
            int last = m_cellPool.Count - 1;
            if (last < 0) return new List<int>(8);
            List<int> cell = m_cellPool[last];
            m_cellPool.RemoveAt(last);
            return cell;
        }

        private void RecycleCells()
        {
            foreach (List<int> cell in m_cells.Values)
            {
                cell.Clear();
                m_cellPool.Add(cell);
            }
            m_cells.Clear();
        }
    }
}
