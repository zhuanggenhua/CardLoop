using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>一次 GameplayEffect 结算使用的权威随机种子。</summary>
    public struct CEffectAuthoritativeRandomSeed : IComponentData
    {
        public uint Value;
    }
}
