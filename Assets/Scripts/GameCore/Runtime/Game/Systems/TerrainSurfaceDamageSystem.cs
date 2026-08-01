using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [DisallowMultipleComponent]
    public sealed class TerrainSurfaceDamageSystem : AGameSystem
    {
        [Header("状态来源")]
        [SerializeField] private TerrainNavigationMap m_navigationMap = null;

        [Header("燃烧伤害")]
        [SerializeField, Min(0.05f)] private float m_damageIntervalSeconds = 0.5f;
        [SerializeField, Min(1)] private int m_burningDamagePerTick = 1;
        [SerializeField] private EDamageType m_damageType = EDamageType.Magical;
        [SerializeField] private bool m_ignoreDefense = true;
        [SerializeField] private bool m_silentDamage = false;
        [SerializeField] private EEffectVisualFlags m_visualFlags =
            EEffectVisualFlags.None;

        private readonly List<CharacterBase> m_targets = new();
        private readonly HashSet<CharacterBase> m_registeredTargets = new();
        private float m_accumulatedTime;
        private bool m_missingMapReported;

        public override void OnSystemStart()
        {
            TryBindActiveMap(reportFailure: false);
        }

        public override void OnSystemStop()
        {
            m_navigationMap = null;
            m_targets.Clear();
            m_registeredTargets.Clear();
            m_accumulatedTime = 0.0f;
            m_missingMapReported = false;
        }

        public override void OnMapLoaded()
        {
            TryBindActiveMap(reportFailure: true);
        }

        public override void OnMapUnloading()
        {
            m_targets.Clear();
            m_navigationMap = null;
            m_accumulatedTime = 0.0f;
            m_missingMapReported = false;
        }

        public void RegisterTarget(CharacterBase target)
        {
            if (target != null)
            {
                m_registeredTargets.Add(target);
            }
        }

        public void UnregisterTarget(CharacterBase target)
        {
            if (target != null)
            {
                m_registeredTargets.Remove(target);
            }
        }

        private void Update()
        {
            if (Time.timeScale <= 0.0f ||
                m_burningDamagePerTick <= 0 ||
                (m_navigationMap == null && !TryBindActiveMap(reportFailure: false)))
            {
                return;
            }

            float interval = Mathf.Max(0.05f, m_damageIntervalSeconds);
            m_accumulatedTime += Time.deltaTime;
            while (m_accumulatedTime >= interval)
            {
                m_accumulatedTime -= interval;
                ApplyBurningContactDamage();
            }
        }

        private bool TryBindActiveMap(bool reportFailure)
        {
            if (m_navigationMap != null)
            {
                return true;
            }

            if (!GameManager.Exists() ||
                !GameManager.TryGetSystem<MapSystem>(out MapSystem mapSystem))
            {
                if (reportFailure)
                {
                    Debug.LogError(
                        "地表伤害系统无法取得 MapSystem，不能读取当前地表状态。",
                        this);
                }

                return false;
            }

            MapInfo mapInfo = mapSystem.ResolveActiveMapInfo();
            if (mapInfo == null ||
                !mapInfo.TryGetTerrainNavigationMap(out TerrainNavigationMap navigationMap))
            {
                if (reportFailure && !m_missingMapReported)
                {
                    Debug.LogError(
                        "当前活动 MapInfo 未配置 TerrainNavigationMap，地表伤害无法生效。",
                        this);
                    m_missingMapReported = true;
                }

                return false;
            }

            m_navigationMap = navigationMap;
            m_missingMapReported = false;
            return true;
        }

        private void ApplyBurningContactDamage()
        {
            CollectTargets();
            if (m_targets.Count == 0)
            {
                return;
            }

            FormalDamageEffectPayload payload = CreateBurningDamagePayload();
            for (int i = 0; i < m_targets.Count; i++)
            {
                TryApplyBurningContactDamage(m_targets[i], payload);
            }
        }

        private bool TryApplyBurningContactDamage(CharacterBase target)
        {
            return TryApplyBurningContactDamage(
                target,
                CreateBurningDamagePayload());
        }

        private bool TryApplyBurningContactDamage(
            CharacterBase target,
            FormalDamageEffectPayload payload)
        {
            if (!TryGetBurningContactSample(target, out TerrainSurfaceSample sample))
            {
                return false;
            }

            CharacterBase source = ResolveBurningDamageSource(sample);
            return FormalGameplayEffectDamageHelper.TryApplyDamage(
                source,
                target,
                payload);
        }

        private void CollectTargets()
        {
            m_targets.Clear();
            foreach (CharacterBase target in m_registeredTargets)
            {
                AddTarget(target);
            }

            if (!GameManager.Exists() ||
                !GameManager.TryGetSystem<PlayerSystem>(out PlayerSystem playerSystem))
            {
                return;
            }

            AddTarget(playerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        private void AddTarget(CharacterBase target)
        {
            if (target != null && !m_targets.Contains(target))
            {
                m_targets.Add(target);
            }
        }

        private bool TryGetBurningContactSample(
            CharacterBase target,
            out TerrainSurfaceSample sample)
        {
            sample = default;
            if (target == null || target.dead || m_navigationMap == null)
            {
                return false;
            }

            if (!m_navigationMap.TryGetSurfaceSample(
                    target.transform.position,
                    out sample))
            {
                return false;
            }

            return (sample.RuntimeState & ETerrainRuntimeSurfaceState.Burning) != 0;
        }

        private static CharacterBase ResolveBurningDamageSource(
            in TerrainSurfaceSample sample)
        {
            IReadOnlyList<TerrainElementStateSnapshot> activeStates =
                sample.ActiveStates;
            for (int i = 0; i < activeStates.Count; i++)
            {
                TerrainElementStateSnapshot state = activeStates[i];
                if (state.StateKind == ETerrainElementStateKind.Burning)
                {
                    return state.SourceEntity as CharacterBase;
                }
            }

            return null;
        }

        private FormalDamageEffectPayload CreateBurningDamagePayload()
        {
            return new FormalDamageEffectPayload(
                new DamageDescriptor
                {
                    damageType = m_damageType,
                    flatDamages = m_burningDamagePerTick,
                    scalingFactor = 0.0f,
                    criticalBehavior = EResolutionBehavior.Never,
                    missBehavior = EResolutionBehavior.Never,
                    ignoreDefense = m_ignoreDefense,
                    silent = m_silentDamage
                },
                m_visualFlags,
                default,
                EEffectImpactDataType.Velocity,
                Vector2.zero);
        }
    }
}
