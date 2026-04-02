using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// Particle tools: manage_particle_system, set_particle_emission, set_particle_renderer, preview_particles
    /// </summary>
    public static class ParticleTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_particle_system",
                ToolDef("manage_particle_system",
                    "Create or configure a Particle System on a GameObject. Set duration, looping, start values.",
                    Param("action",          "string", "create | configure | delete | get"),
                    Param("name",            "string", "Particle system GameObject name"),
                    Param("parent",          "string", "Optional: parent GameObject name"),
                    Param("duration",        "string", "Optional: system duration in seconds"),
                    Param("looping",         "string", "Optional: true|false"),
                    Param("start_lifetime",  "string", "Optional: particle start lifetime e.g. 1.0 or '0.5,2.0' for random"),
                    Param("start_speed",     "string", "Optional: start speed e.g. 5.0"),
                    Param("start_size",      "string", "Optional: start size e.g. 0.1"),
                    Param("start_color",     "string", "Optional: hex start color e.g. #FF6600"),
                    Param("max_particles",   "string", "Optional: max particle count e.g. 100"),
                    Param("gravity",         "string", "Optional: gravity modifier e.g. 0 or -1")),
                ManageParticleSystem);

            MCPToolRegistry.Register("set_particle_emission",
                ToolDef("set_particle_emission",
                    "Configure emission rate, bursts, and emission shape for a Particle System.",
                    Param("gameobject",   "string", "Target ParticleSystem GameObject name"),
                    Param("rate",         "string", "Optional: emission rate over time e.g. 10"),
                    Param("burst_count",  "string", "Optional: burst particle count e.g. 20"),
                    Param("burst_time",   "string", "Optional: burst time e.g. 0.0"),
                    Param("shape",        "string", "Optional: Sphere | Cone | Box | Circle | Edge | Rectangle"),
                    Param("shape_radius", "string", "Optional: shape radius e.g. 1.0"),
                    Param("shape_angle",  "string", "Optional: cone angle in degrees")),
                SetParticleEmission);

            MCPToolRegistry.Register("set_particle_renderer",
                ToolDef("set_particle_renderer",
                    "Configure the Particle System Renderer: material, render mode, sorting.",
                    Param("gameobject",    "string", "Target ParticleSystem GameObject name"),
                    Param("material_path", "string", "Optional: asset path to material"),
                    Param("render_mode",   "string", "Optional: Billboard | Stretch | HorizontalBillboard | VerticalBillboard | Mesh"),
                    Param("sort_mode",     "string", "Optional: None | ByDistance | OldestInFront | YoungestInFront"),
                    Param("sorting_layer", "string", "Optional: sorting layer name"),
                    Param("order_in_layer","string", "Optional: order in sorting layer integer")),
                SetParticleRenderer);

            MCPToolRegistry.Register("preview_particles",
                ToolDef("preview_particles",
                    "Play, pause, stop, or restart a Particle System preview in the Editor.",
                    Param("action",     "string", "play | stop | restart | simulate"),
                    Param("gameobject", "string", "Target ParticleSystem GameObject name"),
                    Param("time",       "string", "Optional: simulation time in seconds for simulate action")),
                PreviewParticles);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageParticleSystem(string args)
        {
            string action       = ParseArg(args, "action")         ?? "create";
            string name         = ParseArg(args, "name")           ?? "Particles";
            string parent       = ParseArg(args, "parent");
            string durationStr  = ParseArg(args, "duration");
            string loopingStr   = ParseArg(args, "looping");
            string lifetimeStr  = ParseArg(args, "start_lifetime");
            string speedStr     = ParseArg(args, "start_speed");
            string sizeStr      = ParseArg(args, "start_size");
            string colorStr     = ParseArg(args, "start_color");
            string maxPartStr   = ParseArg(args, "max_particles");
            string gravityStr   = ParseArg(args, "gravity");

            if (action == "delete")
            {
                EditorApplication.delayCall += () =>
                {
                    var go = GameObject.Find(name);
                    if (go != null) Undo.DestroyObjectImmediate(go);
                };
                return $"{{\"action\":\"delete\",\"name\":\"{name}\",\"status\":\"queued\"}}";
            }

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(name) ?? new GameObject(name);
                go.name = name;

                if (!string.IsNullOrEmpty(parent))
                {
                    var p = GameObject.Find(parent);
                    if (p != null) go.transform.SetParent(p.transform, false);
                }

                var ps = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
                var main = ps.main;

                if (!string.IsNullOrEmpty(durationStr)  && float.TryParse(durationStr, out float dur)) main.duration = dur;
                if (!string.IsNullOrEmpty(loopingStr))    main.loop     = loopingStr.ToLower() == "true";
                if (!string.IsNullOrEmpty(speedStr)     && float.TryParse(speedStr,    out float spd)) main.startSpeed = spd;
                if (!string.IsNullOrEmpty(sizeStr)      && float.TryParse(sizeStr,     out float sz))  main.startSize  = sz;
                if (!string.IsNullOrEmpty(maxPartStr)   && int.TryParse(maxPartStr,    out int mp))    main.maxParticles = mp;
                if (!string.IsNullOrEmpty(gravityStr)   && float.TryParse(gravityStr,  out float gv))  main.gravityModifier = gv;

                if (!string.IsNullOrEmpty(lifetimeStr))
                {
                    var parts = lifetimeStr.Split(',');
                    if (parts.Length >= 2 && float.TryParse(parts[0].Trim(), out float ltMin) && float.TryParse(parts[1].Trim(), out float ltMax))
                        main.startLifetime = new ParticleSystem.MinMaxCurve(ltMin, ltMax);
                    else if (float.TryParse(lifetimeStr, out float lt))
                        main.startLifetime = lt;
                }

                if (!string.IsNullOrEmpty(colorStr) &&
                    ColorUtility.TryParseHtmlString(colorStr.StartsWith("#") ? colorStr : "#" + colorStr, out Color c))
                    main.startColor = c;

                Undo.RegisterCreatedObjectUndo(go, $"Create ParticleSystem {name}");
                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"name\":\"{name}\",\"status\":\"queued\"}}";
        }

        private static string SetParticleEmission(string args)
        {
            string goName       = ParseArg(args, "gameobject");
            string rateStr      = ParseArg(args, "rate");
            string burstCount   = ParseArg(args, "burst_count");
            string burstTime    = ParseArg(args, "burst_time");
            string shape        = ParseArg(args, "shape");
            string radiusStr    = ParseArg(args, "shape_radius");
            string angleStr     = ParseArg(args, "shape_angle");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                var ps = go?.GetComponent<ParticleSystem>();
                if (ps == null) { Debug.LogWarning($"[GameStudioMCP] set_particle_emission: no PS on '{goName}'"); return; }

                var emission = ps.emission;
                emission.enabled = true;

                if (!string.IsNullOrEmpty(rateStr) && float.TryParse(rateStr, out float rate))
                    emission.rateOverTime = rate;

                if (!string.IsNullOrEmpty(burstCount) && int.TryParse(burstCount, out int bc))
                {
                    float bt = 0f;
                    if (!string.IsNullOrEmpty(burstTime)) float.TryParse(burstTime, out bt);
                    emission.SetBursts(new[] { new ParticleSystem.Burst(bt, bc) });
                }

                if (!string.IsNullOrEmpty(shape))
                {
                    var sh = ps.shape;
                    sh.enabled = true;
                    switch (shape.ToLower())
                    {
                        case "sphere":    sh.shapeType = ParticleSystemShapeType.Sphere;    break;
                        case "cone":      sh.shapeType = ParticleSystemShapeType.Cone;      break;
                        case "box":       sh.shapeType = ParticleSystemShapeType.Box;       break;
                        case "circle":    sh.shapeType = ParticleSystemShapeType.Circle;    break;
                        case "rectangle": sh.shapeType = ParticleSystemShapeType.Rectangle; break;
                        default:          sh.shapeType = ParticleSystemShapeType.Sphere;    break;
                    }
                    if (!string.IsNullOrEmpty(radiusStr) && float.TryParse(radiusStr, out float r)) sh.radius = r;
                    if (!string.IsNullOrEmpty(angleStr)  && float.TryParse(angleStr,  out float a)) sh.angle  = a;
                }

                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"set_emission\",\"gameobject\":\"{goName}\",\"status\":\"queued\"}}";
        }

        private static string SetParticleRenderer(string args)
        {
            string goName       = ParseArg(args, "gameobject");
            string matPath      = ParseArg(args, "material_path");
            string renderMode   = ParseArg(args, "render_mode");
            string sortingLayer = ParseArg(args, "sorting_layer");
            string orderStr     = ParseArg(args, "order_in_layer");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                var psr = go?.GetComponent<ParticleSystemRenderer>();
                if (psr == null) { Debug.LogWarning($"[GameStudioMCP] set_particle_renderer: no PSR on '{goName}'"); return; }

                if (!string.IsNullOrEmpty(matPath))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat != null) psr.material = mat;
                }

                if (!string.IsNullOrEmpty(renderMode) &&
                    System.Enum.TryParse<ParticleSystemRenderMode>(renderMode, true, out var rm))
                    psr.renderMode = rm;

                if (!string.IsNullOrEmpty(sortingLayer)) psr.sortingLayerName = sortingLayer;
                if (!string.IsNullOrEmpty(orderStr) && int.TryParse(orderStr, out int order)) psr.sortingOrder = order;

                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"set_renderer\",\"gameobject\":\"{goName}\",\"status\":\"queued\"}}";
        }

        private static string PreviewParticles(string args)
        {
            string action = ParseArg(args, "action")     ?? "play";
            string goName = ParseArg(args, "gameobject");
            string timeStr= ParseArg(args, "time");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                var ps = go?.GetComponent<ParticleSystem>();
                if (ps == null) { Debug.LogWarning($"[GameStudioMCP] preview_particles: no PS on '{goName}'"); return; }

                switch (action.ToLower())
                {
                    case "play":    ps.Play();  break;
                    case "stop":    ps.Stop();  break;
                    case "restart": ps.Stop(); ps.Play(); break;
                    case "simulate":
                        float t = float.TryParse(timeStr, out float tv) ? tv : 1f;
                        ps.Simulate(t, true, true);
                        break;
                }
            };

            return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"status\":\"queued\"}}";
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
