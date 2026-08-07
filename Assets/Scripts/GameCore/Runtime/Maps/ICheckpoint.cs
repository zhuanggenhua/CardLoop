using UnityEngine;

namespace GameCore
{
    public interface ICheckpoint
    {
        public string sceneAddress { get; }
        public Vector3 position { get; }

        public bool IsValid();

        /// <summary>
        /// 若场景地址为空，保存前把它解析为 MapSystem 当前持有的 YooAsset 场景地址。
        /// </summary>
        public void UpdateSceneAddress();
    }
}

