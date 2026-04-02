using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameStudioMCP
{
    /// <summary>
    /// Post-processing tools: manage_post_process_volume, set_bloom, set_color_grading, set_camera_effects
    /// Works with both URP (Volume) and Legacy (Post Processing Stack v2).
    /// </summary>
    public static class PostProcessTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_post_process_volume",
                ToolDef("manage_post_process_volume",
                    "Create or configure a Post-Processing Volume (URP/HDRP). Set weight, priority, and blending.",
                    Param("action",       "string", "create | configure | delete | list"),
                    Param("name",         "string", "Volume GameObject name"),
                    Param("is_global",    "string", "Optional: true|false — global volume affects whole scene"),
                    Param("weight",       "string", "Optional: blend weight 0-1"),
                    Param("priority",     "string", "Optional: priority integer, higher wins"),
                    Param("profile_path", "string", "Optional: asset path to Volume Profile asset"),
                    Param("layer",        "string", "Optional: layer name for volume culling mask")),
                ManagePostProcessVolume);

            MCPToolRegistry.Register("set_bloom",
                ToolDef("set_bloom",
                    "Configure Bloom post-processing effect on a Volume. Enable/disable, set threshold, intensity, scatter.",
                    Param("volume",      "string", "Volume GameObject name"),
                    Param("enabled",     "string", "true|false"),
                    Param("threshold",   "string", "Optional: bloom threshold e.g. 0.9"),
                    Param("intensity",   "string", "Optional: bloom intensity e.g. 1.0"),
                    Param("scatter",     "string", "Optional: scatter 0-1 (URP)"),
                    Param("color",       "string", "Optional: bloom tint color hex e.g. #FFFFFF"),
                    Param("high_quality","string", "Optional: true|false — high quality mode")),
                SetBloom);

            MCPToolRegistry.Register("set_color_grading",
                ToolDef("set_color_grading",
                    "Configure Color Grading / Tonemapping on a Volume. Adjust exposure, contrast, saturation, LUT.",
                    Param("volume",       "string", "Volume GameObject name"),
                    Param("enabled",      "string", "true|false"),
                    Param("mode",         "string", "Optional: None|Neutral|ACES|Custom (tonemapping mode)"),
                    Param("exposure",     "string", "Optional: post-exposure value e.g. 0"),
                    Param("contrast",     "string", "Optional: contrast -100 to 100"),
                    Param("saturation",   "string", "Optional: saturation -100 to 100"),
                    Param("temperature",  "string", "Optional: white balance temperature -100 to 100"),
                    Param("lut_path",     "string", "Optional: asset path to LUT texture")),
                SetColorGrading);

            MCPToolRegistry.Register("set_camera_effects",
                ToolDef("set_camera_effects",
                    "Configure Depth of Field, Vignette, and Chromatic Aberration on a post-process Volume.",
                    Param("volume",            "string", "Volume GameObject name"),
                    Param("dof_enabled",       "string", "Optional: true|false — depth of field"),
                    Param("dof_focus_distance","string", "Optional: DOF focus distance e.g. 10"),
                    Param("dof_aperture",      "string", "Optional: DOF aperture f-stop e.g. 5.6"),
                    Param("vignette_enabled",  "string", "Optional: true|false — vignette"),
                    Param("vignette_intensity","string", "Optional: vignette intensity 0-1"),
                    Param("vignette_color",    "string", "Optional: vignette color hex e.g. #000000"),
                    Param("ca_enabled",        "string", "Optional: true|false — chromatic aberration"),
                    Param("ca_intensity",      "string", "Optional: chromatic aberration intensity 0-1")),
                SetCameraEffects);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManagePostProcessVolume(string args)
        {
            string action      = ParseArg(args, "action")       ?? "create";
            string name        = ParseArg(args, "name")         ?? "PostProcessVolume";
            string isGlobal    = ParseArg(args, "is_global");
            string weightStr   = ParseArg(args, "weight");
            string priorityStr = ParseArg(args, "priority");
            string profilePath = ParseArg(args, "profile_path");
            string layerStr    = ParseArg(args, "layer");

            if (action == "list")
            {
#if UNITY_URP
                var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude);
                var sb = new System.Text.StringBuilder("[");
                for (int i = 0; i < volumes.Length; i++)
                {
                    sb.Append($"{{\"name\":\"{volumes[i].gameObject.name}\",\"isGlobal\":{volumes[i].isGlobal.ToString().ToLower()},\"weight\":{volumes[i].weight},\"priority\":{volumes[i].priority}}}");
                    if (i < volumes.Length - 1) sb.Append(",");
                }
                sb.Append("]");
                return $"{{\"volumes\":{sb}}}";
#else
                return "{\"error\":\"URP package required for Volume support\"}";
#endif
            }

            EditorApplication.delayCall += () =>
            {
                if (action == "delete")
                {
                    var ex = GameObject.Find(name);
                    if (ex != null) Undo.DestroyObjectImmediate(ex);
                    return;
                }

#if UNITY_URP
                var go     = GameObject.Find(name) ?? new GameObject(name);
                go.name    = name;
                var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();

                if (!string.IsNullOrEmpty(isGlobal))    volume.isGlobal = isGlobal.ToLower() == "true";
                if (!string.IsNullOrEmpty(weightStr)   && float.TryParse(weightStr,   out float w))  volume.weight   = w;
                if (!string.IsNullOrEmpty(priorityStr) && float.TryParse(priorityStr, out float pr)) volume.priority = pr;

                if (!string.IsNullOrEmpty(profilePath))
                {
                    var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                    if (profile != null) volume.profile = profile;
                    else Debug.LogWarning($"[GameStudioMCP] VolumeProfile not found: {profilePath}");
                }
                else if (volume.profile == null)
                {
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    string dir  = $"Assets/Settings/PostProcess";
                    System.IO.Directory.CreateDirectory(dir);
                    AssetDatabase.CreateAsset(profile, $"{dir}/{name}_Profile.asset");
                    volume.profile = profile;
                }

                if (!string.IsNullOrEmpty(layerStr))
                {
                    int layerIdx = LayerMask.NameToLayer(layerStr);
                    if (layerIdx >= 0) go.layer = layerIdx;
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create PostProcessVolume {name}");
                EditorUtility.SetDirty(go);
                AssetDatabase.SaveAssets();
#else
                Debug.LogWarning("[PostProcessTools] URP package required for Volume support");
#endif
            };

            return $"{{\"action\":\"{action}\",\"name\":\"{name}\",\"status\":\"queued\"}}";
        }

        private static string SetBloom(string args)
        {
            string volumeName  = ParseArg(args, "volume");
            string enabled     = ParseArg(args, "enabled");
            string thresholdStr= ParseArg(args, "threshold");
            string intensityStr= ParseArg(args, "intensity");
            string scatterStr  = ParseArg(args, "scatter");
            string colorStr    = ParseArg(args, "color");

            if (string.IsNullOrEmpty(volumeName)) return Error("volume is required");

            EditorApplication.delayCall += () =>
            {
#if UNITY_URP
                var go = GameObject.Find(volumeName);
                var vol = go?.GetComponent<Volume>();
                if (vol == null || vol.profile == null)
                {
                    Debug.LogWarning($"[GameStudioMCP] set_bloom: Volume or profile not found on '{volumeName}'");
                    return;
                }

                if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
                    bloom = vol.profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);

                if (!string.IsNullOrEmpty(enabled))     bloom.active = enabled.ToLower() == "true";
                if (!string.IsNullOrEmpty(thresholdStr) && float.TryParse(thresholdStr, out float thr)) { bloom.threshold.Override(thr); }
                if (!string.IsNullOrEmpty(intensityStr) && float.TryParse(intensityStr, out float itv)) { bloom.intensity.Override(itv); }
                if (!string.IsNullOrEmpty(scatterStr)   && float.TryParse(scatterStr,   out float sct)) { bloom.scatter.Override(sct); }
                if (!string.IsNullOrEmpty(colorStr) && ColorUtility.TryParseHtmlString(colorStr.StartsWith("#") ? colorStr : "#" + colorStr, out Color c))
                    bloom.tint.Override(c);

                EditorUtility.SetDirty(vol.profile);
                AssetDatabase.SaveAssets();
#else
                Debug.LogWarning("[PostProcessTools] Bloom requires Unity URP package");
#endif
            };

            return $"{{\"action\":\"set_bloom\",\"volume\":\"{volumeName}\",\"status\":\"queued\"}}";
        }

        private static string SetColorGrading(string args)
        {
            string volumeName   = ParseArg(args, "volume");
            string enabled      = ParseArg(args, "enabled");
            string mode         = ParseArg(args, "mode");
            string exposureStr  = ParseArg(args, "exposure");
            string contrastStr  = ParseArg(args, "contrast");
            string saturationStr= ParseArg(args, "saturation");
            string tempStr      = ParseArg(args, "temperature");

            if (string.IsNullOrEmpty(volumeName)) return Error("volume is required");

            EditorApplication.delayCall += () =>
            {
#if UNITY_URP
                var go  = GameObject.Find(volumeName);
                var vol = go?.GetComponent<Volume>();
                if (vol == null || vol.profile == null)
                {
                    Debug.LogWarning($"[GameStudioMCP] set_color_grading: Volume not found on '{volumeName}'");
                    return;
                }

                // URP uses ColorAdjustments + Tonemapping separately
                if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var ca))
                    ca = vol.profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);

                if (!string.IsNullOrEmpty(enabled))       ca.active = enabled.ToLower() == "true";
                if (!string.IsNullOrEmpty(exposureStr)  && float.TryParse(exposureStr,   out float exp)) ca.postExposure.Override(exp);
                if (!string.IsNullOrEmpty(contrastStr)  && float.TryParse(contrastStr,   out float con)) ca.contrast.Override(con);
                if (!string.IsNullOrEmpty(saturationStr)&& float.TryParse(saturationStr, out float sat)) ca.saturation.Override(sat);
                if (!string.IsNullOrEmpty(tempStr)      && float.TryParse(tempStr,       out float tmp))
                {
                    if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.WhiteBalance>(out var wb))
                        wb = vol.profile.Add<UnityEngine.Rendering.Universal.WhiteBalance>(true);
                    wb.temperature.Override(tmp);
                }

                if (!string.IsNullOrEmpty(mode))
                {
                    if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.Tonemapping>(out var tm))
                        tm = vol.profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);

                    switch (mode.ToUpper())
                    {
                        case "NONE":    tm.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.None);   break;
                        case "NEUTRAL": tm.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);break;
                        case "ACES":    tm.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);   break;
                    }
                }

                EditorUtility.SetDirty(vol.profile);
                AssetDatabase.SaveAssets();
#else
                Debug.LogWarning("[PostProcessTools] Color Grading requires Unity URP package");
#endif
            };

            return $"{{\"action\":\"set_color_grading\",\"volume\":\"{volumeName}\",\"status\":\"queued\"}}";
        }

        private static string SetCameraEffects(string args)
        {
            string volumeName     = ParseArg(args, "volume");
            string dofEnabled     = ParseArg(args, "dof_enabled");
            string dofFocusDist   = ParseArg(args, "dof_focus_distance");
            string dofAperture    = ParseArg(args, "dof_aperture");
            string vigEnabled     = ParseArg(args, "vignette_enabled");
            string vigIntensity   = ParseArg(args, "vignette_intensity");
            string vigColor       = ParseArg(args, "vignette_color");
            string caEnabled      = ParseArg(args, "ca_enabled");
            string caIntensity    = ParseArg(args, "ca_intensity");

            if (string.IsNullOrEmpty(volumeName)) return Error("volume is required");

            EditorApplication.delayCall += () =>
            {
#if UNITY_URP
                var go  = GameObject.Find(volumeName);
                var vol = go?.GetComponent<Volume>();
                if (vol == null || vol.profile == null)
                {
                    Debug.LogWarning($"[GameStudioMCP] set_camera_effects: Volume not found on '{volumeName}'");
                    return;
                }

                // Depth of Field
                if (!string.IsNullOrEmpty(dofEnabled))
                {
                    if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var dof))
                        dof = vol.profile.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
                    dof.active = dofEnabled.ToLower() == "true";
                    if (!string.IsNullOrEmpty(dofFocusDist) && float.TryParse(dofFocusDist, out float fd)) dof.focusDistance.Override(fd);
                    if (!string.IsNullOrEmpty(dofAperture)  && float.TryParse(dofAperture,  out float ap)) dof.aperture.Override(ap);
                }

                // Vignette
                if (!string.IsNullOrEmpty(vigEnabled))
                {
                    if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vig))
                        vig = vol.profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
                    vig.active = vigEnabled.ToLower() == "true";
                    if (!string.IsNullOrEmpty(vigIntensity) && float.TryParse(vigIntensity, out float vi)) vig.intensity.Override(vi);
                    if (!string.IsNullOrEmpty(vigColor) && ColorUtility.TryParseHtmlString(vigColor.StartsWith("#") ? vigColor : "#" + vigColor, out Color vc))
                        vig.color.Override(vc);
                }

                // Chromatic Aberration
                if (!string.IsNullOrEmpty(caEnabled))
                {
                    if (!vol.profile.TryGet<UnityEngine.Rendering.Universal.ChromaticAberration>(out var chrom))
                        chrom = vol.profile.Add<UnityEngine.Rendering.Universal.ChromaticAberration>(true);
                    chrom.active = caEnabled.ToLower() == "true";
                    if (!string.IsNullOrEmpty(caIntensity) && float.TryParse(caIntensity, out float ci)) chrom.intensity.Override(ci);
                }

                EditorUtility.SetDirty(vol.profile);
                AssetDatabase.SaveAssets();
#else
                Debug.LogWarning("[PostProcessTools] Camera Effects require Unity URP package");
#endif
            };

            return $"{{\"action\":\"set_camera_effects\",\"volume\":\"{volumeName}\",\"status\":\"queued\"}}";
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
