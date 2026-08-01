#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSteering2D;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public static class ContextSteering2DPerformanceBenchmark
    {
        private const string ProfilePath =
            "Assets/ProjectPlugins/ContextSteering2D/Runtime/Defaults/DefaultContextSteeringProfile2D.asset";
        private const string ResultPath =
            "Temp/UnityBridge/results/context-steering-performance-benchmark.json";

        public static string Run()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Context Steering 性能基准必须从 EditMode 启动。");
            }

            ContextSteeringProfile2D profile =
                AssetDatabase.LoadAssetAtPath<ContextSteeringProfile2D>(ProfilePath);
            if (profile == null)
            {
                throw new InvalidOperationException($"找不到 Context Steering Profile：{ProfilePath}");
            }

            ContextSteeringSimulation2D? simulation = ContextSteeringSimulation2D.Current ??
                UnityEngine.Object.FindFirstObjectByType<ContextSteeringSimulation2D>();
            GameObject? temporarySimulationObject = null;
            if (simulation == null)
            {
                temporarySimulationObject = new GameObject("Context Steering Benchmark Simulation");
                simulation = temporarySimulationObject.AddComponent<ContextSteeringSimulation2D>();
            }

            if (simulation.AgentCount != 0)
            {
                throw new InvalidOperationException(
                    $"性能基准启动前 Simulation 仍登记了 {simulation.AgentCount} 个 Agent，拒绝混入旧世界状态。");
            }

            try
            {
                BenchmarkReport report = new()
                {
                    UnityVersion = Application.unityVersion,
                    Cpu = SystemInfo.processorType,
                    TimestampUtc = DateTime.UtcNow.ToString("O"),
                    Samples = new[]
                    {
                        RunScale(simulation, profile, 100, 5, 20),
                        RunScale(simulation, profile, 500, 5, 15),
                        RunScale(simulation, profile, 1000, 5, 10),
                    },
                };

                string fullPath = Path.GetFullPath(ResultPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, JsonUtility.ToJson(report, true));
                return fullPath;
            }
            finally
            {
                if (temporarySimulationObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(temporarySimulationObject);
                }
            }
        }

        private static BenchmarkSample RunScale(
            ContextSteeringSimulation2D simulation,
            ContextSteeringProfile2D profile,
            int agentCount,
            int warmupSteps,
            int sampleSteps)
        {
            List<GameObject> agentObjects = new(agentCount);
            List<ContextSteeringAgentHandle2D> handles = new(agentCount);
            try
            {
                int characterLayer = LayerMask.NameToLayer("Character");
                if (characterLayer < 0)
                {
                    throw new InvalidOperationException("项目缺少 Character Layer。");
                }

                ContactFilter2D emptyFilter = new();
                emptyFilter.SetLayerMask(0);
                emptyFilter.useTriggers = true;
                ContactFilter2D neighbourFilter = new();
                neighbourFilter.SetLayerMask(1 << characterLayer);
                neighbourFilter.useTriggers = true;

                int width = Mathf.CeilToInt(Mathf.Sqrt(agentCount));
                for (int i = 0; i < agentCount; i++)
                {
                    GameObject agentObject = new($"Benchmark Agent {i}");
                    agentObjects.Add(agentObject);
                    agentObject.layer = characterLayer;
                    agentObject.transform.position = new Vector2(
                        (i % width) * 0.55f,
                        (i / width) * 0.55f);
                    Rigidbody2D body = agentObject.AddComponent<Rigidbody2D>();
                    body.bodyType = RigidbodyType2D.Kinematic;
                    CircleCollider2D collider = agentObject.AddComponent<CircleCollider2D>();
                    collider.radius = profile.AgentRadius;
                    collider.isTrigger = true;
                    ContextSteeringAgentHandle2D handle = simulation.Register(
                        body,
                        profile,
                        emptyFilter,
                        neighbourFilter,
                        emptyFilter);
                    Vector2 direction = (i & 1) == 0 ? Vector2.right : Vector2.left;
                    handle.SubmitIntent(
                        true,
                        (Vector2)agentObject.transform.position + direction * 10.0f,
                        Vector2.zero,
                        direction,
                        captureDebug: false,
                        maxSpeed: profile.MaxSpeed);
                    handles.Add(handle);
                }

                Physics2D.SyncTransforms();
                for (int i = 0; i < warmupSteps; i++)
                {
                    simulation.Simulate(0.02f);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                ContextSteeringSimulationMetrics2D[] metrics =
                    new ContextSteeringSimulationMetrics2D[sampleSteps];
                long allocatedBytes = 0;
                for (int i = 0; i < sampleSteps; i++)
                {
                    long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                    simulation.Simulate(0.02f);
                    allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                    metrics[i] = simulation.LastMetrics;
                }

                double[] totals = metrics.Select(value => value.TotalMilliseconds).OrderBy(value => value).ToArray();
                return new BenchmarkSample
                {
                    AgentCount = agentCount,
                    WarmupSteps = warmupSteps,
                    SampleSteps = sampleSteps,
                    DetectionAverageMilliseconds = metrics.Average(value => value.DetectionMilliseconds),
                    SteeringAverageMilliseconds = metrics.Average(value => value.SteeringMilliseconds),
                    Rvo2AverageMilliseconds = metrics.Average(value => value.LocalAvoidanceMilliseconds),
                    PbdAverageMilliseconds = metrics.Average(value => value.ContactMilliseconds),
                    TotalAverageMilliseconds = metrics.Average(value => value.TotalMilliseconds),
                    TotalMedianMilliseconds = Percentile(totals, 0.5),
                    TotalP95Milliseconds = Percentile(totals, 0.95),
                    ManagedAllocatedBytesTotal = allocatedBytes,
                    ManagedAllocatedBytesPerStep = allocatedBytes / (double)sampleSteps,
                };
            }
            finally
            {
                for (int i = 0; i < handles.Count; i++)
                {
                    handles[i].Dispose();
                }
                for (int i = 0; i < agentObjects.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(agentObjects[i]);
                }
            }
        }

        private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0.0;
            }

            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(sortedValues.Count * percentile)) - 1,
                0,
                sortedValues.Count - 1);
            return sortedValues[index];
        }

        [Serializable]
        private sealed class BenchmarkReport
        {
            public string TimestampUtc = string.Empty;
            public string UnityVersion = string.Empty;
            public string Cpu = string.Empty;
            public BenchmarkSample[] Samples = Array.Empty<BenchmarkSample>();
        }

        [Serializable]
        private sealed class BenchmarkSample
        {
            public int AgentCount;
            public int WarmupSteps;
            public int SampleSteps;
            public double DetectionAverageMilliseconds;
            public double SteeringAverageMilliseconds;
            public double Rvo2AverageMilliseconds;
            public double PbdAverageMilliseconds;
            public double TotalAverageMilliseconds;
            public double TotalMedianMilliseconds;
            public double TotalP95Milliseconds;
            public long ManagedAllocatedBytesTotal;
            public double ManagedAllocatedBytesPerStep;
        }
    }
}
