using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    public static class LevelTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("list_levels",
                ToolDef("list_levels", "List all available level JSON files in the project",
                    Param("path", "string", "Optional: directory to search (default: Assets/Resources/Data/Levels)")),
                ListLevels);

            MCPToolRegistry.Register("load_level",
                ToolDef("load_level", "Load a level by index or name into the scene",
                    Param("index", "string", "Level number to load"),
                    Param("name", "string", "Optional: level name override")),
                LoadLevel);

            MCPToolRegistry.Register("generate_level",
                ToolDef("generate_level", "Generate a new level JSON and add it to the levels file",
                    Param("count", "string", "Number of levels to generate"),
                    Param("difficulty", "string", "easy | medium | hard"),
                    Param("output", "string", "Optional: output JSON path")),
                GenerateLevel);

            MCPToolRegistry.Register("get_level_data",
                ToolDef("get_level_data", "Read level data JSON and return its contents",
                    Param("path", "string", "Path to levels JSON file")),
                GetLevelData);
        }

        private static string ListLevels(string args)
        {
            string searchPath = ParseArg(args, "path") ?? "Assets/Resources/Data/Levels";
            string fullPath   = Path.Combine(Application.dataPath, "..", searchPath);

            if (!Directory.Exists(fullPath))
                return $"{{\"levels\":[],\"message\":\"Directory not found: {searchPath}\"}}";

            var files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);
            var sb = new StringBuilder("[");
            for (int i = 0; i < files.Length; i++)
            {
                string rel = files[i].Replace(Application.dataPath, "Assets").Replace("\\", "/");
                sb.Append($"\"{rel}\"");
                if (i < files.Length - 1) sb.Append(",");
            }
            sb.Append("]");
            return $"{{\"levels\":{sb},\"count\":{files.Length}}}";
        }

        private static string LoadLevel(string args)
        {
            string index = ParseArg(args, "index") ?? "1";
            // Trigger via messaging — actual load happens in GameManager/LevelLoader
            EditorApplication.delayCall += () =>
            {
                var gm = Object.FindAnyObjectByType<MonoBehaviour>();
                Debug.Log($"[GameStudioMCP] Requesting level load: {index}");
            };
            return $"{{\"action\":\"load_level\",\"index\":{index},\"message\":\"Level {index} load requested — ensure GameManager is in scene\"}}";
        }

        private static string GenerateLevel(string args)
        {
            string count      = ParseArg(args, "count")      ?? "5";
            string difficulty = ParseArg(args, "difficulty") ?? "medium";
            string output     = ParseArg(args, "output")     ?? "Assets/Resources/Data/Levels/levels_generated.json";

            // Generate simple procedural levels
            var sb = new StringBuilder("{\"levels\":[");
            int n = int.TryParse(count, out var c) ? c : 5;
            for (int i = 0; i < n; i++)
            {
                int screws = difficulty == "easy" ? 4 + i : difficulty == "hard" ? 12 + i * 2 : 6 + i;
                int holes  = screws + 2;
                sb.Append($"{{\"id\":{i + 1},\"difficulty\":\"{difficulty}\",\"screwCount\":{screws},\"holeCount\":{holes},\"timeLimit\":120}}");
                if (i < n - 1) sb.Append(",");
            }
            sb.Append("]}");

            string fullPath = Path.Combine(Application.dataPath, "..", output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            return $"{{\"generated\":{n},\"difficulty\":\"{difficulty}\",\"output\":\"{output}\"}}";
        }

        private static string GetLevelData(string args)
        {
            string assetPath = ParseArg(args, "path") ?? "Assets/Resources/Data/Levels/levels.json";
            string fullPath  = Path.Combine(Application.dataPath, "..", assetPath);

            if (!File.Exists(fullPath))
                return $"{{\"error\":\"File not found: {assetPath}\"}}";

            string data = File.ReadAllText(fullPath);
            return $"{{\"path\":\"{assetPath}\",\"data\":{data}}}";
        }

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
