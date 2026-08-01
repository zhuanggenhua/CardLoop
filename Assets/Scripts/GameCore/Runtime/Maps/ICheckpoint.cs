using UnityEngine;

namespace FantasyWord.GameCore
{
    public interface ICheckpoint
    {
        public string map { get; }
        public Vector3 position { get; }

        public bool IsValid();

        /// <summary>
        /// 若地图名为空，保存前把它解析为当前 MapSystem 的地图名，避免检查点在跨场景存档中丢失归属。
        /// </summary>
        public void UpdateMapName();
    }
}

