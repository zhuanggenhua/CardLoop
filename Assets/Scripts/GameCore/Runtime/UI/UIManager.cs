using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 正式 UI 运行时协调器。
    /// 这里统一承接项目菜单语义到 UIKit 原生入口的唯一正式入口。
    /// </summary>
    public sealed partial class UIManager : MonoBehaviour
    {
        private void Start()
        {
            StartMenuRuntime();
        }

        private void OnDestroy()
        {
            StopMenuRuntime();
        }
    }
}
