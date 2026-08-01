using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore.Tests
{
    public sealed class TerrainNavigationMapEditModeTests
    {
        private GameObject m_gridObject;
        private GameObject m_tilemapObject;
        private Tilemap m_tilemap;
        private TerrainNavigationMap m_navigationMap;

        [SetUp]
        public void SetUp()
        {
            m_gridObject = new GameObject("地形测试 Grid", typeof(Grid));
            m_tilemapObject = new GameObject("地形规则", typeof(Tilemap), typeof(TilemapRenderer));
            m_tilemapObject.transform.SetParent(m_gridObject.transform);
            m_tilemap = m_tilemapObject.GetComponent<Tilemap>();
            m_navigationMap = m_gridObject.AddComponent<TerrainNavigationMap>();
            SetPrivateField(m_navigationMap, "m_ruleTilemap", m_tilemap);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_gridObject);
        }

        [Test]
        public void BuildWorldPath_UsesSameNorthWestRampTileCentersInBothDirections()
        {
            TerrainNavigationTile lowGround = CreateTile(0, ETerrainTransitionKind.Ground);
            TerrainNavigationTile lowRamp = CreateTile(
                0,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthWest);
            TerrainNavigationTile highRamp = CreateTile(
                1,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthWest);
            TerrainNavigationTile highGround = CreateTile(1, ETerrainTransitionKind.Ground);

            Vector3Int lowGroundCell = new(2, 0);
            Vector3Int lowRampCell = new(1, 0);
            Vector3Int highRampCornerCell = new(1, 1);
            Vector3Int highRampExitCell = new(0, 1);
            Vector3Int highGroundCell = new(-1, 1);

            m_tilemap.SetTile(lowGroundCell, lowGround);
            m_tilemap.SetTile(lowRampCell, lowRamp);
            m_tilemap.SetTile(highRampCornerCell, highRamp);
            m_tilemap.SetTile(highRampExitCell, highRamp);
            m_tilemap.SetTile(highGroundCell, highGround);
            m_navigationMap.RefreshNavigationData();

            bool foundForward = m_navigationMap.TryBuildWorldPath(
                m_tilemap.GetCellCenterWorld(lowGroundCell),
                m_tilemap.GetCellCenterWorld(highGroundCell),
                out Vector2[] forwardPath);
            bool foundReverse = m_navigationMap.TryBuildWorldPath(
                m_tilemap.GetCellCenterWorld(highGroundCell),
                m_tilemap.GetCellCenterWorld(lowGroundCell),
                out Vector2[] reversePath);

            Vector2 lowEntrance = m_tilemap.GetCellCenterWorld(lowRampCell);
            Vector2 highCorner = m_tilemap.GetCellCenterWorld(highRampCornerCell);
            Vector2 highExit = m_tilemap.GetCellCenterWorld(highRampExitCell);

            Assert.IsTrue(foundForward);
            Assert.AreEqual(4, forwardPath.Length);
            Assert.AreEqual(lowEntrance, forwardPath[0]);
            Assert.AreEqual(highCorner, forwardPath[1]);
            Assert.AreEqual(highExit, forwardPath[2]);
            Assert.AreEqual(
                (Vector2)m_tilemap.GetCellCenterWorld(highGroundCell),
                forwardPath[3]);

            Assert.IsTrue(foundReverse);
            Assert.AreEqual(4, reversePath.Length);
            Assert.AreEqual(highExit, reversePath[0]);
            Assert.AreEqual(highCorner, reversePath[1]);
            Assert.AreEqual(lowEntrance, reversePath[2]);
            Assert.AreEqual(
                (Vector2)m_tilemap.GetCellCenterWorld(lowGroundCell),
                reversePath[3]);
        }

        [Test]
        public void BuildWorldPathWithoutDebug_PreservesLastVisiblePlayerPath()
        {
            TerrainNavigationTile ground = CreateTile(0, ETerrainTransitionKind.Ground);
            Vector3Int startCell = new(0, 0);
            Vector3Int playerGoalCell = new(1, 0);
            Vector3Int aiGoalCell = new(2, 0);
            m_tilemap.SetTile(startCell, ground);
            m_tilemap.SetTile(playerGoalCell, ground);
            m_tilemap.SetTile(aiGoalCell, ground);
            m_navigationMap.RefreshNavigationData();

            Vector2 start = m_tilemap.GetCellCenterWorld(startCell);
            Vector2 playerGoal = m_tilemap.GetCellCenterWorld(playerGoalCell);
            Vector2 aiGoal = m_tilemap.GetCellCenterWorld(aiGoalCell);
            Assert.IsTrue(m_navigationMap.TryBuildWorldPath(start, playerGoal, out Vector2[] playerPath));

            Assert.IsTrue(m_navigationMap.TryBuildWorldPathWithoutDebug(start, aiGoal, out Vector2[] aiPath));

            Assert.That(aiPath[^1], Is.EqualTo(aiGoal));
            Assert.That(GetPrivateField<Vector2>(m_navigationMap, "m_lastDebugDestination"), Is.EqualTo(playerGoal));
            CollectionAssert.AreEqual(
                playerPath,
                GetPrivateField<Vector2[]>(m_navigationMap, "m_lastDebugWorldPath"));
            Assert.That(GetPrivateField<bool>(m_navigationMap, "m_hasDebugPathRequest"), Is.True);
            Assert.That(GetPrivateField<bool>(m_navigationMap, "m_lastDebugPathSucceeded"), Is.True);
        }

        [Test]
        public void BuildWorldPath_ProjectsNorthEastRampOntoAuthoredCenterLine()
        {
            TerrainNavigationTile lowGround = CreateTile(0, ETerrainTransitionKind.Ground);
            TerrainNavigationTile lowRamp = CreateTile(
                0,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthEast);
            TerrainNavigationTile highRamp = CreateTile(
                1,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthEast);
            TerrainNavigationTile highGround = CreateTile(1, ETerrainTransitionKind.Ground);

            Vector3Int lowGroundCell = new(-1, 0);
            Vector3Int lowRampCell = new(0, 0);
            Vector3Int highRampCornerCell = new(0, 1);
            Vector3Int highRampExitCell = new(1, 1);
            Vector3Int highGroundCell = new(2, 1);

            m_tilemap.SetTile(lowGroundCell, lowGround);
            m_tilemap.SetTile(lowRampCell, lowRamp);
            m_tilemap.SetTile(highRampCornerCell, highRamp);
            m_tilemap.SetTile(highRampExitCell, highRamp);
            m_tilemap.SetTile(highGroundCell, highGround);
            m_navigationMap.RefreshNavigationData();

            bool found = m_navigationMap.TryBuildWorldPath(
                m_tilemap.GetCellCenterWorld(lowGroundCell),
                m_tilemap.GetCellCenterWorld(highGroundCell),
                out Vector2[] path);

            Vector2 lowEntrance = m_tilemap.GetCellCenterWorld(lowRampCell);
            Vector2 highCorner = m_tilemap.GetCellCenterWorld(highRampCornerCell);
            Vector2 highExit = m_tilemap.GetCellCenterWorld(highRampExitCell);

            Assert.IsTrue(found);
            Assert.AreEqual(4, path.Length);
            Assert.AreEqual(lowEntrance, path[0]);
            Assert.AreEqual(highCorner, path[1]);
            Assert.AreEqual(highExit, path[2]);
            Assert.AreEqual(
                (Vector2)m_tilemap.GetCellCenterWorld(highGroundCell),
                path[3]);
        }

        [Test]
        public void ResolveRampMovementDirection_TargetsNextRampTileCenterForManualDownhillInput()
        {
            TerrainNavigationTile lowRamp = CreateTile(
                0,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthWest);
            TerrainNavigationTile highRamp = CreateTile(
                1,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthWest);

            Vector3Int lowRampCell = new(1, 0);
            Vector3Int highRampCornerCell = new(1, 1);
            Vector3Int highRampExitCell = new(0, 1);
            m_tilemap.SetTile(lowRampCell, lowRamp);
            m_tilemap.SetTile(highRampCornerCell, highRamp);
            m_tilemap.SetTile(highRampExitCell, highRamp);
            m_navigationMap.RefreshNavigationData();

            Vector2 currentWorld =
                (Vector2)m_tilemap.GetCellCenterWorld(highRampExitCell) +
                new Vector2(0.0f, -0.25f);
            Vector2 expectedDirection =
                ((Vector2)m_tilemap.GetCellCenterWorld(highRampCornerCell) - currentWorld).normalized;

            bool resolved = m_navigationMap.TryResolveRampMovementDirection(
                currentWorld,
                Vector2.right,
                out Vector2 resolvedDirection);

            Assert.IsTrue(resolved);
            Assert.That(Vector2.Dot(resolvedDirection, expectedDirection), Is.GreaterThan(0.999f));
            Assert.That(resolvedDirection.y, Is.GreaterThan(0.0f));
        }

        [Test]
        public void BuildWorldPath_RejectsElevationChangeAgainstRampDirection()
        {
            TerrainNavigationTile lowRamp = CreateTile(
                0,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthWest);
            TerrainNavigationTile highRamp = CreateTile(
                1,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthWest);

            Vector3Int lowCell = new(0, 0);
            Vector3Int wrongHighCell = new(1, 0);
            m_tilemap.SetTile(lowCell, lowRamp);
            m_tilemap.SetTile(wrongHighCell, highRamp);
            m_navigationMap.RefreshNavigationData();

            bool found = m_navigationMap.TryBuildWorldPath(
                m_tilemap.GetCellCenterWorld(lowCell),
                m_tilemap.GetCellCenterWorld(wrongHighCell),
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void BuildWorldPath_RejectsElevationChangeWithoutRamp()
        {
            m_tilemap.SetTile(new Vector3Int(0, 0), CreateTile(0, ETerrainTransitionKind.Ground));
            m_tilemap.SetTile(new Vector3Int(1, 0), CreateTile(1, ETerrainTransitionKind.Ground));
            m_navigationMap.RefreshNavigationData();

            bool found = m_navigationMap.TryBuildWorldPath(
                m_tilemap.GetCellCenterWorld(new Vector3Int(0, 0)),
                m_tilemap.GetCellCenterWorld(new Vector3Int(1, 0)),
                out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void BuildWorldPath_ExpandsDiagonalStepThroughWalkableOrthogonalCell()
        {
            TerrainNavigationTile ground = CreateTile(0, ETerrainTransitionKind.Ground);
            m_tilemap.SetTile(new Vector3Int(0, 0), ground);
            m_tilemap.SetTile(new Vector3Int(1, 0), ground);
            m_tilemap.SetTile(new Vector3Int(1, 1), ground);
            m_navigationMap.RefreshNavigationData();

            Vector2 start = m_tilemap.GetCellCenterWorld(new Vector3Int(0, 0));
            bool found = m_navigationMap.TryBuildWorldPath(
                start,
                m_tilemap.GetCellCenterWorld(new Vector3Int(1, 1)),
                out Vector2[] path);

            Assert.IsTrue(found);
            Assert.AreEqual(2, path.Length);
            Assert.AreEqual(
                (Vector2)m_tilemap.GetCellCenterWorld(new Vector3Int(1, 0)),
                path[0]);
            Assert.AreEqual(
                (Vector2)m_tilemap.GetCellCenterWorld(new Vector3Int(1, 1)),
                path[1]);
            AssertAxisAligned(start, path);
        }

        [Test]
        public void RuntimeSurfaceState_DoesNotMutateAuthoredTile()
        {
            TerrainNavigationTile grass = CreateTile(0, ETerrainTransitionKind.Ground);
            Vector3Int cell = new(0, 0);
            m_tilemap.SetTile(cell, grass);
            m_navigationMap.RefreshNavigationData();
            Vector2 world = m_tilemap.GetCellCenterWorld(cell);

            Assert.IsTrue(m_navigationMap.SetRuntimeSurfaceState(
                world,
                ETerrainRuntimeSurfaceState.Wet | ETerrainRuntimeSurfaceState.Electrified));
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(world, out TerrainSurfaceSample sample));

            Assert.AreEqual(ETerrainSurfaceKind.Grass, grass.SurfaceKind);
            Assert.AreEqual(
                ETerrainRuntimeSurfaceState.Wet | ETerrainRuntimeSurfaceState.Electrified,
                sample.RuntimeState);
        }

        [Test]
        public void CollectAffectedCells_RejectsHighGroundAcrossCliff()
        {
            TerrainNavigationTile lowGround = CreateTile(0, ETerrainTransitionKind.Ground);
            TerrainNavigationTile highGround = CreateTile(1, ETerrainTransitionKind.Ground);
            Vector3Int originCell = new(0, 0);
            Vector3Int lowCell = new(1, 0);
            Vector3Int highCell = new(2, 0);
            m_tilemap.SetTile(originCell, lowGround);
            m_tilemap.SetTile(lowCell, lowGround);
            m_tilemap.SetTile(highCell, highGround);
            m_navigationMap.RefreshNavigationData();
            ElementApplication application = new(
                EWorldElementKind.Fire,
                1.0f,
                0.25f,
                ElementArea.Cone(3.0f, 30.0f),
                m_tilemap.GetCellCenterWorld(originCell),
                Vector2.right);
            System.Collections.Generic.List<Vector3Int> affectedCells = new();

            bool found = m_navigationMap.TryCollectAffectedCells(
                application,
                affectedCells);

            Assert.IsTrue(found);
            CollectionAssert.Contains(affectedCells, originCell);
            CollectionAssert.Contains(affectedCells, lowCell);
            CollectionAssert.DoesNotContain(affectedCells, highCell);
        }

        [Test]
        public void CollectAffectedCells_ReachesHighGroundThroughRamp()
        {
            TerrainNavigationTile lowGround = CreateTile(0, ETerrainTransitionKind.Ground);
            TerrainNavigationTile lowRamp = CreateTile(
                0,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthEast);
            TerrainNavigationTile highRamp = CreateTile(
                1,
                ETerrainTransitionKind.Ramp,
                ETerrainRampDirection.NorthEast);
            TerrainNavigationTile highGround = CreateTile(1, ETerrainTransitionKind.Ground);
            Vector3Int originCell = new(0, 0);
            Vector3Int lowRampCell = new(1, 0);
            Vector3Int highRampCell = new(2, 0);
            Vector3Int highGroundCell = new(3, 0);
            m_tilemap.SetTile(originCell, lowGround);
            m_tilemap.SetTile(lowRampCell, lowRamp);
            m_tilemap.SetTile(highRampCell, highRamp);
            m_tilemap.SetTile(highGroundCell, highGround);
            m_navigationMap.RefreshNavigationData();
            ElementApplication application = new(
                EWorldElementKind.Fire,
                1.0f,
                0.25f,
                ElementArea.Cone(4.0f, 30.0f),
                m_tilemap.GetCellCenterWorld(originCell),
                Vector2.right);
            System.Collections.Generic.List<Vector3Int> affectedCells = new();

            bool found = m_navigationMap.TryCollectAffectedCells(
                application,
                affectedCells);

            Assert.IsTrue(found);
            CollectionAssert.Contains(affectedCells, highRampCell);
            CollectionAssert.Contains(affectedCells, highGroundCell);
        }

        [Test]
        public void RuntimeStateCost_RecalculatesFromAuthoredBaseCost()
        {
            TerrainNavigationTile grass = CreateTile(0, ETerrainTransitionKind.Ground);
            Vector3Int cell = new(0, 0);
            m_tilemap.SetTile(cell, grass);
            m_navigationMap.RefreshNavigationData();
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                cell,
                out TerrainSurfaceSample before));

            TerrainCellRuntimeState runtimeState = GetOrCreateRuntimeState(cell);
            TerrainElementStateSource source = new(null, 101);
            runtimeState.ApplyOrMergeState(
                ETerrainElementStateKind.Burning,
                1.0f,
                3.0f,
                source,
                "fire-grass",
                ETerrainStateMergePolicy.RefreshDuration);
            CommitRuntimeState(cell, before, 3.0f);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                cell,
                out TerrainSurfaceSample burning));

            Assert.AreEqual(1.0f, burning.BaseTraversalCost);
            Assert.AreEqual(3.0f, burning.EffectiveTraversalCost);

            runtimeState.RemoveState(ETerrainElementStateKind.Burning);
            CommitRuntimeState(cell, burning, 1.0f);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                cell,
                out TerrainSurfaceSample cleared));

            Assert.AreEqual(1.0f, cleared.BaseTraversalCost);
            Assert.AreEqual(1.0f, cleared.EffectiveTraversalCost);
        }

        [Test]
        public void TerrainNodeKey_Default_PreservesCellOnDefaultLayer()
        {
            Vector3Int cell = new(3, -2, 0);

            TerrainNodeKey nodeKey = TerrainNodeKey.Default(cell);

            Assert.AreEqual(TerrainNodeKey.DefaultLayerId, nodeKey.LayerId);
            Assert.AreEqual(cell, nodeKey.Cell);
            Assert.IsTrue(nodeKey.IsDefaultLayer);
        }

        [Test]
        public void TerrainNodeKey_SameCellOnDifferentLayers_IsDistinct()
        {
            Vector3Int cell = new(1, 2, 0);
            TerrainNodeKey ground = new(TerrainNodeKey.DefaultLayerId, cell);
            TerrainNodeKey bridge = new(TerrainNodeKey.DefaultLayerId + 1, cell);

            Assert.AreNotEqual(ground, bridge);
            Assert.IsTrue(ground != bridge);
        }

        [Test]
        public void RuntimeState_NonDefaultLayer_IsRejectedBySingleLayerMap()
        {
            TerrainNavigationTile grass = CreateTile(0, ETerrainTransitionKind.Ground);
            Vector3Int cell = new(0, 0);
            TerrainNodeKey unsupportedNode = new(TerrainNodeKey.DefaultLayerId + 1, cell);
            m_tilemap.SetTile(cell, grass);
            m_navigationMap.RefreshNavigationData();

            bool created = TryGetOrCreateRuntimeNodeState(
                unsupportedNode,
                out TerrainCellRuntimeState runtimeState);

            Assert.IsFalse(created);
            Assert.IsNull(runtimeState);
            Assert.IsFalse(m_navigationMap.TryGetSurfaceSample(unsupportedNode, out _));
            Assert.IsFalse(m_navigationMap.TryGetRuntimeNodeState(unsupportedNode, out _));
        }

        [Test]
        public void RuntimeState_NonDefaultLayerWithSource_CanBeCreatedAndSampled()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, cell);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(cell, CreateTile(1, ETerrainTransitionKind.Ground, ETerrainRampDirection.None, ETerrainSurfaceKind.Stone));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId, m_tilemap),
                        CreateLayerSource(bridgeNode.LayerId, bridgeTilemap)
                    });
                m_navigationMap.RefreshNavigationData();

                Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                    bridgeNode,
                    out TerrainSurfaceSample sample));
                Assert.AreEqual(bridgeNode, sample.NodeKey);
                Assert.AreEqual(1, sample.Elevation);
                Assert.AreEqual(ETerrainSurfaceKind.Stone, sample.BaseSurface);

                Assert.IsTrue(TryGetOrCreateRuntimeNodeState(
                    bridgeNode,
                    out TerrainCellRuntimeState runtimeState));
                Assert.IsTrue(runtimeState.SetEffectiveSurface(ETerrainSurfaceKind.Dirt));
                Assert.IsTrue(CommitRuntimeNodeState(bridgeNode, sample, 2.0f));

                Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                    bridgeNode,
                    out TerrainSurfaceSample scorchedSample));
                Assert.AreEqual(ETerrainSurfaceKind.Dirt, scorchedSample.EffectiveSurface);
                Assert.AreEqual(2.0f, scorchedSample.EffectiveTraversalCost);
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void RuntimeState_Vector3IntCompatibility_UsesDefaultLayerState()
        {
            TerrainNavigationTile grass = CreateTile(0, ETerrainTransitionKind.Ground);
            Vector3Int cell = new(0, 0);
            TerrainNodeKey defaultNode = TerrainNodeKey.Default(cell);
            m_tilemap.SetTile(cell, grass);
            m_navigationMap.RefreshNavigationData();

            TerrainCellRuntimeState createdState = GetOrCreateRuntimeState(cell);
            Assert.IsTrue(createdState.SetEffectiveSurface(ETerrainSurfaceKind.Mud));

            Assert.IsTrue(m_navigationMap.TryGetRuntimeState(
                cell,
                out TerrainCellRuntimeState legacyState));
            Assert.IsTrue(m_navigationMap.TryGetRuntimeNodeState(
                defaultNode,
                out TerrainCellRuntimeState nodeState));
            Assert.AreSame(createdState, legacyState);
            Assert.AreSame(createdState, nodeState);

            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                cell,
                out TerrainSurfaceSample legacySample));
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                defaultNode,
                out TerrainSurfaceSample nodeSample));
            Assert.AreEqual(defaultNode, legacySample.NodeKey);
            Assert.AreEqual(defaultNode, nodeSample.NodeKey);
            Assert.AreEqual(ETerrainSurfaceKind.Mud, legacySample.EffectiveSurface);
            Assert.AreEqual(legacySample.EffectiveSurface, nodeSample.EffectiveSurface);
        }

        [Test]
        public void RuleTilemap_WhenLayerSourcesAreEmpty_UsesLegacyDefaultTilemap()
        {
            TerrainNavigationTile grass = CreateTile(0, ETerrainTransitionKind.Ground);
            Vector3Int cell = new(0, 0);
            m_tilemap.SetTile(cell, grass);
            m_navigationMap.RefreshNavigationData();

            Assert.AreSame(m_tilemap, m_navigationMap.RuleTilemap);
            Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                cell,
                out TerrainSurfaceSample sample));
            Assert.AreEqual(TerrainNodeKey.Default(cell), sample.NodeKey);
            Assert.AreEqual(ETerrainSurfaceKind.Grass, sample.BaseSurface);
        }

        [Test]
        public void RuleTilemap_WhenDefaultLayerSourceExists_UsesLayerSourceTilemap()
        {
            GameObject layerTilemapObject = new("默认层规则", typeof(Tilemap), typeof(TilemapRenderer));
            layerTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap layerTilemap = layerTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground, ETerrainRampDirection.None, ETerrainSurfaceKind.Dirt));
                layerTilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground, ETerrainRampDirection.None, ETerrainSurfaceKind.Stone));
                TerrainNavigationLayerSource source = new();
                SetPrivateField(source, "m_layerId", TerrainNodeKey.DefaultLayerId);
                SetPrivateField(source, "m_ruleTilemap", layerTilemap);
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[] { source });
                m_navigationMap.RefreshNavigationData();

                Assert.AreSame(layerTilemap, m_navigationMap.RuleTilemap);
                Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                    cell,
                    out TerrainSurfaceSample sample));
                Assert.AreEqual(ETerrainSurfaceKind.Stone, sample.BaseSurface);
            }
            finally
            {
                Object.DestroyImmediate(layerTilemapObject);
            }
        }

        [Test]
        public void RefreshNavigationData_DuplicateLayerId_InvalidatesNavigation()
        {
            TerrainNavigationLayerSource firstSource = CreateLayerSource(
                TerrainNodeKey.DefaultLayerId,
                m_tilemap);
            TerrainNavigationLayerSource secondSource = CreateLayerSource(
                TerrainNodeKey.DefaultLayerId,
                m_tilemap);
            SetPrivateField(
                m_navigationMap,
                "m_layerSources",
                new[] { firstSource, secondSource });

            LogAssert.Expect(
                LogType.Error,
                $"地形导航组件 '{m_navigationMap.name}' 存在重复地形层 ID：{TerrainNodeKey.DefaultLayerId}。请确保每个规则层来源使用唯一 LayerId。");

            m_navigationMap.RefreshNavigationData();

            Assert.IsNull(GetPrivateField<TerrainNavigationTile[,]>(
                m_navigationMap,
                "m_cachedTiles"));
            Assert.IsNull(GetPrivateField<float[,]>(
                m_navigationMap,
                "m_cachedCostMap"));
        }

        [Test]
        public void RefreshNavigationData_OverlappingCellsOnDifferentLayers_KeepDistinctNodes()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(cell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId, m_tilemap),
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId + 1, bridgeTilemap)
                    });

                m_navigationMap.RefreshNavigationData();

                Assert.IsTrue(m_navigationMap.TryGetSurfaceSample(
                    cell,
                    out TerrainSurfaceSample sample));
                Assert.AreEqual(TerrainNodeKey.Default(cell), sample.NodeKey);
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void NavigationGraph_LegacyDefaultLayer_BuildsSameLayerEdges()
        {
            Vector3Int startCell = new(0, 0);
            Vector3Int middleCell = new(1, 0);
            Vector3Int goalCell = new(2, 0);
            m_tilemap.SetTile(startCell, CreateTile(0, ETerrainTransitionKind.Ground));
            m_tilemap.SetTile(middleCell, CreateTile(0, ETerrainTransitionKind.Ground));
            m_tilemap.SetTile(goalCell, CreateTile(0, ETerrainTransitionKind.Ground));
            m_navigationMap.RefreshNavigationData();

            TerrainNodeKey startNode = TerrainNodeKey.Default(startCell);
            TerrainNodeKey middleNode = TerrainNodeKey.Default(middleCell);
            TerrainNodeKey goalNode = TerrainNodeKey.Default(goalCell);
            System.Collections.Generic.List<TerrainNodeKey> nodePath = new();

            Assert.AreEqual(3, m_navigationMap.NavigationGraphNodeCount);
            Assert.IsTrue(m_navigationMap.HasNavigationGraphNode(startNode));
            Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(startNode, middleNode));
            Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(middleNode, goalNode));
            Assert.IsTrue(m_navigationMap.TryBuildNodePath(startNode, goalNode, nodePath));
            CollectionAssert.AreEqual(
                new[] { startNode, middleNode, goalNode },
                nodePath);
        }

        [Test]
        public void NavigationGraph_OverlappingLayers_DoNotAutoConnect()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                TerrainNodeKey groundNode = TerrainNodeKey.Default(cell);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, cell);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(cell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId, m_tilemap),
                        CreateLayerSource(bridgeNode.LayerId, bridgeTilemap)
                    });
                m_navigationMap.RefreshNavigationData();

                System.Collections.Generic.List<TerrainNodeKey> nodePath = new();

                Assert.AreEqual(2, m_navigationMap.NavigationGraphNodeCount);
                Assert.IsTrue(m_navigationMap.HasNavigationGraphNode(groundNode));
                Assert.IsTrue(m_navigationMap.HasNavigationGraphNode(bridgeNode));
                Assert.IsFalse(m_navigationMap.HasNavigationGraphEdge(groundNode, bridgeNode));
                Assert.IsFalse(m_navigationMap.HasNavigationGraphEdge(bridgeNode, groundNode));
                Assert.IsFalse(m_navigationMap.TryBuildNodePath(groundNode, bridgeNode, nodePath));
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void NavigationGraph_ExplicitTransitionLink_ConnectsLayers()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                TerrainNodeKey groundNode = TerrainNodeKey.Default(cell);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, cell);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(cell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId, m_tilemap),
                        CreateLayerSource(bridgeNode.LayerId, bridgeTilemap)
                    });
                SetPrivateField(
                    m_navigationMap,
                    "m_transitionLinks",
                    new[]
                    {
                        CreateTransitionLink(
                            groundNode,
                            bridgeNode,
                            ETerrainTransitionLinkKind.Ramp,
                            bidirectional: true)
                    });
                m_navigationMap.RefreshNavigationData();

                System.Collections.Generic.List<TerrainNodeKey> nodePath = new();

                Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(groundNode, bridgeNode));
                Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(bridgeNode, groundNode));
                Assert.IsTrue(m_navigationMap.TryBuildNodePath(groundNode, bridgeNode, nodePath));
                CollectionAssert.AreEqual(new[] { groundNode, bridgeNode }, nodePath);
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void NavigationGraph_OneWayTransitionLink_DoesNotConnectReverse()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                TerrainNodeKey groundNode = TerrainNodeKey.Default(cell);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, cell);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(cell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId, m_tilemap),
                        CreateLayerSource(bridgeNode.LayerId, bridgeTilemap)
                    });
                SetPrivateField(
                    m_navigationMap,
                    "m_transitionLinks",
                    new[]
                    {
                        CreateTransitionLink(
                            groundNode,
                            bridgeNode,
                            ETerrainTransitionLinkKind.Drop,
                            bidirectional: false)
                    });
                m_navigationMap.RefreshNavigationData();

                System.Collections.Generic.List<TerrainNodeKey> nodePath = new();

                Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(groundNode, bridgeNode));
                Assert.IsFalse(m_navigationMap.HasNavigationGraphEdge(bridgeNode, groundNode));
                Assert.IsTrue(m_navigationMap.TryBuildNodePath(groundNode, bridgeNode, nodePath));
                Assert.IsFalse(m_navigationMap.TryBuildNodePath(bridgeNode, groundNode, nodePath));
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void NavigationGraph_TransitionMissingEndpoint_ReportsAndSkipsLink()
        {
            TerrainNodeKey groundNode = TerrainNodeKey.Default(new Vector3Int(0, 0));
            TerrainNodeKey missingBridgeNode = new(
                TerrainNodeKey.DefaultLayerId + 1,
                new Vector3Int(0, 0));
            m_tilemap.SetTile(groundNode.Cell, CreateTile(0, ETerrainTransitionKind.Ground));
            SetPrivateField(
                m_navigationMap,
                "m_transitionLinks",
                new[]
                {
                    CreateTransitionLink(
                        groundNode,
                        missingBridgeNode,
                        ETerrainTransitionLinkKind.Ramp,
                        bidirectional: true)
                });

            LogAssert.Expect(
                LogType.Error,
                $"地形导航组件 '{m_navigationMap.name}' 的跨层连接端点不存在：{groundNode} -> {missingBridgeNode}。");

            m_navigationMap.RefreshNavigationData();

            Assert.IsFalse(m_navigationMap.HasNavigationGraphEdge(groundNode, missingBridgeNode));
        }

        [Test]
        public void NavigationGraph_DuplicateTransition_ReportsAndKeepsSingleConnection()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int cell = new(0, 0);
                TerrainNodeKey groundNode = TerrainNodeKey.Default(cell);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, cell);
                m_tilemap.SetTile(cell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(cell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(TerrainNodeKey.DefaultLayerId, m_tilemap),
                        CreateLayerSource(bridgeNode.LayerId, bridgeTilemap)
                    });
                SetPrivateField(
                    m_navigationMap,
                    "m_transitionLinks",
                    new[]
                    {
                        CreateTransitionLink(
                            groundNode,
                            bridgeNode,
                            ETerrainTransitionLinkKind.Ramp,
                            bidirectional: true),
                        CreateTransitionLink(
                            groundNode,
                            bridgeNode,
                            ETerrainTransitionLinkKind.Ramp,
                            bidirectional: true)
                    });

                LogAssert.Expect(
                    LogType.Error,
                    $"地形导航组件 '{m_navigationMap.name}' 的跨层连接重复或被已有边占用：{groundNode} -> {bridgeNode}。");

                m_navigationMap.RefreshNavigationData();

                Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(groundNode, bridgeNode));
                Assert.IsTrue(m_navigationMap.HasNavigationGraphEdge(bridgeNode, groundNode));
                Assert.AreEqual(2, m_navigationMap.NavigationGraphEdgeCount);
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void NavigationGraph_NonDefaultSource_KeepsLegacyDefaultGraph()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int groundCell = new(0, 0);
                Vector3Int bridgeCell = new(1, 0);
                TerrainNodeKey groundNode = TerrainNodeKey.Default(groundCell);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, bridgeCell);
                m_tilemap.SetTile(groundCell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(bridgeCell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[] { CreateLayerSource(bridgeNode.LayerId, bridgeTilemap) });
                m_navigationMap.RefreshNavigationData();

                Assert.AreEqual(2, m_navigationMap.NavigationGraphNodeCount);
                Assert.IsTrue(m_navigationMap.HasNavigationGraphNode(groundNode));
                Assert.IsTrue(m_navigationMap.HasNavigationGraphNode(bridgeNode));
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void BuildWorldPath_ExplicitTransition_UsesGraphWaypointsAndResolvesTargetLayer()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int groundCell = new(0, 0);
                Vector3Int bridgeCell = new(1, 0);
                TerrainNodeKey groundNode = TerrainNodeKey.Default(groundCell);
                TerrainNodeKey bridgeNode = new(TerrainNodeKey.DefaultLayerId + 1, bridgeCell);
                m_tilemap.SetTile(groundCell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(bridgeCell, CreateTile(1, ETerrainTransitionKind.Ground));

                Vector2 groundCenter = m_tilemap.GetCellCenterWorld(groundCell);
                Vector2 bridgeCenter = bridgeTilemap.GetCellCenterWorld(bridgeCell);
                Vector2 transitionCenter = (groundCenter + bridgeCenter) * 0.5f;
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(groundNode.LayerId, m_tilemap),
                        CreateLayerSource(bridgeNode.LayerId, bridgeTilemap)
                    });
                SetPrivateField(
                    m_navigationMap,
                    "m_transitionLinks",
                    new[]
                    {
                        CreateTransitionLink(
                            groundNode,
                            bridgeNode,
                            ETerrainTransitionLinkKind.Ramp,
                            bidirectional: true,
                            new[] { groundCenter, transitionCenter, bridgeCenter })
                    });
                m_navigationMap.RefreshNavigationData();

                bool found = m_navigationMap.TryBuildWorldPath(
                    groundCenter,
                    groundNode.LayerId,
                    bridgeCenter,
                    out Vector2[] path,
                    out TerrainNodeKey destinationNode);

                Assert.IsTrue(found);
                Assert.AreEqual(bridgeNode, destinationNode);
                CollectionAssert.AreEqual(
                    new[] { transitionCenter, bridgeCenter },
                    path);
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void BuildWorldPath_OverlappingReachableTargets_PrefersCurrentLayer()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int startCell = new(-1, 0);
                Vector3Int goalCell = new(0, 0);
                TerrainNodeKey startNode = TerrainNodeKey.Default(startCell);
                TerrainNodeKey groundGoal = TerrainNodeKey.Default(goalCell);
                TerrainNodeKey bridgeGoal = new(TerrainNodeKey.DefaultLayerId + 1, goalCell);
                m_tilemap.SetTile(startCell, CreateTile(0, ETerrainTransitionKind.Ground));
                m_tilemap.SetTile(goalCell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(goalCell, CreateTile(1, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(startNode.LayerId, m_tilemap),
                        CreateLayerSource(bridgeGoal.LayerId, bridgeTilemap)
                    });
                SetPrivateField(
                    m_navigationMap,
                    "m_transitionLinks",
                    new[]
                    {
                        CreateTransitionLink(
                            startNode,
                            bridgeGoal,
                            ETerrainTransitionLinkKind.Stairs,
                            bidirectional: true)
                    });
                m_navigationMap.RefreshNavigationData();

                bool found = m_navigationMap.TryBuildWorldPath(
                    m_tilemap.GetCellCenterWorld(startCell),
                    startNode.LayerId,
                    m_tilemap.GetCellCenterWorld(goalCell),
                    out _,
                    out TerrainNodeKey destinationNode);

                Assert.IsTrue(found);
                Assert.AreEqual(groundGoal, destinationNode);
            }
            finally
            {
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        [Test]
        public void BuildWorldPath_MultipleReachableTargetLayersWithoutCurrentLayer_RejectsAmbiguity()
        {
            GameObject bridgeTilemapObject = new("桥面规则", typeof(Tilemap), typeof(TilemapRenderer));
            GameObject upperTilemapObject = new("高层规则", typeof(Tilemap), typeof(TilemapRenderer));
            bridgeTilemapObject.transform.SetParent(m_gridObject.transform);
            upperTilemapObject.transform.SetParent(m_gridObject.transform);
            Tilemap bridgeTilemap = bridgeTilemapObject.GetComponent<Tilemap>();
            Tilemap upperTilemap = upperTilemapObject.GetComponent<Tilemap>();
            try
            {
                Vector3Int startCell = new(-1, 0);
                Vector3Int goalCell = new(0, 0);
                TerrainNodeKey startNode = TerrainNodeKey.Default(startCell);
                TerrainNodeKey bridgeGoal = new(TerrainNodeKey.DefaultLayerId + 1, goalCell);
                TerrainNodeKey upperGoal = new(TerrainNodeKey.DefaultLayerId + 2, goalCell);
                m_tilemap.SetTile(startCell, CreateTile(0, ETerrainTransitionKind.Ground));
                bridgeTilemap.SetTile(goalCell, CreateTile(1, ETerrainTransitionKind.Ground));
                upperTilemap.SetTile(goalCell, CreateTile(2, ETerrainTransitionKind.Ground));
                SetPrivateField(
                    m_navigationMap,
                    "m_layerSources",
                    new[]
                    {
                        CreateLayerSource(startNode.LayerId, m_tilemap),
                        CreateLayerSource(bridgeGoal.LayerId, bridgeTilemap),
                        CreateLayerSource(upperGoal.LayerId, upperTilemap)
                    });
                SetPrivateField(
                    m_navigationMap,
                    "m_transitionLinks",
                    new[]
                    {
                        CreateTransitionLink(
                            startNode,
                            bridgeGoal,
                            ETerrainTransitionLinkKind.Ramp,
                            bidirectional: true),
                        CreateTransitionLink(
                            startNode,
                            upperGoal,
                            ETerrainTransitionLinkKind.Stairs,
                            bidirectional: true)
                    });
                m_navigationMap.RefreshNavigationData();

                bool found = m_navigationMap.TryBuildWorldPath(
                    m_tilemap.GetCellCenterWorld(startCell),
                    startNode.LayerId,
                    bridgeTilemap.GetCellCenterWorld(goalCell),
                    out _,
                    out _);

                Assert.IsFalse(found);
            }
            finally
            {
                Object.DestroyImmediate(upperTilemapObject);
                Object.DestroyImmediate(bridgeTilemapObject);
            }
        }

        private static void AssertAxisAligned(Vector2 start, Vector2[] path)
        {
            Vector2 previous = start;
            foreach (Vector2 point in path)
            {
                Vector2 delta = point - previous;
                Assert.IsTrue(
                    Mathf.Approximately(delta.x, 0f) || Mathf.Approximately(delta.y, 0f),
                    $"路径段必须为正交移动：{previous} -> {point}");
                previous = point;
            }
        }

        private static TerrainNavigationTile CreateTile(
            int elevation,
            ETerrainTransitionKind transitionKind,
            ETerrainRampDirection rampDirection = ETerrainRampDirection.None,
            ETerrainSurfaceKind surfaceKind = ETerrainSurfaceKind.Grass)
        {
            TerrainNavigationTile tile = ScriptableObject.CreateInstance<TerrainNavigationTile>();
            SetPrivateField(tile, "m_walkable", true);
            SetPrivateField(tile, "m_elevation", elevation);
            SetPrivateField(tile, "m_transitionKind", transitionKind);
            SetPrivateField(tile, "m_rampDirection", rampDirection);
            SetPrivateField(tile, "m_surfaceKind", surfaceKind);
            SetPrivateField(tile, "m_traversalCost", 1.0f);
            return tile;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"找不到字段：{target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static TerrainNavigationLayerSource CreateLayerSource(
            int layerId,
            Tilemap tilemap)
        {
            TerrainNavigationLayerSource source = new();
            SetPrivateField(source, "m_layerId", layerId);
            SetPrivateField(source, "m_ruleTilemap", tilemap);
            return source;
        }

        private static TerrainTransitionLink CreateTransitionLink(
            TerrainNodeKey fromNode,
            TerrainNodeKey toNode,
            ETerrainTransitionLinkKind kind,
            bool bidirectional,
            Vector2[] worldWaypoints = null)
        {
            return new TerrainTransitionLink(
                fromNode,
                toNode,
                kind,
                bidirectional,
                1.0f,
                worldWaypoints ?? new[] { Vector2.zero },
                Vector2.zero);
        }

        private TerrainCellRuntimeState GetOrCreateRuntimeState(Vector3Int cell)
        {
            MethodInfo method = typeof(TerrainNavigationMap).GetMethod(
                "TryGetOrCreateRuntimeState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            object[] arguments = { cell, null };
            bool result = (bool)method.Invoke(m_navigationMap, arguments);
            Assert.IsTrue(result);
            return (TerrainCellRuntimeState)arguments[1];
        }

        private bool TryGetOrCreateRuntimeNodeState(
            TerrainNodeKey nodeKey,
            out TerrainCellRuntimeState runtimeState)
        {
            MethodInfo method = typeof(TerrainNavigationMap).GetMethod(
                "TryGetOrCreateRuntimeNodeState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            object[] arguments = { nodeKey, null };
            bool result = (bool)method.Invoke(m_navigationMap, arguments);
            runtimeState = (TerrainCellRuntimeState)arguments[1];
            return result;
        }

        private void CommitRuntimeState(
            Vector3Int cell,
            TerrainSurfaceSample previousSample,
            float traversalCostMultiplier)
        {
            MethodInfo method = typeof(TerrainNavigationMap).GetMethod(
                "CommitRuntimeState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            object[] arguments =
            {
                cell,
                previousSample,
                traversalCostMultiplier,
                EElementPresentationSignal.None
            };
            bool result = (bool)method.Invoke(m_navigationMap, arguments);
            Assert.IsTrue(result);
        }

        private bool CommitRuntimeNodeState(
            TerrainNodeKey nodeKey,
            TerrainSurfaceSample previousSample,
            float traversalCostMultiplier)
        {
            MethodInfo method = typeof(TerrainNavigationMap).GetMethod(
                "CommitRuntimeNodeState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            object[] arguments =
            {
                nodeKey,
                previousSample,
                traversalCostMultiplier,
                EElementPresentationSignal.None
            };
            return (bool)method.Invoke(m_navigationMap, arguments);
        }
    }
}
