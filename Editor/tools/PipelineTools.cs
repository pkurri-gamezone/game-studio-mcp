using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    public static class PipelineTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("get_sprint_status",
                ToolDef("get_sprint_status", "Read SPRINT_PLAN.md and return current day, completed tasks, and pending tasks"),
                GetSprintStatusTool);

            MCPToolRegistry.Register("get_project_info",
                ToolDef("get_project_info", "Return Unity project metadata: name, version, target platforms, package count"),
                GetProjectInfoTool);

            MCPToolRegistry.Register("run_audit",
                ToolDef("run_audit", "Run game audit via game-automation-bundle CLI and return results",
                    Param("compliance", "string", "true to include compliance check"),
                    Param("project_path", "string", "Optional: path to project")),
                RunAudit);

            MCPToolRegistry.Register("get_game_metrics",
                ToolDef("get_game_metrics", "Return Unity Editor performance metrics: memory, asset count, scene stats"),
                GetGameMetrics);
        }

        private static string GetSprintStatusTool(string args) => GetSprintStatus();
        private static string GetProjectInfoTool(string args) => GetProjectInfo();

        public static string GetSprintStatus()
        {
            string[] searchPaths = {
                Path.Combine(Application.dataPath, "..", "SPRINT_PLAN.md"),
                Path.Combine(Application.dataPath, "..", "..", "SPRINT_PLAN.md"),
                Path.Combine(Application.dataPath, "..", "..", "games", "screw-vault", "SPRINT_PLAN.md")
            };

            foreach (var p in searchPaths)
            {
                if (!File.Exists(p)) continue;
                string content = File.ReadAllText(p);
                int total    = Regex.Matches(content, @"\[x\]").Count;
                int pending  = Regex.Matches(content, @"\[ \]").Count;
                int autoDone = Regex.Matches(content, @"\[AUTO\].*\(Completed\)").Count;

                // Find current day
                var dayMatch = Regex.Match(content, @"## (Day \d+[^#\n]*)");
                string currentDay = dayMatch.Success ? dayMatch.Value.Replace("## ", "") : "Unknown";

                return $"{{\"current_day\":\"{currentDay}\",\"completed_tasks\":{total},\"pending_tasks\":{pending},\"auto_completed\":{autoDone},\"sprint_file_found\":true}}";
            }
            return "{\"error\":\"SPRINT_PLAN.md not found\",\"sprint_file_found\":false}";
        }

        public static string GetProjectInfo()
        {
            string projectName = Application.productName;
            string version     = Application.version;
            string dataPath    = Application.dataPath;
            int    sceneCount  = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;

            // Count scripts
            string[] scripts = Directory.GetFiles(dataPath, "*.cs", SearchOption.AllDirectories);
            // Count assets
            string[] allAssets = Directory.GetFiles(dataPath, "*.*", SearchOption.AllDirectories);

            return $"{{\"name\":\"{projectName}\",\"version\":\"{version}\",\"unity_version\":\"{Application.unityVersion}\",\"scene_count\":{sceneCount},\"script_count\":{scripts.Length},\"asset_count\":{allAssets.Length},\"platform\":\"{Application.platform}\"}}";
        }

        private static string RunAudit(string args)
        {
            string compliance   = ParseArg(args, "compliance") ?? "false";
            string projectPath  = ParseArg(args, "project_path") ?? ".";

            // Try to find audit report already generated
            string[] reportFiles = Directory.GetFiles(
                Path.Combine(Application.dataPath, ".."),
                "AUDIT_REPORT_*.md", SearchOption.TopDirectoryOnly);

            if (reportFiles.Length > 0)
            {
                string latest = reportFiles[reportFiles.Length - 1];
                string content = File.ReadAllText(latest);

                bool hasCritical = content.Contains("Critical: 0") == false && Regex.IsMatch(content, @"Critical:\s*[1-9]");
                return $"{{\"audit_found\":true,\"report_path\":\"{Path.GetFileName(latest)}\",\"has_critical\":{hasCritical.ToString().ToLower()},\"summary\":{EscapeJson(content.Substring(0, System.Math.Min(content.Length, 500)))}}}";
            }

            return "{\"audit_found\":false,\"message\":\"Run 'npx game audit . --compliance' from the project root to generate an audit report\"}";
        }

        private static string GetGameMetrics(string args)
        {
            long totalMemory = System.GC.GetTotalMemory(false);
            string scene     = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            int    goCount   = Object.FindObjectsOfType<GameObject>().Length;

            return $"{{\"active_scene\":\"{scene}\",\"gameobject_count\":{goCount},\"managed_memory_mb\":{totalMemory / 1024 / 1024},\"unity_version\":\"{Application.unityVersion}\"}}";
        }

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string EscapeJson(string s)
            => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string ToolDef(string name, string desc)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
