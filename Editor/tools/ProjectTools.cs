using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// Project-level tools: manage_tags, manage_layers, manage_sorting_layers,
    /// manage_player_settings, manage_player_prefs
    /// </summary>
    public static class ProjectTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_tags",
                ToolDef("manage_tags",
                    "Add, remove, or list Unity tags in Project Settings.",
                    Param("action", "string", "add | remove | list"),
                    Param("tag",    "string", "Optional: tag name to add or remove e.g. 'Enemy' or 'Collectable'")),
                ManageTags);

            MCPToolRegistry.Register("manage_layers",
                ToolDef("manage_layers",
                    "Add, remove, or list Unity layers in Project Settings. Built-in layers 0-7 cannot be modified.",
                    Param("action", "string", "add | remove | list | set_collision"),
                    Param("layer",  "string", "Optional: layer name to add or remove e.g. 'Gameplay' or 'UI'"),
                    Param("layer_a","string", "Optional: first layer name for set_collision"),
                    Param("layer_b","string", "Optional: second layer name for set_collision"),
                    Param("ignore", "string", "Optional: true|false — whether layers should ignore each other")),
                ManageLayers);

            MCPToolRegistry.Register("manage_sorting_layers",
                ToolDef("manage_sorting_layers",
                    "Add, remove, or list sorting layers for 2D rendering.",
                    Param("action", "string", "add | remove | list"),
                    Param("layer",  "string", "Optional: sorting layer name to add or remove")),
                ManageSortingLayers);

            MCPToolRegistry.Register("manage_player_settings",
                ToolDef("manage_player_settings",
                    "Configure Unity PlayerSettings: company name, product name, bundle ID, version, orientation, icons, scripting backend.",
                    Param("company_name",       "string", "Optional: company name"),
                    Param("product_name",       "string", "Optional: product/game name"),
                    Param("bundle_id",          "string", "Optional: application identifier e.g. com.studio.mygame"),
                    Param("version",            "string", "Optional: version string e.g. 1.0.0"),
                    Param("build_number",       "string", "Optional: build/bundle version code e.g. 1"),
                    Param("orientation",        "string", "Optional: Portrait|LandscapeLeft|LandscapeRight|AutoRotation"),
                    Param("scripting_backend",  "string", "Optional: Mono|IL2CPP"),
                    Param("target_platform",    "string", "Optional: iOS|Android|Standalone (for platform-specific settings)"),
                    Param("min_sdk",            "string", "Optional: Android minimum API level e.g. 21"),
                    Param("target_sdk",         "string", "Optional: Android target API level e.g. 33"),
                    Param("allow_unsafe",       "string", "Optional: true|false — allow unsafe code")),
                ManagePlayerSettings);

            MCPToolRegistry.Register("manage_player_prefs",
                ToolDef("manage_player_prefs",
                    "Get, set, delete, or clear PlayerPrefs keys at runtime (play mode only).",
                    Param("action", "string", "get | set | delete | clear | has | list_known"),
                    Param("key",    "string", "Optional: PlayerPrefs key name"),
                    Param("value",  "string", "Optional: value to set (string, float, or int)"),
                    Param("type",   "string", "Optional: string|float|int (default: string)")),
                ManagePlayerPrefs);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageTags(string args)
        {
            string action  = ParseArg(args, "action") ?? "list";
            string tagName = ParseArg(args, "tag");

            if (action == "list")
            {
                var tags = UnityEditorInternal.InternalEditorUtility.tags;
                var sb = new StringBuilder("[");
                for (int i = 0; i < tags.Length; i++)
                {
                    sb.Append($"\"{tags[i]}\"");
                    if (i < tags.Length - 1) sb.Append(",");
                }
                sb.Append("]");
                return $"{{\"tags\":{sb}}}";
            }

            if (string.IsNullOrEmpty(tagName)) return Error("tag is required for add/remove");

            if (action == "add")
            {
                EditorApplication.delayCall += () =>
                {
                    var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
                    var tagsProp = so.FindProperty("tags");
                    // Check not already existing
                    bool exists = false;
                    for (int i = 0; i < tagsProp.arraySize; i++)
                        if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName) { exists = true; break; }
                    if (!exists)
                    {
                        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
                        so.ApplyModifiedProperties();
                        Debug.Log($"[GameStudioMCP] Added tag: {tagName}");
                    }
                    else Debug.Log($"[GameStudioMCP] Tag already exists: {tagName}");
                };
                return $"{{\"action\":\"add\",\"tag\":\"{tagName}\",\"status\":\"queued\"}}";
            }

            if (action == "remove")
            {
                EditorApplication.delayCall += () =>
                {
                    var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
                    var tagsProp = so.FindProperty("tags");
                    for (int i = 0; i < tagsProp.arraySize; i++)
                    {
                        if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                        {
                            tagsProp.DeleteArrayElementAtIndex(i);
                            so.ApplyModifiedProperties();
                            Debug.Log($"[GameStudioMCP] Removed tag: {tagName}");
                            break;
                        }
                    }
                };
                return $"{{\"action\":\"remove\",\"tag\":\"{tagName}\",\"status\":\"queued\"}}";
            }

            return Error($"Unknown action: {action}");
        }

        private static string ManageLayers(string args)
        {
            string action  = ParseArg(args, "action")  ?? "list";
            string layer   = ParseArg(args, "layer");
            string layerA  = ParseArg(args, "layer_a");
            string layerB  = ParseArg(args, "layer_b");
            string ignore  = ParseArg(args, "ignore");

            if (action == "list")
            {
                var sb = new StringBuilder("[");
                bool first = true;
                for (int i = 0; i < 32; i++)
                {
                    string name = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (!first) sb.Append(",");
                        sb.Append($"{{\"index\":{i},\"name\":\"{name}\"}}");
                        first = false;
                    }
                }
                sb.Append("]");
                return $"{{\"layers\":{sb}}}";
            }

            if (action == "set_collision" && !string.IsNullOrEmpty(layerA) && !string.IsNullOrEmpty(layerB))
            {
                EditorApplication.delayCall += () =>
                {
                    int la = LayerMask.NameToLayer(layerA);
                    int lb = LayerMask.NameToLayer(layerB);
                    if (la >= 0 && lb >= 0)
                    {
                        bool shouldIgnore = ignore?.ToLower() == "true";
                        Physics.IgnoreLayerCollision(la, lb, shouldIgnore);
                        Physics2D.IgnoreLayerCollision(la, lb, shouldIgnore);
                    }
                };
                return $"{{\"action\":\"set_collision\",\"layer_a\":\"{layerA}\",\"layer_b\":\"{layerB}\",\"ignore\":\"{ignore}\",\"status\":\"queued\"}}";
            }

            if (string.IsNullOrEmpty(layer)) return Error("layer is required for add/remove");

            if (action == "add")
            {
                EditorApplication.delayCall += () =>
                {
                    var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
                    var layersProp = so.FindProperty("layers");
                    // User layers start at index 8
                    for (int i = 8; i < layersProp.arraySize; i++)
                    {
                        var el = layersProp.GetArrayElementAtIndex(i);
                        if (string.IsNullOrEmpty(el.stringValue))
                        {
                            el.stringValue = layer;
                            so.ApplyModifiedProperties();
                            Debug.Log($"[GameStudioMCP] Added layer '{layer}' at index {i}");
                            return;
                        }
                        if (el.stringValue == layer) { Debug.Log($"[GameStudioMCP] Layer already exists: {layer}"); return; }
                    }
                    Debug.LogWarning("[GameStudioMCP] No free layer slots available (max 32)");
                };
                return $"{{\"action\":\"add\",\"layer\":\"{layer}\",\"status\":\"queued\"}}";
            }

            if (action == "remove")
            {
                EditorApplication.delayCall += () =>
                {
                    var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
                    var layersProp = so.FindProperty("layers");
                    for (int i = 8; i < layersProp.arraySize; i++)
                    {
                        if (layersProp.GetArrayElementAtIndex(i).stringValue == layer)
                        {
                            layersProp.GetArrayElementAtIndex(i).stringValue = "";
                            so.ApplyModifiedProperties();
                            Debug.Log($"[GameStudioMCP] Removed layer: {layer}");
                            return;
                        }
                    }
                };
                return $"{{\"action\":\"remove\",\"layer\":\"{layer}\",\"status\":\"queued\"}}";
            }

            return Error($"Unknown action: {action}");
        }

        private static string ManageSortingLayers(string args)
        {
            string action = ParseArg(args, "action") ?? "list";
            string layer  = ParseArg(args, "layer");

            if (action == "list")
            {
                var layers = SortingLayer.layers;
                var sb = new StringBuilder("[");
                for (int i = 0; i < layers.Length; i++)
                {
                    sb.Append($"{{\"id\":{layers[i].id},\"name\":\"{layers[i].name}\",\"value\":{layers[i].value}}}");
                    if (i < layers.Length - 1) sb.Append(",");
                }
                sb.Append("]");
                return $"{{\"sorting_layers\":{sb}}}";
            }

            if (string.IsNullOrEmpty(layer)) return Error("layer is required");

            if (action == "add")
            {
                EditorApplication.delayCall += () =>
                {
                    var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
                    var sortingLayersProp = so.FindProperty("m_SortingLayers");
                    if (sortingLayersProp == null) { Debug.LogWarning("[GameStudioMCP] m_SortingLayers property not found"); return; }

                    bool exists = false;
                    for (int i = 0; i < sortingLayersProp.arraySize; i++)
                        if (sortingLayersProp.GetArrayElementAtIndex(i).FindPropertyRelative("name")?.stringValue == layer) { exists = true; break; }

                    if (!exists)
                    {
                        sortingLayersProp.InsertArrayElementAtIndex(sortingLayersProp.arraySize);
                        var newLayer = sortingLayersProp.GetArrayElementAtIndex(sortingLayersProp.arraySize - 1);
                        newLayer.FindPropertyRelative("name").stringValue = layer;
                        newLayer.FindPropertyRelative("uniqueID").intValue = layer.GetHashCode();
                        so.ApplyModifiedProperties();
                        Debug.Log($"[GameStudioMCP] Added sorting layer: {layer}");
                    }
                };
                return $"{{\"action\":\"add\",\"layer\":\"{layer}\",\"status\":\"queued\"}}";
            }

            return $"{{\"action\":\"{action}\",\"layer\":\"{layer}\",\"status\":\"queued\"}}";
        }

        private static string ManagePlayerSettings(string args)
        {
            string companyName      = ParseArg(args, "company_name");
            string productName      = ParseArg(args, "product_name");
            string bundleId         = ParseArg(args, "bundle_id");
            string version          = ParseArg(args, "version");
            string buildNumber      = ParseArg(args, "build_number");
            string orientation      = ParseArg(args, "orientation");
            string scriptingBackend = ParseArg(args, "scripting_backend");
            string targetPlatform   = ParseArg(args, "target_platform");
            string minSdk           = ParseArg(args, "min_sdk");
            string allowUnsafe      = ParseArg(args, "allow_unsafe");

            EditorApplication.delayCall += () =>
            {
                if (!string.IsNullOrEmpty(companyName)) PlayerSettings.companyName = companyName;
                if (!string.IsNullOrEmpty(productName)) PlayerSettings.productName = productName;
                if (!string.IsNullOrEmpty(version))     PlayerSettings.bundleVersion = version;

                if (!string.IsNullOrEmpty(bundleId))
                {
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,    bundleId);
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,        bundleId);
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, bundleId);
                }

                if (!string.IsNullOrEmpty(orientation))
                {
                    switch (orientation.ToLower())
                    {
                        case "portrait":       PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;      break;
                        case "landscapeleft":  PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft; break;
                        case "landscaperight": PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;break;
                        case "autorotation":   PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;  break;
                    }
                }

                if (!string.IsNullOrEmpty(scriptingBackend))
                {
                    var backend = scriptingBackend.ToUpper() == "IL2CPP"
                        ? ScriptingImplementation.IL2CPP
                        : ScriptingImplementation.Mono2x;
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, backend);
                    PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS,     backend);
                }

                if (!string.IsNullOrEmpty(minSdk) && int.TryParse(minSdk, out int sdk))
                    PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)sdk;

                if (!string.IsNullOrEmpty(buildNumber) && int.TryParse(buildNumber, out int bn))
                {
                    PlayerSettings.Android.bundleVersionCode = bn;
                    PlayerSettings.iOS.buildNumber = buildNumber;
                }

                if (!string.IsNullOrEmpty(allowUnsafe))
                    PlayerSettings.allowUnsafeCode = allowUnsafe.ToLower() == "true";

                AssetDatabase.SaveAssets();
                Debug.Log("[GameStudioMCP] PlayerSettings updated");
            };

            var result = new StringBuilder("{\"action\":\"manage_player_settings\"");
            if (!string.IsNullOrEmpty(companyName)) result.Append($",\"company_name\":\"{companyName}\"");
            if (!string.IsNullOrEmpty(productName)) result.Append($",\"product_name\":\"{productName}\"");
            if (!string.IsNullOrEmpty(bundleId))    result.Append($",\"bundle_id\":\"{bundleId}\"");
            if (!string.IsNullOrEmpty(version))     result.Append($",\"version\":\"{version}\"");
            result.Append(",\"status\":\"queued\"}");
            return result.ToString();
        }

        private static string ManagePlayerPrefs(string args)
        {
            string action = ParseArg(args, "action") ?? "get";
            string key    = ParseArg(args, "key");
            string value  = ParseArg(args, "value");
            string type   = ParseArg(args, "type") ?? "string";

            if (action == "clear")
            {
                if (EditorApplication.isPlaying) PlayerPrefs.DeleteAll();
                return "{\"action\":\"clear\",\"status\":\"ok\",\"note\":\"Only works in play mode\"}";
            }

            if (action == "list_known")
            {
                return "{\"action\":\"list_known\",\"note\":\"PlayerPrefs keys cannot be enumerated in Unity. Use get with specific keys.\"}";
            }

            if (string.IsNullOrEmpty(key)) return Error("key is required");

            if (action == "has")
                return $"{{\"action\":\"has\",\"key\":\"{key}\",\"exists\":{PlayerPrefs.HasKey(key).ToString().ToLower()}}}";

            if (action == "delete")
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                return $"{{\"action\":\"delete\",\"key\":\"{key}\",\"status\":\"ok\"}}";
            }

            if (action == "get")
            {
                if (!PlayerPrefs.HasKey(key)) return $"{{\"action\":\"get\",\"key\":\"{key}\",\"exists\":false}}";
                string val;
                switch (type.ToLower())
                {
                    case "float": val = PlayerPrefs.GetFloat(key).ToString();  break;
                    case "int":   val = PlayerPrefs.GetInt(key).ToString();    break;
                    default:      val = PlayerPrefs.GetString(key);            break;
                }
                return $"{{\"action\":\"get\",\"key\":\"{key}\",\"value\":\"{val}\",\"type\":\"{type}\"}}";
            }

            if (action == "set")
            {
                if (string.IsNullOrEmpty(value)) return Error("value is required for set");
                switch (type.ToLower())
                {
                    case "float": if (float.TryParse(value, out float f)) PlayerPrefs.SetFloat(key, f);  break;
                    case "int":   if (int.TryParse(value,   out int   i)) PlayerPrefs.SetInt(key, i);    break;
                    default:      PlayerPrefs.SetString(key, value); break;
                }
                PlayerPrefs.Save();
                return $"{{\"action\":\"set\",\"key\":\"{key}\",\"value\":\"{value}\",\"type\":\"{type}\",\"status\":\"ok\"}}";
            }

            return Error($"Unknown action: {action}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string Error(string msg) => $"{{\"error\":\"{msg}\"}}";

        private static string ToolDef(string name, string desc, params string[] inputProps)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", inputProps)}}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
