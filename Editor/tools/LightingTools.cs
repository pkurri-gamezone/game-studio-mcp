using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameStudioMCP
{
    /// <summary>
    /// Lighting tools: manage_light, set_lighting_settings, manage_reflection_probe, bake_lighting
    /// </summary>
    public static class LightingTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_light",
                ToolDef("manage_light",
                    "Create or configure a Light in the scene. Supports Directional, Point, Spot, Area.",
                    Param("action",     "string", "create | configure | delete | list"),
                    Param("name",       "string", "Light GameObject name"),
                    Param("type",       "string", "Directional | Point | Spot | Area (default: Directional)"),
                    Param("color",      "string", "Optional: hex color e.g. #FFFFFF"),
                    Param("intensity",  "string", "Optional: intensity value e.g. 1.0"),
                    Param("range",      "string", "Optional: range for Point/Spot lights"),
                    Param("spot_angle", "string", "Optional: spot angle in degrees for Spot lights"),
                    Param("shadows",    "string", "Optional: None | Hard | Soft"),
                    Param("position",   "string", "Optional: position as 'x,y,z'")),
                ManageLight);

            MCPToolRegistry.Register("set_lighting_settings",
                ToolDef("set_lighting_settings",
                    "Configure global scene lighting: ambient mode, ambient color, skybox material, fog, HDR.",
                    Param("ambient_mode",  "string", "Skybox | Trilight | Flat | Custom"),
                    Param("ambient_color", "string", "Optional: hex color for flat ambient"),
                    Param("skybox_material", "string", "Optional: asset path to skybox material"),
                    Param("fog_enabled",   "string", "Optional: true|false"),
                    Param("fog_color",     "string", "Optional: hex fog color"),
                    Param("fog_density",   "string", "Optional: fog density 0-1"),
                    Param("realtime_gi",   "string", "Optional: true|false — enable Realtime GI")),
                SetLightingSettings);

            MCPToolRegistry.Register("manage_reflection_probe",
                ToolDef("manage_reflection_probe",
                    "Create or configure a Reflection Probe for environment reflections.",
                    Param("action",     "string", "create | configure | bake | delete"),
                    Param("name",       "string", "Reflection probe GameObject name"),
                    Param("mode",       "string", "Optional: Baked | Realtime | Custom"),
                    Param("intensity",  "string", "Optional: intensity multiplier"),
                    Param("size",       "string", "Optional: size as 'x,y,z'"),
                    Param("position",   "string", "Optional: position as 'x,y,z'")),
                ManageReflectionProbe);

            MCPToolRegistry.Register("bake_lighting",
                ToolDef("bake_lighting",
                    "Trigger Unity lightmap baking or clear baked data.",
                    Param("action",      "string", "bake | bake_reflection | clear | cancel | get_status"),
                    Param("force_clear", "string", "Optional: true — clear existing lightmaps before baking")),
                BakeLighting);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageLight(string args)
        {
            string action    = ParseArg(args, "action")    ?? "create";
            string name      = ParseArg(args, "name")      ?? "New Light";
            string typeStr   = ParseArg(args, "type")      ?? "Directional";
            string colorStr  = ParseArg(args, "color");
            string intensStr = ParseArg(args, "intensity");
            string rangeStr  = ParseArg(args, "range");
            string shadowStr = ParseArg(args, "shadows");
            string posStr    = ParseArg(args, "position");
            string spotAngle = ParseArg(args, "spot_angle");

            if (action == "list")
            {
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
                var sb = new System.Text.StringBuilder("[");
                for (int i = 0; i < lights.Length; i++)
                {
                    sb.Append($"{{\"name\":\"{lights[i].gameObject.name}\",\"type\":\"{lights[i].type}\",\"intensity\":{lights[i].intensity}}}");
                    if (i < lights.Length - 1) sb.Append(",");
                }
                sb.Append("]");
                return $"{{\"lights\":{sb}}}";
            }

            EditorApplication.delayCall += () =>
            {
                if (action == "delete")
                {
                    var existing = GameObject.Find(name);
                    if (existing != null) { Undo.DestroyObjectImmediate(existing); }
                    return;
                }

                var go = GameObject.Find(name) ?? new GameObject(name);
                go.name = name;

                var light = go.GetComponent<Light>() ?? go.AddComponent<Light>();

                if (System.Enum.TryParse<LightType>(typeStr, true, out var lt)) light.type = lt;

                if (!string.IsNullOrEmpty(colorStr))
                {
                    if (ColorUtility.TryParseHtmlString(colorStr.StartsWith("#") ? colorStr : "#" + colorStr, out Color c))
                        light.color = c;
                }
                if (!string.IsNullOrEmpty(intensStr) && float.TryParse(intensStr, out float intensity)) light.intensity = intensity;
                if (!string.IsNullOrEmpty(rangeStr)  && float.TryParse(rangeStr,  out float range))     light.range = range;
                if (!string.IsNullOrEmpty(spotAngle) && float.TryParse(spotAngle, out float sa))        light.spotAngle = sa;

                if (!string.IsNullOrEmpty(shadowStr))
                {
                    switch (shadowStr.ToLower())
                    {
                        case "hard": light.shadows = LightShadows.Hard; break;
                        case "soft": light.shadows = LightShadows.Soft; break;
                        case "none": light.shadows = LightShadows.None; break;
                    }
                }

                if (!string.IsNullOrEmpty(posStr))
                {
                    var parts = posStr.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), out float px) &&
                        float.TryParse(parts[1].Trim(), out float py) && float.TryParse(parts[2].Trim(), out float pz))
                        go.transform.position = new Vector3(px, py, pz);
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create Light {name}");
                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"name\":\"{name}\",\"type\":\"{typeStr}\",\"status\":\"queued\"}}";
        }

        private static string SetLightingSettings(string args)
        {
            string ambientMode   = ParseArg(args, "ambient_mode");
            string ambientColor  = ParseArg(args, "ambient_color");
            string skyboxMat     = ParseArg(args, "skybox_material");
            string fogEnabled    = ParseArg(args, "fog_enabled");
            string fogColor      = ParseArg(args, "fog_color");
            string fogDensity    = ParseArg(args, "fog_density");
            string realtimeGI    = ParseArg(args, "realtime_gi");

            EditorApplication.delayCall += () =>
            {
                if (!string.IsNullOrEmpty(ambientMode))
                {
                    switch (ambientMode.ToLower())
                    {
                        case "flat":    RenderSettings.ambientMode = AmbientMode.Flat;     break;
                        case "trilight":RenderSettings.ambientMode = AmbientMode.Trilight; break;
                        case "skybox":  RenderSettings.ambientMode = AmbientMode.Skybox;   break;
                    }
                }

                if (!string.IsNullOrEmpty(ambientColor) &&
                    ColorUtility.TryParseHtmlString(ambientColor.StartsWith("#") ? ambientColor : "#" + ambientColor, out Color ac))
                    RenderSettings.ambientLight = ac;

                if (!string.IsNullOrEmpty(skyboxMat))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(skyboxMat);
                    if (mat != null) RenderSettings.skybox = mat;
                }

                if (!string.IsNullOrEmpty(fogEnabled))   RenderSettings.fog = fogEnabled.ToLower() == "true";
                if (!string.IsNullOrEmpty(fogColor) &&
                    ColorUtility.TryParseHtmlString(fogColor.StartsWith("#") ? fogColor : "#" + fogColor, out Color fc))
                    RenderSettings.fogColor = fc;
                if (!string.IsNullOrEmpty(fogDensity) && float.TryParse(fogDensity, out float fd)) RenderSettings.fogDensity = fd;
                if (!string.IsNullOrEmpty(realtimeGI)) Lightmapping.realtimeGI = realtimeGI.ToLower() == "true";
            };

            return "{\"action\":\"set_lighting_settings\",\"status\":\"queued\"}";
        }

        private static string ManageReflectionProbe(string args)
        {
            string action   = ParseArg(args, "action")    ?? "create";
            string name     = ParseArg(args, "name")      ?? "Reflection Probe";
            string modeStr  = ParseArg(args, "mode")      ?? "Baked";
            string intensStr= ParseArg(args, "intensity");
            string sizeStr  = ParseArg(args, "size");
            string posStr   = ParseArg(args, "position");

            EditorApplication.delayCall += () =>
            {
                if (action == "bake")
                {
                    Lightmapping.BakeAsync();
                    return;
                }

                if (action == "delete")
                {
                    var ex = GameObject.Find(name);
                    if (ex != null) Undo.DestroyObjectImmediate(ex);
                    return;
                }

                var go    = GameObject.Find(name) ?? new GameObject(name);
                var probe = go.GetComponent<ReflectionProbe>() ?? go.AddComponent<ReflectionProbe>();

                if (System.Enum.TryParse<ReflectionProbeMode>(modeStr, true, out var mode)) probe.mode = mode;
                if (!string.IsNullOrEmpty(intensStr) && float.TryParse(intensStr, out float iv)) probe.intensity = iv;

                if (!string.IsNullOrEmpty(sizeStr))
                {
                    var parts = sizeStr.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), out float sx) &&
                        float.TryParse(parts[1].Trim(), out float sy) && float.TryParse(parts[2].Trim(), out float sz))
                        probe.size = new Vector3(sx, sy, sz);
                }

                if (!string.IsNullOrEmpty(posStr))
                {
                    var parts = posStr.Split(',');
                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), out float px) &&
                        float.TryParse(parts[1].Trim(), out float py) && float.TryParse(parts[2].Trim(), out float pz))
                        go.transform.position = new Vector3(px, py, pz);
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create ReflectionProbe {name}");
                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"name\":\"{name}\",\"status\":\"queued\"}}";
        }

        private static string BakeLighting(string args)
        {
            string action     = ParseArg(args, "action")      ?? "bake";
            string forceClear = ParseArg(args, "force_clear");

            switch (action.ToLower())
            {
                case "bake":
                    EditorApplication.delayCall += () =>
                    {
                        if (forceClear?.ToLower() == "true") Lightmapping.Clear();
                        Lightmapping.BakeAsync();
                    };
                    return "{\"action\":\"bake\",\"status\":\"bake_started\"}";
                case "bake_reflection":
                    EditorApplication.delayCall += () => Lightmapping.BakeAsync();
                    return "{\"action\":\"bake_reflection\",\"status\":\"queued\"}";
                case "clear":
                    EditorApplication.delayCall += () => Lightmapping.Clear();
                    return "{\"action\":\"clear\",\"status\":\"queued\"}";
                case "cancel":
                    EditorApplication.delayCall += () => Lightmapping.Cancel();
                    return "{\"action\":\"cancel\",\"status\":\"queued\"}";
                case "get_status":
                    return $"{{\"action\":\"get_status\",\"is_baking\":{Lightmapping.isRunning.ToString().ToLower()}}}";
                default:
                    return Error($"Unknown action: {action}");
            }
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
