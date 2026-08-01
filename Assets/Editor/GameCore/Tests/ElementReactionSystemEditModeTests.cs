using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore.Tests
{
    public sealed class ElementReactionSystemEditModeTests
    {
        private readonly List<UnityEngine.Object> m_createdObjects = new();

        private GameObject m_gridObject;
        private Tilemap m_tilemap;
        private Tilemap m_surfaceCoverTilemap;
        private TerrainSurfaceLayerSource m_surfaceCoverSource;
        private TerrainNavigationMap m_navigationMap;
        private ElementReactionSystem m_reactionSystem;
        private Vector3Int m_testCell;

        [SetUp]
        public void SetUp()
        {
            m_gridObject = new GameObject("元素反应测试 Grid", typeof(Grid));
            GameObject tilemapObject = new(
                "地形规则",
                typeof(Tilemap),
                typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(m_gridObject.transform);
            m_tilemap = tilemapObject.GetComponent<Tilemap>();
            GameObject coverTilemapObject = new(
                "上层地表",
                typeof(Tilemap),
                typeof(TilemapRenderer));
            coverTilemapObject.transform.SetParent(m_gridObject.transform);
            m_surfaceCoverTilemap = coverTilemapObject.GetComponent<Tilemap>();

            m_navigationMap = m_gridObject.AddComponent<TerrainNavigationMap>();
            SetPrivateField(m_navigationMap, "m_ruleTilemap", m_tilemap);

            GameObject systemObject = new("元素反应系统");
            m_createdObjects.Add(systemObject);
            m_reactionSystem = systemObject.AddComponent<ElementReactionSystem>();
            SetPrivateField(m_reactionSystem, "m_navigationMap", m_navigationMap);
            SetPrivateField(m_reactionSystem, "m_initialized", true);

            RegisterStateDefinition(
                ETerrainElementStateKind.Burning,
                defaultDuration: 1.0f,
                traversalMultiplier: 3.0f);
            RegisterStateDefinition(
                ETerrainElementStateKind.Wet,
                defaultDuration: 2.0f,
                traversalMultiplier: 1.0f);
            RegisterStateDefinition(
                ETerrainElementStateKind.Oiled,
                defaultDuration: 4.0f,
                traversalMultiplier: 1.0f);
            RegisterStateDefinition(
                ETerrainElementStateKind.Electrified,
                defaultDuration: 1.5f,
                traversalMultiplier: 1.5f);

            RegisterReaction(CreateElementReaction(
                "fire-grass",
                EWorldElementKind.Fire,
                priority: 10,
                requireEffectiveSurface: null,
                requireSurfaceCover: ETerrainSurfaceCoverKind.Grass,
                requiredSurfaceCoverTraits: ETerrainSurfaceCoverTraits.Flammable,
                requiredStates: ETerrainRuntimeSurfaceState.None,
                CreateOperation(
                    EElementReactionOperationKind.AddOrRefreshState,
                    stateKind: ETerrainElementStateKind.Burning)));
            RegisterReaction(CreateElementReaction(
                "oil-surface",
                EWorldElementKind.Oil,
                priority: 10,
                requireEffectiveSurface: null,
                requireSurfaceCover: ETerrainSurfaceCoverKind.Grass,
                requiredSurfaceCoverTraits: ETerrainSurfaceCoverTraits.None,
                requiredStates: ETerrainRuntimeSurfaceState.None,
                CreateOperation(
                    EElementReactionOperationKind.AddOrRefreshState,
                    stateKind: ETerrainElementStateKind.Oiled)));
            RegisterReaction(CreateElementReaction(
                "fire-oiled",
                EWorldElementKind.Fire,
                priority: 20,
                requireEffectiveSurface: null,
                requireSurfaceCover: ETerrainSurfaceCoverKind.Grass,
                requiredSurfaceCoverTraits: ETerrainSurfaceCoverTraits.Flammable,
                requiredStates: ETerrainRuntimeSurfaceState.Oiled,
                CreateOperation(
                    EElementReactionOperationKind.AddOrRefreshState,
                    stateKind: ETerrainElementStateKind.Burning,
                    durationOverride: 5.0f)));
            RegisterReaction(CreateElementReaction(
                "water-burning",
                EWorldElementKind.Water,
                priority: 30,
                requireEffectiveSurface: null,
                requireSurfaceCover: null,
                requiredSurfaceCoverTraits: ETerrainSurfaceCoverTraits.None,
                requiredStates: ETerrainRuntimeSurfaceState.Burning,
                CreateOperation(
                    EElementReactionOperationKind.RemoveState,
                    stateKind: ETerrainElementStateKind.Burning),
                CreateOperation(
                    EElementReactionOperationKind.AddOrRefreshState,
                    stateKind: ETerrainElementStateKind.Wet),
                CreateOperation(
                    EElementReactionOperationKind.EmitPresentationSignal,
                    presentationSignal: EElementPresentationSignal.Steam)));
            RegisterReaction(CreateElementReaction(
                "electricity-wet",
                EWorldElementKind.Electricity,
                priority: 10,
                requireEffectiveSurface: null,
                requireSurfaceCover: null,
                requiredSurfaceCoverTraits: ETerrainSurfaceCoverTraits.None,
                requiredStates: ETerrainRuntimeSurfaceState.Wet,
                CreateOperation(
                    EElementReactionOperationKind.AddOrRefreshState,
                    stateKind: ETerrainElementStateKind.Electrified)));
            RegisterReaction(CreateExpirationReaction(
                "burning-grass-expired",
                ETerrainElementStateKind.Burning,
                ETerrainSurfaceCoverKind.Grass,
                CreateOperation(
                    EElementReactionOperationKind.RemoveSurfaceCover)));

            m_testCell = Vector3Int.zero;
            TerrainNavigationTile dirtTile =
                ScriptableObject.CreateInstance<TerrainNavigationTile>();
            m_createdObjects.Add(dirtTile);
            SetPrivateField(dirtTile, "m_walkable", true);
            SetPrivateField(dirtTile, "m_surfaceKind", ETerrainSurfaceKind.Dirt);
            SetPrivateField(dirtTile, "m_traversalCost", 1.0f);
            m_tilemap.SetTile(m_testCell, dirtTile);

            Tile grassCoverTile = ScriptableObject.CreateInstance<Tile>();
            m_createdObjects.Add(grassCoverTile);
            m_surfaceCoverTilemap.SetTile(m_testCell, grassCoverTile);
            TerrainSurfaceCoverTileMapping grassCoverMapping =
                CreateSurfaceCoverMapping(
                    grassCoverTile,
                    ETerrainSurfaceCoverKind.Grass,
                    ETerrainSurfaceCoverTraits.Flammable |
                    ETerrainSurfaceCoverTraits.Destructible |
                    ETerrainSurfaceCoverTraits.Regrowable);
            m_surfaceCoverSource = CreateSurfaceLayerSource(
                TerrainSurfaceCoverSourceReference.DefaultSurfaceLayerSourceId,
                ETerrainSurfaceLayerRole.SurfaceCover,
                m_surfaceCoverTilemap,
                priority: 0,
                grassCoverMapping);
            SetPrivateField(
                m_navigationMap,
                "m_surfaceLayerSources",
                new[] { m_surfaceCoverSource });
            m_navigationMap.RefreshNavigationData();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_gridObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_gridObject);
            }

            for (int i = 0; i < m_createdObjects.Count; i++)
            {
                if (m_createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(m_createdObjects[i]);
                }
            }

            m_createdObjects.Clear();
        }

        [Test]
        public void ApplyFireToGrass_AddsBurningAndRaisesTraversalCost()
        {
            bool changed = m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Fire, 0.6f));

            Assert.IsTrue(changed);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceKind.Dirt, sample.BaseSurface);
            Assert.AreEqual(ETerrainSurfaceKind.Dirt, sample.EffectiveSurface);
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, sample.BaseSurfaceCover);
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, sample.EffectiveSurfaceCover);
            Assert.AreEqual(
                ETerrainSurfaceLayerRole.SurfaceCover,
                sample.SurfaceCoverSource.Role);
            Assert.AreEqual(
                TerrainSurfaceCoverSourceReference.DefaultSurfaceLayerSourceId,
                sample.SurfaceCoverSource.SourceId);
            Assert.AreEqual(ETerrainSurfaceCoverLifecycle.Alive, sample.SurfaceCoverLifecycle);
            Assert.IsTrue(sample.IsSurfaceCoverFlammable);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.Burning,
                sample.RuntimeState);
            Assert.AreEqual(1.0f, sample.BaseTraversalCost);
            Assert.AreEqual(3.0f, sample.EffectiveTraversalCost);
            Assert.AreEqual(1, m_reactionSystem.ActiveTimedCellCount);
        }

        [Test]
        public void ApplyFireToMappedDecorationLayer_AddsBurning()
        {
            Vector3Int decorationCell = new(2, 0, 0);
            TerrainNavigationTile dirtTile =
                ScriptableObject.CreateInstance<TerrainNavigationTile>();
            m_createdObjects.Add(dirtTile);
            SetPrivateField(dirtTile, "m_walkable", true);
            SetPrivateField(dirtTile, "m_surfaceKind", ETerrainSurfaceKind.Dirt);
            SetPrivateField(dirtTile, "m_traversalCost", 1.0f);
            m_tilemap.SetTile(decorationCell, dirtTile);

            GameObject decorationObject = new(
                "地表装饰",
                typeof(Tilemap),
                typeof(TilemapRenderer));
            decorationObject.transform.SetParent(m_gridObject.transform);
            Tilemap decorationTilemap = decorationObject.GetComponent<Tilemap>();
            Tile decorationGrassTile = ScriptableObject.CreateInstance<Tile>();
            m_createdObjects.Add(decorationGrassTile);
            decorationTilemap.SetTile(decorationCell, decorationGrassTile);

            TerrainSurfaceCoverTileMapping decorationGrassMapping =
                CreateSurfaceCoverMapping(
                    decorationGrassTile,
                    ETerrainSurfaceCoverKind.Grass,
                    ETerrainSurfaceCoverTraits.Flammable |
                    ETerrainSurfaceCoverTraits.Destructible);

            TerrainSurfaceLayerSource decorationSource = CreateSurfaceLayerSource(
                sourceId: 10,
                ETerrainSurfaceLayerRole.Decoration,
                decorationTilemap,
                priority: 10,
                decorationGrassMapping);
            SetPrivateField(
                m_navigationMap,
                "m_surfaceLayerSources",
                new[] { m_surfaceCoverSource, decorationSource });
            m_navigationMap.RefreshNavigationData();

            bool changed = m_reactionSystem.Apply(
                CreateApplication(
                    EWorldElementKind.Fire,
                    0.6f,
                    decorationCell));

            Assert.IsTrue(changed);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                decorationCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, sample.BaseSurfaceCover);
            Assert.AreEqual(ETerrainSurfaceLayerRole.Decoration, sample.SurfaceCoverSource.Role);
            Assert.AreEqual(10, sample.SurfaceCoverSource.SourceId);
            Assert.IsTrue(sample.IsSurfaceCoverFlammable);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.Burning,
                sample.RuntimeState);
        }

        [Test]
        public void OilFireWaterAndElectricity_ResolveThroughConfiguredRules()
        {
            Assert.IsTrue(m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Oil, 1.0f)));
            Assert.IsTrue(m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Fire, 0.8f)));

            Assert.IsTrue(m_navigationMap.TryGetRuntimeState(
                m_testCell,
                out TerrainCellRuntimeState burningState));
            Assert.IsTrue(burningState.TryGetState(
                ETerrainElementStateKind.Burning,
                out TerrainElementStateInstance burning));
            Assert.AreEqual(5.0f, burning.RemainingDuration);
            Assert.AreEqual(0.8f, burning.Intensity);

            Assert.IsTrue(m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Water, 1.0f)));
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample wetSample));
            Assert.IsFalse(
                (wetSample.RuntimeState & ETerrainRuntimeSurfaceState.Burning) != 0);
            Assert.IsTrue(
                (wetSample.RuntimeState & ETerrainRuntimeSurfaceState.Wet) != 0);
            Assert.AreEqual(1.0f, wetSample.EffectiveTraversalCost);
            Assert.AreEqual(1, m_reactionSystem.ActiveTimedCellCount);

            Assert.IsTrue(m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Electricity, 1.0f)));
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample electrifiedSample));
            Assert.IsTrue(
                (electrifiedSample.RuntimeState &
                 ETerrainRuntimeSurfaceState.Electrified) != 0);
        }

        [Test]
        public void ApplyFireToBareDirt_DoesNotAddBurning()
        {
            m_surfaceCoverTilemap.SetTile(m_testCell, null);
            m_navigationMap.RefreshNavigationData();

            bool changed = m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Fire, 1.0f));

            Assert.IsFalse(changed);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceKind.Dirt, sample.EffectiveSurface);
            Assert.AreEqual(ETerrainSurfaceCoverKind.None, sample.EffectiveSurfaceCover);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.None,
                sample.RuntimeState);
            Assert.AreEqual(0, m_reactionSystem.ActiveTimedCellCount);
        }

        [Test]
        public void ConfiguredSurfaceLayerSources_DoNotFallbackToLegacyCover()
        {
            Tile legacyOnlyTile = ScriptableObject.CreateInstance<Tile>();
            m_createdObjects.Add(legacyOnlyTile);
            m_surfaceCoverTilemap.SetTile(m_testCell, legacyOnlyTile);
            SetPrivateField(
                m_navigationMap,
                "m_surfaceCoverTilemap",
                m_surfaceCoverTilemap);
            SetPrivateField(
                m_navigationMap,
                "m_surfaceCoverTileMappings",
                new[]
                {
                    CreateSurfaceCoverMapping(
                        legacyOnlyTile,
                        ETerrainSurfaceCoverKind.Grass,
                        ETerrainSurfaceCoverTraits.Flammable)
                });
            SetPrivateField(
                m_navigationMap,
                "m_surfaceLayerSources",
                new[]
                {
                    CreateSurfaceLayerSource(
                        sourceId: 20,
                        ETerrainSurfaceLayerRole.Decoration,
                        m_surfaceCoverTilemap,
                        priority: 0)
                });
            m_navigationMap.RefreshNavigationData();

            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceCoverKind.None, sample.BaseSurfaceCover);
            Assert.AreEqual(
                TerrainSurfaceCoverSourceReference.None.SourceId,
                sample.SurfaceCoverSource.SourceId);
            Assert.AreEqual(
                ETerrainSurfaceLayerRole.None,
                sample.SurfaceCoverSource.Role);
        }

        [Test]
        public void LegacySurfaceCoverFallback_WorksWhenNoSurfaceLayerSourcesConfigured()
        {
            Tile legacyTile = ScriptableObject.CreateInstance<Tile>();
            m_createdObjects.Add(legacyTile);
            m_surfaceCoverTilemap.SetTile(m_testCell, legacyTile);
            SetPrivateField(
                m_navigationMap,
                "m_surfaceLayerSources",
                Array.Empty<TerrainSurfaceLayerSource>());
            SetPrivateField(
                m_navigationMap,
                "m_surfaceCoverTilemap",
                m_surfaceCoverTilemap);
            SetPrivateField(
                m_navigationMap,
                "m_surfaceCoverTileMappings",
                new[]
                {
                    CreateSurfaceCoverMapping(
                        legacyTile,
                        ETerrainSurfaceCoverKind.Grass,
                        ETerrainSurfaceCoverTraits.Flammable)
                });
            m_navigationMap.RefreshNavigationData();

            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, sample.BaseSurfaceCover);
            Assert.AreEqual(
                TerrainSurfaceCoverSourceReference.LegacySurfaceCoverSourceId,
                sample.SurfaceCoverSource.SourceId);
            Assert.AreEqual(
                ETerrainSurfaceLayerRole.SurfaceCover,
                sample.SurfaceCoverSource.Role);
        }

        [Test]
        public void BurningExpiration_RemovesGrassCoverAndKeepsDirtGround()
        {
            Assert.IsTrue(m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Fire, 1.0f)));

            InvokePrivate(
                m_reactionSystem,
                "AdvanceTimedStates",
                1.0f);

            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceKind.Dirt, sample.BaseSurface);
            Assert.AreEqual(ETerrainSurfaceKind.Dirt, sample.EffectiveSurface);
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, sample.BaseSurfaceCover);
            Assert.AreEqual(ETerrainSurfaceCoverKind.None, sample.EffectiveSurfaceCover);
            Assert.AreEqual(ETerrainSurfaceCoverLifecycle.Removed, sample.SurfaceCoverLifecycle);
            Assert.IsFalse(sample.HasSurfaceCover);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.None,
                sample.RuntimeState);
            Assert.AreEqual(1.0f, sample.EffectiveTraversalCost);
            Assert.AreEqual(0, m_reactionSystem.ActiveTimedCellCount);

            bool reapplied = m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Fire, 1.0f));

            Assert.IsFalse(reapplied, "草覆盖已移除的 Dirt 格不应再次匹配 Fire + Grass 规则。");
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample reappliedSample));
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, reappliedSample.BaseSurfaceCover);
            Assert.AreEqual(ETerrainSurfaceCoverKind.None, reappliedSample.EffectiveSurfaceCover);
            Assert.AreEqual(ETerrainSurfaceCoverLifecycle.Removed, reappliedSample.SurfaceCoverLifecycle);
            Assert.AreEqual(ETerrainRuntimeSurfaceState.None, reappliedSample.RuntimeState);
            Assert.AreEqual(0, m_reactionSystem.ActiveTimedCellCount);
        }

        [Test]
        public void MapUnloading_ClearsTransientStateAndStopsTiming()
        {
            Assert.IsTrue(m_reactionSystem.Apply(
                CreateApplication(EWorldElementKind.Fire, 1.0f)));

            m_reactionSystem.OnMapUnloading();

            Assert.IsNull(m_reactionSystem.BoundNavigationMap);
            Assert.AreEqual(0, m_reactionSystem.ActiveTimedCellCount);
            Assert.AreEqual(0, m_navigationMap.RuntimeStateCount);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                m_testCell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(ETerrainSurfaceKind.Dirt, sample.EffectiveSurface);
            Assert.AreEqual(ETerrainSurfaceCoverKind.Grass, sample.EffectiveSurfaceCover);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.None,
                sample.RuntimeState);
        }

        private ElementApplication CreateApplication(
            EWorldElementKind elementKind,
            float intensity)
        {
            return CreateApplication(elementKind, intensity, m_testCell);
        }

        private ElementApplication CreateApplication(
            EWorldElementKind elementKind,
            float intensity,
            Vector3Int cell)
        {
            return new ElementApplication(
                elementKind,
                intensity,
                0.1f,
                ElementArea.Cone(1.0f, 45.0f),
                m_tilemap.GetCellCenterWorld(cell),
                Vector2.right,
                sourceAbilityCode: 9001);
        }

        private void RegisterStateDefinition(
            ETerrainElementStateKind stateKind,
            float defaultDuration,
            float traversalMultiplier)
        {
            TerrainElementStateDefinition definition =
                ScriptableObject.CreateInstance<TerrainElementStateDefinition>();
            m_createdObjects.Add(definition);
            SetPrivateField(definition, "m_stateKind", stateKind);
            SetPrivateField(definition, "m_defaultDuration", defaultDuration);
            SetPrivateField(
                definition,
                "m_mergePolicy",
                ETerrainStateMergePolicy.RefreshDuration);
            SetPrivateField(
                definition,
                "m_traversalCostMultiplier",
                traversalMultiplier);

            FieldInfo definitionsField = typeof(ElementReactionSystem).GetField(
                "m_stateDefinitions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(definitionsField);
            IDictionary definitions =
                definitionsField.GetValue(m_reactionSystem) as IDictionary;
            Assert.IsNotNull(definitions);

            Type bindingType = typeof(ElementReactionSystem).GetNestedType(
                "StateDefinitionBinding",
                BindingFlags.NonPublic);
            Assert.IsNotNull(bindingType);
            object binding = Activator.CreateInstance(
                bindingType,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                binder: null,
                args: new object[]
                {
                    $"state-{stateKind.ToString().ToLowerInvariant()}",
                    definition
                },
                culture: null);
            definitions.Add(stateKind, binding);
        }

        private void RegisterReaction(
            (string stableId, ElementReactionDefinition definition) reaction)
        {
            FieldInfo candidatesField = typeof(ElementReactionSystem).GetField(
                "m_reactionCandidates",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(candidatesField);
            List<ElementReactionCandidate> candidates =
                candidatesField.GetValue(m_reactionSystem) as
                    List<ElementReactionCandidate>;
            Assert.IsNotNull(candidates);
            candidates.Add(new ElementReactionCandidate(
                reaction.stableId,
                reaction.definition));
        }

        private (string stableId, ElementReactionDefinition definition)
            CreateElementReaction(
                string stableId,
                EWorldElementKind elementKind,
                int priority,
                ETerrainSurfaceKind? requireEffectiveSurface,
                ETerrainSurfaceCoverKind? requireSurfaceCover,
                ETerrainSurfaceCoverTraits requiredSurfaceCoverTraits,
                ETerrainRuntimeSurfaceState requiredStates,
                params ElementReactionOperation[] operations)
        {
            ElementReactionDefinition definition =
                ScriptableObject.CreateInstance<ElementReactionDefinition>();
            m_createdObjects.Add(definition);
            SetPrivateField(
                definition,
                "m_trigger",
                EElementReactionTrigger.OnElementApplied);
            SetPrivateField(definition, "m_elementKind", elementKind);
            SetPrivateField(definition, "m_priority", priority);
            SetPrivateField(definition, "m_requiredStates", requiredStates);
            SetPrivateField(definition, "m_operations", operations);
            if (requireEffectiveSurface.HasValue)
            {
                SetPrivateField(
                    definition,
                    "m_requireEffectiveSurface",
                    true);
                SetPrivateField(
                    definition,
                    "m_effectiveSurface",
                    requireEffectiveSurface.Value);
            }

            if (requireSurfaceCover.HasValue)
            {
                SetPrivateField(definition, "m_requireSurfaceCover", true);
                SetPrivateField(
                    definition,
                    "m_surfaceCover",
                    requireSurfaceCover.Value);
            }

            SetPrivateField(
                definition,
                "m_requiredSurfaceCoverTraits",
                requiredSurfaceCoverTraits);
            return (stableId, definition);
        }

        private (string stableId, ElementReactionDefinition definition)
            CreateExpirationReaction(
                string stableId,
                ETerrainElementStateKind expiredStateKind,
                ETerrainSurfaceCoverKind surfaceCover,
                params ElementReactionOperation[] operations)
        {
            ElementReactionDefinition definition =
                ScriptableObject.CreateInstance<ElementReactionDefinition>();
            m_createdObjects.Add(definition);
            SetPrivateField(
                definition,
                "m_trigger",
                EElementReactionTrigger.OnStateExpired);
            SetPrivateField(
                definition,
                "m_expiredStateKind",
                expiredStateKind);
            SetPrivateField(definition, "m_requireSurfaceCover", true);
            SetPrivateField(definition, "m_surfaceCover", surfaceCover);
            SetPrivateField(definition, "m_priority", 10);
            SetPrivateField(definition, "m_operations", operations);
            return (stableId, definition);
        }

        private static ElementReactionOperation CreateOperation(
            EElementReactionOperationKind kind,
            ETerrainElementStateKind stateKind =
                ETerrainElementStateKind.None,
            float durationOverride = 0.0f,
            ETerrainSurfaceKind surfaceKind = ETerrainSurfaceKind.None,
            ETerrainSurfaceCoverKind surfaceCoverKind =
                ETerrainSurfaceCoverKind.None,
            EElementPresentationSignal presentationSignal =
                EElementPresentationSignal.None)
        {
            ElementReactionOperation operation = new();
            SetPrivateField(operation, "m_kind", kind);
            SetPrivateField(operation, "m_stateKind", stateKind);
            SetPrivateField(
                operation,
                "m_durationOverride",
                durationOverride);
            SetPrivateField(operation, "m_surfaceKind", surfaceKind);
            SetPrivateField(operation, "m_surfaceCoverKind", surfaceCoverKind);
            SetPrivateField(
                operation,
                "m_presentationSignal",
                presentationSignal);
            return operation;
        }

        private static TerrainSurfaceCoverTileMapping CreateSurfaceCoverMapping(
            TileBase tile,
            ETerrainSurfaceCoverKind coverKind,
            ETerrainSurfaceCoverTraits traits)
        {
            TerrainSurfaceCoverTileMapping mapping = new();
            SetPrivateField(mapping, "m_tile", tile);
            SetPrivateField(mapping, "m_coverKind", coverKind);
            SetPrivateField(mapping, "m_traits", traits);
            return mapping;
        }

        private static TerrainSurfaceLayerSource CreateSurfaceLayerSource(
            int sourceId,
            ETerrainSurfaceLayerRole role,
            Tilemap tilemap,
            int priority,
            params TerrainSurfaceCoverTileMapping[] mappings)
        {
            TerrainSurfaceLayerSource source = new();
            SetPrivateField(source, "m_sourceId", sourceId);
            SetPrivateField(source, "m_role", role);
            SetPrivateField(source, "m_tilemap", tilemap);
            SetPrivateField(source, "m_priority", priority);
            SetPrivateField(source, "m_surfaceCoverTileMappings", mappings);
            return source;
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"找不到方法：{target.GetType().Name}.{methodName}");
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
