using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// Mod 内容包描述文件。每个启用的 Mod 必须对应一个独立 YooAsset 资源包。
    /// 文件通常来自 Mod 目录下的 cfg/json 内容清单，运行时路径不写回清单本体。
    /// </summary>
    [Serializable]
    public class ModInfo
    {
        public string apiVersion;
        public string authorName;
        public string modName;
        public string version;
        public string description;
        public string packageName;
        public int loadOrder;
        public string contentHash;
        public byte[] modIconBytes;
        public Dictionary<string, string> metaData = new();

        [JsonIgnore] public string FilePath { get; set; }
        [JsonIgnore] public string FullName => $"{modName}-{version}-{apiVersion}";
    }
}
