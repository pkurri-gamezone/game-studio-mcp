using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Note: Test Runner API requires com.unity.test-framework package
#if UNITY_TEST_TOOLS
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace GameStudioMCP
{
    public static class TestTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("run_tests",
                ToolDef("run_tests", "Run Unity Test Runner tests and return results",
                    Param("mode", "string", "EditMode | PlayMode"),
                    Param("filter", "string", "Optional: test name filter")),
                RunTests);

            MCPToolRegistry.Register("get_test_files",
                ToolDef("get_test_files", "List all Unity test files in the project"),
                GetTestFiles);

            MCPToolRegistry.Register("create_test",
                ToolDef("create_test", "Create a new Unity Test Runner test file",
                    Param("name", "string", "Test class name e.g. MySystemTests"),
                    Param("mode", "string", "EditMode | PlayMode"),
                    Param("output_path", "string", "Optional: output path")),
                CreateTest);
        }

        private static string RunTests(string args)
        {
            string mode   = ParseArg(args, "mode")   ?? "EditMode";
            string filter = ParseArg(args, "filter");

#if UNITY_TEST_TOOLS
            EditorApplication.delayCall += () =>
            {
                var filter_obj = new Filter
                {
                    testMode = mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode
                };
                if (!string.IsNullOrEmpty(filter)) filter_obj.testNames = new[] { filter };

                var api = TestRunnerApi.CreateInstance<TestRunnerApi>();
                api.Execute(new ExecutionSettings(filter_obj));
                Debug.Log($"[GameStudioMCP] Test run triggered: {mode} {filter ?? "all"}");
            };
            return $"{{\"action\":\"run_tests\",\"mode\":\"{mode}\",\"filter\":\"{filter ?? "all"}\",\"message\":\"Tests started — check Test Runner window\"}}";
#else
            Debug.LogWarning("[GameStudioMCP] Test Runner API not available — install com.unity.test-framework package");
            return $"{{\"action\":\"run_tests\",\"mode\":\"{mode}\",\"error\":\"Test framework package not installed\"}}";
#endif
        }

        private static string GetTestFiles(string args)
        {
            string dataPath = Application.dataPath;
            string[] testFiles = Directory.GetFiles(dataPath, "*Tests.cs", SearchOption.AllDirectories);
            var sb = new StringBuilder("[");
            for (int i = 0; i < testFiles.Length; i++)
            {
                string rel = testFiles[i].Replace(Application.dataPath, "Assets").Replace("\\", "/");
                sb.Append($"\"{rel}\"");
                if (i < testFiles.Length - 1) sb.Append(",");
            }
            sb.Append("]");
            return $"{{\"test_files\":{sb},\"count\":{testFiles.Length}}}";
        }

        private static string CreateTest(string args)
        {
            string name       = ParseArg(args, "name")        ?? "NewTests";
            string mode       = ParseArg(args, "mode")        ?? "EditMode";
            string outputPath = ParseArg(args, "output_path") ?? $"Assets/Tests/{mode}/{name}.cs";

            string attr = mode == "PlayMode" ? "[UnityTest]" : "[Test]";
            string usings = mode == "PlayMode"
                ? "using System.Collections;\nusing NUnit.Framework;\nusing UnityEngine.TestTools;\n"
                : "using NUnit.Framework;\n";

            string content = $@"{usings}
public class {name}
{{
    {attr}
    public {(mode == "PlayMode" ? "IEnumerator" : "void")} Test_{name}_DefaultPass()
    {{
        // Arrange

        // Act

        // Assert
        {(mode == "PlayMode" ? "yield return null;\n        " : "")}Assert.IsTrue(true, ""Default pass — replace with real assertion"");
    }}
}}
";
            string fullPath = Path.Combine(Application.dataPath, "..", outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            return $"{{\"created\":\"{outputPath}\",\"mode\":\"{mode}\",\"message\":\"Test file created — open Window > General > Test Runner to run\"}}";
        }

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string ToolDef(string name, string desc)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
