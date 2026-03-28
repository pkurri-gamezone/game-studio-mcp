using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    public static class ScriptTools
    {
        private static readonly StringBuilder _consoleLog = new StringBuilder();
        private const int MAX_LOG_LINES = 100;

        static ScriptTools()
        {
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string msg, string stack, LogType type)
        {
            _consoleLog.AppendLine($"[{type}] {msg}");
            var lines = _consoleLog.ToString().Split('\n');
            if (lines.Length > MAX_LOG_LINES)
                _consoleLog.Clear().Append(string.Join("\n", lines, lines.Length - MAX_LOG_LINES, MAX_LOG_LINES));
        }

        public static void Register()
        {
            MCPToolRegistry.Register("create_script",
                ToolDef("create_script", "Create a new C# script in the Unity project",
                    Param("path", "string", "Asset path e.g. Assets/Scripts/MyScript.cs"),
                    Param("content", "string", "Full C# source code"),
                    Param("namespace", "string", "Optional namespace")),
                CreateScript);

            MCPToolRegistry.Register("edit_script",
                ToolDef("edit_script", "Edit an existing C# script by replacing a code block",
                    Param("path", "string", "Asset path of the script"),
                    Param("old_code", "string", "Exact code to replace"),
                    Param("new_code", "string", "Replacement code")),
                EditScript);

            MCPToolRegistry.Register("read_script",
                ToolDef("read_script", "Read the contents of a C# script",
                    Param("path", "string", "Asset path of the script")),
                ReadScript);

            MCPToolRegistry.Register("validate_script",
                ToolDef("validate_script", "Check if a C# script has compile errors",
                    Param("path", "string", "Asset path of the script")),
                ValidateScript);

            MCPToolRegistry.Register("read_console",
                ToolDef("read_console", "Read recent Unity console output",
                    Param("filter", "string", "Optional: Error | Warning | Log")),
                ReadConsole);
        }

        private static string CreateScript(string args)
        {
            string assetPath = ParseArg(args, "path");
            string content   = ParseArgMultiline(args, "content");

            if (string.IsNullOrEmpty(assetPath)) return Error("path is required");
            if (string.IsNullOrEmpty(content))   return Error("content is required");

            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            return $"{{\"created\":\"{assetPath}\",\"message\":\"Script created and imported\"}}";
        }

        private static string EditScript(string args)
        {
            string assetPath = ParseArg(args, "path");
            string oldCode   = ParseArgMultiline(args, "old_code");
            string newCode   = ParseArgMultiline(args, "new_code");

            if (string.IsNullOrEmpty(assetPath)) return Error("path is required");

            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!File.Exists(fullPath)) return Error($"File not found: {assetPath}");

            string src = File.ReadAllText(fullPath);
            if (!string.IsNullOrEmpty(oldCode) && src.Contains(oldCode))
            {
                src = src.Replace(oldCode, newCode ?? "");
                File.WriteAllText(fullPath, src);
                AssetDatabase.Refresh();
                return $"{{\"edited\":\"{assetPath}\",\"message\":\"Script updated successfully\"}}";
            }

            return Error($"Old code block not found in {assetPath}");
        }

        private static string ReadScript(string args)
        {
            string assetPath = ParseArg(args, "path");
            if (string.IsNullOrEmpty(assetPath)) return Error("path is required");

            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!File.Exists(fullPath)) return Error($"File not found: {assetPath}");

            string src = File.ReadAllText(fullPath);
            return $"{{\"path\":\"{assetPath}\",\"content\":{EscapeJson(src)},\"lines\":{src.Split('\n').Length}}}";
        }

        private static string ValidateScript(string args)
        {
            var errors = new StringBuilder();
            foreach (var msg in UnityEditor.Compilation.CompilationPipeline.GetAssemblies())
            {
                errors.Append(msg.name);
            }
            bool hasErrors = EditorUtility.scriptCompilationFailed;
            return $"{{\"hasErrors\":{hasErrors.ToString().ToLower()},\"message\":\"{(hasErrors ? "Script compilation failed — check console" : "No compile errors detected")}\"}}";
        }

        private static string ReadConsole(string args)
        {
            string filter = ParseArg(args, "filter") ?? "all";
            string log = _consoleLog.ToString();
            if (filter.ToLower() != "all")
            {
                var lines = log.Split('\n');
                var sb = new StringBuilder();
                foreach (var line in lines)
                    if (line.StartsWith($"[{filter}", StringComparison.OrdinalIgnoreCase)) sb.AppendLine(line);
                log = sb.ToString();
            }
            return $"{{\"log\":{EscapeJson(log)},\"filter\":\"{filter}\"}}";
        }

        public static string GetConsoleLogs() => _consoleLog.ToString();

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ParseArgMultiline(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx);
            int start = json.IndexOf('"', colon + 1);
            if (start < 0) return null;
            var sb = new StringBuilder();
            for (int i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '"' && json[i - 1] != '\\') break;
                sb.Append(json[i] == '\\' && i + 1 < json.Length && json[i + 1] == 'n' ? "\n" : json[i].ToString());
                if (json[i] == '\\') i++;
            }
            return sb.ToString();
        }

        private static string EscapeJson(string s)
            => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";

        private static string Error(string msg) => $"{{\"error\":\"{msg}\"}}";

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
