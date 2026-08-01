
#nullable enable
using System;
using System.Collections.Generic;
using UnityAiBridge;
using UnityAiBridge.Editor.Tools.TestRunner;
using UnityAiBridge.Utils;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UnityAiBridge.Editor.Tools
{
    [BridgeToolType]
    [InitializeOnLoad]
    public static partial class Tool_Tests
    {
        const int ActiveTestRunWaitTimeoutMs = 15000;
        const int ActiveTestRunPollIntervalMs = 200;

        static readonly object _lock = new();
        static volatile TestRunnerApi? _testRunnerApi = null!;
        static volatile TestResultCollector? _resultCollector = null!;
        static volatile bool _callbacksRegistered = false;

        static Tool_Tests()
        {
            _testRunnerApi ??= CreateInstance();
        }

        public static TestRunnerApi TestRunnerApi
        {
            get
            {
                lock (_lock)
                {
                    if (_testRunnerApi == null)
                        _testRunnerApi = CreateInstance();
                    return _testRunnerApi;
                }
            }
        }
        public static TestRunnerApi CreateInstance()
        {
            // Keep callback registration stable across domain reloads.
            if (BridgeCompat.IsLogEnabled(LogLevel.Trace))
                Debug.Log($"[{nameof(TestRunnerApi)}] Creating new instance. Existing API: {_testRunnerApi != null}, Existing Collector: {_resultCollector != null}, Callbacks Registered: {_callbacksRegistered}");

            _resultCollector ??= new TestResultCollector();
            var testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();

            // Unity can recreate TestRunnerApi across domain reloads / repeated calls.
            // Re-register the live collector onto the fresh API instance so RunStarted/RunFinished
            // callbacks still arrive, but try to unhook the previous instance first to avoid duplicates.
            if (_testRunnerApi != null && _callbacksRegistered)
            {
                try
                {
                    _testRunnerApi.UnregisterCallbacks(_resultCollector);
                }
                catch (Exception e)
                {
                    if (BridgeCompat.IsLogEnabled(LogLevel.Trace))
                        Debug.LogWarning($"[{nameof(TestRunnerApi)}] Failed to unregister callbacks from previous instance: {e.Message}");
                }
            }

            testRunnerApi.RegisterCallbacks(_resultCollector);
            _callbacksRegistered = true;

            if (BridgeCompat.IsLogEnabled(LogLevel.Trace))
                Debug.Log($"[{nameof(TestRunnerApi)}] Registered callbacks on the current TestRunnerApi instance.");

            return testRunnerApi;
        }

        public static void Init()
        {
            // none
        }

        private static class Error
        {
            public static string InvalidTestMode(string testMode)
                => $"[Error] Invalid test mode '{testMode}'. Valid modes: EditMode, PlayMode, All";

            public static string TestExecutionFailed(string reason)
                => $"[Error] Test execution failed: {reason}";

            public static string TestTimeout(int timeoutMs)
                => $"[Error] Test execution timed out after {timeoutMs} ms";

            public static string PreviousRunStillActive(int timeoutMs)
                => $"[Error] Previous Unity Test Runner job is still active after waiting {timeoutMs} ms. Wait for the current run to fully finish before starting the next tests-run request.";

            public static string NoTestsFound(TestFilterParameters filterParams)
            {
                var filters = new List<string>();

                if (!string.IsNullOrEmpty(filterParams.TestAssembly)) filters.Add($"assembly '{filterParams.TestAssembly}'");
                if (!string.IsNullOrEmpty(filterParams.TestNamespace)) filters.Add($"namespace '{filterParams.TestNamespace}'");
                if (!string.IsNullOrEmpty(filterParams.TestClass)) filters.Add($"class '{filterParams.TestClass}'");
                if (!string.IsNullOrEmpty(filterParams.TestMethod)) filters.Add($"method '{filterParams.TestMethod}'");

                var filterText = filters.Count > 0
                    ? $" matching {string.Join(", ", filters)}"
                    : string.Empty;

                return $"[Error] No tests found{filterText}. Please check that the specified assembly, namespace, class, and method names are correct and that your Unity project contains tests.";
            }
        }
    }
}
