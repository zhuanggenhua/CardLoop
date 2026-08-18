using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameCore
{
    /// <summary>
    /// Mod 内容包描述文件。每个启用的 Mod 必须对应一个独立 YooAsset 资源包。
    /// 文件通常来自 Mod 目录下的 cfg/json 内容清单，运行时路径不写回清单本体。
    /// </summary>
    [Serializable]
    public class ModInfo
    {
        public string modId;
        public string apiVersion;
        public string authorName;
        public string modName;
        public string version;
        public string description;
        public string packageName;
        public List<ModDependency> dependencies = new();
        public byte[] modIconBytes;

        [JsonIgnore] public string FilePath { get; set; }
        [JsonIgnore] public string DisplayName => string.IsNullOrWhiteSpace(modName) ? modId : modName;
    }

    /// <summary>一个 Mod 对另一个稳定 Mod 身份的版本约束；版本边界均为包含。</summary>
    [Serializable]
    public sealed class ModDependency
    {
        public string modId;
        public string minimumVersion;
        public string maximumVersion;
    }
}
