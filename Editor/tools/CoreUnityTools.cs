using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// Core Unity tools matching / extending unity-mcp parity:
    /// batch_execute, execute_menu_item, refresh_unity, manage_editor,
    /// manage_components, find_in_file, manage_asset, unity_docs, delete_script
    /// </summary>
    public static class CoreUnityTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("batch_execute",
                ToolDef("batch_execute",
                    "Execute multiple tool calls in one request — 10-100x faster than individual calls. Pass an array of {tool,arguments} objects.",
                    Param("calls", "string", "JSON array: [{\"tool\":\"create_gameobject\",\"arguments\":{\"name\":\"Player\"}},...]")),
                BatchExecute);

            MCPToolRegistry.Register("execute_menu_item",
                ToolDef("execute_menu_item",
                    "Execute any Unity Editor menu item by path (e.g. 'Assets/Reimport All', 'Edit/Play', 'Window/Test Runner')",
                    Param("menu_path", "string", "Full menu path e.g. 'Assets/Refresh' or 'Edit/Select All'")),
                ExecuteMenuItem);

            MCPToolRegistry.Register("refresh_unity",
                ToolDef("refresh_unity",
                    "Trigger AssetDatabase.Refresh() — reimport assets and pick up file changes"),
                RefreshUnity);

            MCPToolRegistry.Register("manage_editor",
                ToolDef("manage_editor",
                    "Control the Unity Editor play/pause/stop state and query current state",
                    Param("action", "string", "play | pause | stop | get_state | get_info")),
                ManageEditor);

            MCPToolRegistry.Register("manage_components",
                ToolDef("manage_components",
                    "Add, remove, get, or set component properties on a GameObject",
                    Param("action",     "string", "add | remove | get | list"),
                    Param("gameobject", "string", "Name of the target GameObject"),
                    Param("component",  "string", "Component type name e.g. Rigidbody, BoxCollider, AudioSource"),
                    Param("property",   "string", "Optional: component property name to get/set"),
                    Param("value",      "string", "Optional: value to set on the property")),
                ManageComponents);

            MCPToolRegistry.Register("find_in_file",
                ToolDef("find_in_file",
                    "Search for text across project files and return matching lines with file paths",
                    Param("query",      "string", "Text or regex pattern to search for"),
                    Param("path",       "string", "Optional: directory or file to search (default: Assets/)"),
                    Param("extension",  "string", "Optional: file extension filter e.g. .cs .json .asmdef")),
                FindInFile);

            MCPToolRegistry.Register("manage_asset",
                ToolDef("manage_asset",
                    "Asset database operations: copy, delete, move, rename, create folder, list assets",
                    Param("action", "string", "copy | delete | move | rename | create_folder | list | get_info"),
                    Param("path",   "string", "Source asset path e.g. Assets/Scripts/MyScript.cs"),
                    Param("dest",   "string", "Optional: destination path for copy/move/rename")),
                ManageAsset);

            MCPToolRegistry.Register("delete_script",
                ToolDef("delete_script",
                    "Delete a C# script from the project",
                    Param("path", "string", "Asset path e.g. Assets/Scripts/OldScript.cs")),
                DeleteScript);

            MCPToolRegistry.Register("unity_docs",
                ToolDef("unity_docs",
                    "Look up Unity scripting API documentation for a class or method",
                    Param("query",   "string", "Class or method name e.g. Rigidbody, AddForce, PlayerPrefs"),
                    Param("version", "string", "Optional: Unity version e.g. 2022.3 (defaults to current)")),
                UnityDocs);

            MCPToolRegistry.Register("get_editor_state",
                ToolDef("get_editor_state",
                    "Return full Unity Editor state: play mode, compilation status, active scene, selection"),
                GetEditorState);

            MCPToolRegistry.Register("apply_text_edits",
                ToolDef("apply_text_edits",
                    "Apply multiple find-replace edits to a file in one atomic operation",
                    Param("path",  "string", "Asset path of the file to edit"),
                    Param("edits", "string", "JSON array: [{\"old\":\"...\",\"new\":\"...\"},...] ")),
                ApplyTextEdits);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string BatchExecute(string args)
        {
            // Extract the calls array value
            int arrStart = args.IndexOf('[');
            int arrEnd   = args.LastIndexOf(']');
            if (arrStart < 0 || arrEnd < 0)
                return Error("calls must be a JSON array [{\"tool\":\"...\",\"arguments\":{...}},...]");

            string callsJson = args.Substring(arrStart, arrEnd - arrStart + 1);

            // Parse each call: {"tool":"...","arguments":{...}}
            var results = new StringBuilder("[");
            var callMatches = Regex.Matches(callsJson, @"\{[^{}]*""tool""\s*:\s*""([^""]+)""[^{}]*\}|(\{[^{}]*\{[^{}]*\}[^{}]*\})");

            // Better approach: split on top-level objects
            var calls = SplitTopLevelObjects(callsJson);
            bool first = true;
            int  count = 0;

            foreach (var call in calls)
            {
                string toolName = ParseString(call, "tool") ?? ParseString(call, "name");
                if (string.IsNullOrEmpty(toolName)) continue;

                // Extract arguments sub-object
                string argsSubJson = ExtractObject(call, "arguments") ?? "{}";

                string result;
                try   { result = MCPToolRegistry.Call(toolName, argsSubJson); }
                catch (Exception e) { result = $"{{\"error\":\"{e.Message}\"}}"; }

                if (!first) results.Append(",");
                results.Append($"{{\"tool\":\"{toolName}\",\"result\":{result}}}");
                first = false;
                count++;
            }

            results.Append("]");
            return $"{{\"batch_results\":{results},\"executed\":{count}}}";
        }

        private static string ExecuteMenuItem(string args)
        {
            string menuPath = ParseArg(args, "menu_path");
            if (string.IsNullOrEmpty(menuPath)) return Error("menu_path is required");

            bool success = false;
            EditorApplication.delayCall += () =>
            {
                success = EditorApplication.ExecuteMenuItem(menuPath);
                if (!success) Debug.LogWarning($"[GameStudioMCP] Menu item not found: {menuPath}");
            };

            return $"{{\"menu_path\":\"{menuPath}\",\"message\":\"Menu item execution queued\"}}";
        }

        private static string RefreshUnity(string args)
        {
            EditorApplication.delayCall += () => AssetDatabase.Refresh();
            return "{\"action\":\"refresh\",\"message\":\"AssetDatabase.Refresh() queued\"}";
        }

        private static string ManageEditor(string args)
        {
            string action = ParseArg(args, "action") ?? "get_state";
            switch (action.ToLower())
            {
                case "play":
                    EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
                    return "{\"action\":\"play\",\"message\":\"Entering play mode\"}";
                case "pause":
                    EditorApplication.delayCall += () => { EditorApplication.isPaused = !EditorApplication.isPaused; };
                    return "{\"action\":\"pause\",\"message\":\"Toggle pause requested\"}";
                case "stop":
                    EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
                    return "{\"action\":\"stop\",\"message\":\"Exiting play mode\"}";
                case "get_state":
                case "get_info":
                    return GetEditorState(args);
                default:
                    return Error($"Unknown action: {action}. Use: play | pause | stop | get_state");
            }
        }

        private static string GetEditorState(string args)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isPlaying = EditorApplication.isPlaying;
            bool isPaused  = EditorApplication.isPaused;
            bool isCompiling = EditorApplication.isCompiling;
            string selected = Selection.activeGameObject != null ? Selection.activeGameObject.name : "none";

            return $@"{{
  ""is_playing"":{isPlaying.ToString().ToLower()},
  ""is_paused"":{isPaused.ToString().ToLower()},
  ""is_compiling"":{isCompiling.ToString().ToLower()},
  ""active_scene"":""{scene}"",
  ""selected_object"":""{selected}"",
  ""unity_version"":""{Application.unityVersion}"",
  ""platform"":""{EditorUserBuildSettings.activeBuildTarget}""
}}";
        }

        private static string ManageComponents(string args)
        {
            string action     = ParseArg(args, "action")     ?? "list";
            string goName     = ParseArg(args, "gameobject");
            string compName   = ParseArg(args, "component");
            string property   = ParseArg(args, "property");
            string value      = ParseArg(args, "value");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");
            var go = GameObject.Find(goName);
            if (go == null) return $"{{\"error\":\"GameObject not found: {goName}\"}}";

            switch (action.ToLower())
            {
                case "list":
                    var comps = go.GetComponents<Component>();
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < comps.Length; i++)
                    {
                        if (comps[i] == null) continue;
                        sb.Append($"\"{comps[i].GetType().Name}\"");
                        if (i < comps.Length - 1) sb.Append(",");
                    }
                    sb.Append("]");
                    return $"{{\"gameobject\":\"{goName}\",\"components\":{sb}}}";

                case "add":
                    if (string.IsNullOrEmpty(compName)) return Error("component is required for add");
                    var compType = GetTypeByName(compName);
                    if (compType == null) return Error($"Component type not found: {compName}");
                    EditorApplication.delayCall += () =>
                    {
                        var g = GameObject.Find(goName);
                        if (g != null) Undo.AddComponent(g, compType);
                    };
                    return $"{{\"action\":\"add\",\"gameobject\":\"{goName}\",\"component\":\"{compName}\"}}";

                case "remove":
                    if (string.IsNullOrEmpty(compName)) return Error("component is required for remove");
                    EditorApplication.delayCall += () =>
                    {
                        var g = GameObject.Find(goName);
                        if (g != null)
                        {
                            var c = g.GetComponent(compName);
                            if (c != null) Undo.DestroyObjectImmediate(c);
                        }
                    };
                    return $"{{\"action\":\"remove\",\"gameobject\":\"{goName}\",\"component\":\"{compName}\"}}";

                case "get":
                    if (string.IsNullOrEmpty(compName)) return Error("component is required for get");
                    var existing = go.GetComponent(compName);
                    if (existing == null) return $"{{\"found\":false,\"component\":\"{compName}\"}}";
                    return $"{{\"found\":true,\"component\":\"{compName}\",\"enabled\":{(existing as Behaviour)?.enabled.ToString().ToLower() ?? "true"}}}";

                default:
                    return Error($"Unknown action: {action}. Use: list | add | remove | get");
            }
        }

        private static string FindInFile(string args)
        {
            string query     = ParseArg(args, "query");
            string searchDir = ParseArg(args, "path")      ?? "Assets";
            string ext       = ParseArg(args, "extension") ?? ".cs";

            if (string.IsNullOrEmpty(query)) return Error("query is required");

            string fullDir = Path.Combine(Application.dataPath, "..", searchDir);
            if (!Directory.Exists(fullDir)) return Error($"Directory not found: {searchDir}");

            string[] files = Directory.GetFiles(fullDir, $"*{ext}", SearchOption.AllDirectories);
            var matches    = new StringBuilder("[");
            int matchCount = 0;
            bool first     = true;

            foreach (var file in files)
            {
                if (matchCount >= 50) break; // cap results
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!Regex.IsMatch(lines[i], query, RegexOptions.IgnoreCase)) continue;
                    string rel = file.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                    if (!first) matches.Append(",");
                    matches.Append($"{{\"file\":\"{rel}\",\"line\":{i + 1},\"text\":{EscapeJson(lines[i].Trim())}}}");
                    first = false;
                    matchCount++;
                }
            }
            matches.Append("]");
            return $"{{\"matches\":{matches},\"count\":{matchCount},\"query\":\"{query}\"}}";
        }

        private static string ManageAsset(string args)
        {
            string action = ParseArg(args, "action") ?? "list";
            string path   = ParseArg(args, "path");
            string dest   = ParseArg(args, "dest");

            switch (action.ToLower())
            {
                case "delete":
                    if (string.IsNullOrEmpty(path)) return Error("path required");
                    EditorApplication.delayCall += () => AssetDatabase.DeleteAsset(path);
                    return $"{{\"action\":\"delete\",\"path\":\"{path}\"}}";

                case "copy":
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dest)) return Error("path and dest required");
                    EditorApplication.delayCall += () => AssetDatabase.CopyAsset(path, dest);
                    return $"{{\"action\":\"copy\",\"from\":\"{path}\",\"to\":\"{dest}\"}}";

                case "move":
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dest)) return Error("path and dest required");
                    EditorApplication.delayCall += () => AssetDatabase.MoveAsset(path, dest);
                    return $"{{\"action\":\"move\",\"from\":\"{path}\",\"to\":\"{dest}\"}}";

                case "rename":
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dest)) return Error("path and dest required");
                    EditorApplication.delayCall += () => AssetDatabase.RenameAsset(path, dest);
                    return $"{{\"action\":\"rename\",\"path\":\"{path}\",\"new_name\":\"{dest}\"}}";

                case "create_folder":
                    if (string.IsNullOrEmpty(path)) return Error("path required (parent folder)");
                    string folderName = dest ?? "NewFolder";
                    EditorApplication.delayCall += () => AssetDatabase.CreateFolder(path, folderName);
                    return $"{{\"action\":\"create_folder\",\"parent\":\"{path}\",\"name\":\"{folderName}\"}}";

                case "list":
                    string searchRoot = path ?? "Assets";
                    string fullPath   = Path.Combine(Application.dataPath, "..", searchRoot);
                    if (!Directory.Exists(fullPath)) return Error($"Path not found: {searchRoot}");
                    string[] entries = Directory.GetFileSystemEntries(fullPath);
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < Math.Min(entries.Length, 100); i++)
                    {
                        string rel = entries[i].Replace(Application.dataPath, "Assets").Replace("\\", "/");
                        bool isDir = Directory.Exists(entries[i]);
                        if (i > 0) sb.Append(",");
                        sb.Append($"{{\"path\":\"{rel}\",\"type\":\"{(isDir ? "folder" : "file")}\"}}");
                    }
                    sb.Append("]");
                    return $"{{\"action\":\"list\",\"path\":\"{searchRoot}\",\"entries\":{sb},\"count\":{entries.Length}}}";

                case "get_info":
                    if (string.IsNullOrEmpty(path)) return Error("path required");
                    string fPath = Path.Combine(Application.dataPath, "..", path);
                    bool exists = File.Exists(fPath) || Directory.Exists(fPath);
                    long size   = exists && File.Exists(fPath) ? new FileInfo(fPath).Length : 0;
                    return $"{{\"path\":\"{path}\",\"exists\":{exists.ToString().ToLower()},\"size_bytes\":{size}}}";

                default:
                    return Error($"Unknown action: {action}. Use: copy | delete | move | rename | create_folder | list | get_info");
            }
        }

        private static string DeleteScript(string args)
        {
            string assetPath = ParseArg(args, "path");
            if (string.IsNullOrEmpty(assetPath)) return Error("path is required");
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.Refresh();
            };
            return $"{{\"deleted\":\"{assetPath}\",\"message\":\"Script deleted and database refreshed\"}}";
        }

        private static string UnityDocs(string args)
        {
            string query   = ParseArg(args, "query")   ?? "";
            string version = ParseArg(args, "version") ?? Application.unityVersion.Substring(0, 6);
            // Format version for docs URL e.g. "2022.3" → "2022.3"
            string cleanVer = Regex.Match(version, @"\d{4}\.\d+").Value;
            string url = $"https://docs.unity3d.com/ScriptReference/{query}.html";
            string searchUrl = $"https://docs.unity3d.com/{cleanVer}/Documentation/ScriptReference/30_search.html?q={Uri.EscapeDataString(query)}";

            return $@"{{
  ""query"":""{query}"",
  ""direct_url"":""{url}"",
  ""search_url"":""{searchUrl}"",
  ""message"":""Open one of these URLs in your browser to view Unity docs"",
  ""common_classes"":{{
    ""GameObject"":""https://docs.unity3d.com/ScriptReference/GameObject.html"",
    ""Transform"":""https://docs.unity3d.com/ScriptReference/Transform.html"",
    ""MonoBehaviour"":""https://docs.unity3d.com/ScriptReference/MonoBehaviour.html"",
    ""Physics"":""https://docs.unity3d.com/ScriptReference/Physics.html"",
    ""PlayerPrefs"":""https://docs.unity3d.com/ScriptReference/PlayerPrefs.html""
  }}
}}";
        }

        private static string ApplyTextEdits(string args)
        {
            string assetPath = ParseArg(args, "path");
            if (string.IsNullOrEmpty(assetPath)) return Error("path is required");

            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            if (!File.Exists(fullPath)) return Error($"File not found: {assetPath}");

            // Parse edits array: [{"old":"...","new":"..."},...]
            string editsArr = ExtractArray(args, "edits");
            if (string.IsNullOrEmpty(editsArr)) return Error("edits array is required");

            string src = File.ReadAllText(fullPath);
            int applied = 0;

            // Find each {"old":..., "new":...} pair
            var editMatches = Regex.Matches(editsArr, @"\{[^{}]*\}");
            foreach (Match m in editMatches)
            {
                string oldVal = ParseString(m.Value, "old") ?? ParseString(m.Value, "find");
                string newVal = ParseString(m.Value, "new") ?? ParseString(m.Value, "replace") ?? "";
                if (string.IsNullOrEmpty(oldVal)) continue;
                if (src.Contains(oldVal))
                {
                    src = src.Replace(oldVal, newVal);
                    applied++;
                }
            }

            File.WriteAllText(fullPath, src);
            AssetDatabase.Refresh();
            return $"{{\"path\":\"{assetPath}\",\"edits_applied\":{applied}}}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Type GetTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName) ??
                        asm.GetType($"UnityEngine.{typeName}") ??
                        asm.GetType($"UnityEngine.UI.{typeName}");
                if (t != null) return t;
            }
            return null;
        }

        private static System.Collections.Generic.List<string> SplitTopLevelObjects(string json)
        {
            var result = new System.Collections.Generic.List<string>();
            int depth = 0; int start = -1;
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '{') { if (depth++ == 0) start = i; }
                else if (json[i] == '}') { if (--depth == 0 && start >= 0) { result.Add(json.Substring(start, i - start + 1)); start = -1; } }
            }
            return result;
        }

        private static string ExtractObject(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = json.IndexOf('{', idx);
            if (start < 0) return null;
            int depth = 0; var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                sb.Append(json[i]);
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { if (--depth == 0) break; }
            }
            return sb.ToString();
        }

        private static string ExtractArray(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = json.IndexOf('[', idx);
            if (start < 0) return null;
            int depth = 0; var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                sb.Append(json[i]);
                if (json[i] == '[') depth++;
                else if (json[i] == ']') { if (--depth == 0) break; }
            }
            return sb.ToString();
        }

        private static string ParseString(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string EscapeJson(string s)
            => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";

        private static string Error(string msg) => $"{{\"error\":\"{msg}\"}}";

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string ToolDef(string name, string desc)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
