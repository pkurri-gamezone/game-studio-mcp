using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace GameStudioMCP
{
    /// <summary>
    /// Audio tools: manage_audio_source, manage_audio_mixer, manage_audio_clip, play_audio
    /// </summary>
    public static class AudioTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_audio_source",
                ToolDef("manage_audio_source",
                    "Add or configure an AudioSource on a GameObject. Set clip, volume, pitch, loop, spatial blend.",
                    Param("action",        "string", "add | configure | remove | get"),
                    Param("gameobject",    "string", "Target GameObject name"),
                    Param("clip_path",     "string", "Optional: asset path to AudioClip e.g. Assets/Audio/SFX/jump.wav"),
                    Param("volume",        "string", "Optional: volume 0-1"),
                    Param("pitch",         "string", "Optional: pitch e.g. 1.0"),
                    Param("loop",          "string", "Optional: true|false"),
                    Param("play_on_awake", "string", "Optional: true|false"),
                    Param("spatial_blend", "string", "Optional: 0=2D, 1=3D, 0.5=mixed"),
                    Param("mixer_group",   "string", "Optional: AudioMixer group path e.g. 'Master/SFX'")),
                ManageAudioSource);

            MCPToolRegistry.Register("manage_audio_mixer",
                ToolDef("manage_audio_mixer",
                    "Create an AudioMixer asset or configure mixer group volumes and effects.",
                    Param("action",      "string", "create | set_volume | list_groups | expose_parameter"),
                    Param("name",        "string", "AudioMixer name or asset path"),
                    Param("group",       "string", "Optional: group name e.g. 'SFX' or 'Music'"),
                    Param("volume",      "string", "Optional: volume in dB e.g. -6"),
                    Param("parameter",   "string", "Optional: exposed parameter name to set/expose")),
                ManageAudioMixer);

            MCPToolRegistry.Register("manage_audio_clip",
                ToolDef("manage_audio_clip",
                    "Configure AudioClip import settings: compression, load type, quality.",
                    Param("path",         "string", "Asset path to AudioClip e.g. Assets/Audio/SFX/jump.wav"),
                    Param("load_type",    "string", "Optional: DecompressOnLoad | CompressedInMemory | Streaming"),
                    Param("compression",  "string", "Optional: PCM | Vorbis | ADPCM"),
                    Param("quality",      "string", "Optional: compression quality 0-100 (Vorbis only)"),
                    Param("force_mono",   "string", "Optional: true|false"),
                    Param("normalize",    "string", "Optional: true|false — normalize sample data")),
                ManageAudioClip);

            MCPToolRegistry.Register("play_audio",
                ToolDef("play_audio",
                    "Play or stop an AudioClip preview in the Editor without entering play mode.",
                    Param("action",    "string", "play | stop | stop_all"),
                    Param("clip_path", "string", "Optional: asset path to AudioClip for play action")),
                PlayAudio);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageAudioSource(string args)
        {
            string action      = ParseArg(args, "action")        ?? "add";
            string goName      = ParseArg(args, "gameobject");
            string clipPath    = ParseArg(args, "clip_path");
            string volumeStr   = ParseArg(args, "volume");
            string pitchStr    = ParseArg(args, "pitch");
            string loopStr     = ParseArg(args, "loop");
            string playOnAwake = ParseArg(args, "play_on_awake");
            string spatialStr  = ParseArg(args, "spatial_blend");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                if (go == null) { Debug.LogWarning($"[GameStudioMCP] manage_audio_source: '{goName}' not found"); return; }

                if (action == "remove")
                {
                    var src = go.GetComponent<AudioSource>();
                    if (src != null) UnityEngine.Object.DestroyImmediate(src);
                    return;
                }

                var source = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();

                if (!string.IsNullOrEmpty(clipPath))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                    if (clip != null) source.clip = clip;
                    else Debug.LogWarning($"[GameStudioMCP] AudioClip not found: {clipPath}");
                }

                if (!string.IsNullOrEmpty(volumeStr)   && float.TryParse(volumeStr,   out float vol))  source.volume = vol;
                if (!string.IsNullOrEmpty(pitchStr)    && float.TryParse(pitchStr,    out float pit))  source.pitch  = pit;
                if (!string.IsNullOrEmpty(spatialStr)  && float.TryParse(spatialStr,  out float spat)) source.spatialBlend = spat;
                if (!string.IsNullOrEmpty(loopStr))      source.loop        = loopStr.ToLower()      == "true";
                if (!string.IsNullOrEmpty(playOnAwake))  source.playOnAwake = playOnAwake.ToLower()  == "true";

                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"status\":\"queued\"}}";
        }

        private static string ManageAudioMixer(string args)
        {
            string action  = ParseArg(args, "action") ?? "create";
            string name    = ParseArg(args, "name")   ?? "GameAudioMixer";
            string group   = ParseArg(args, "group");
            string volume  = ParseArg(args, "volume");

            string assetPath = name.StartsWith("Assets/") ? name : $"Assets/Audio/{name}.mixer";

            if (action == "create")
            {
                EditorApplication.delayCall += () =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                    if (!File.Exists(assetPath))
                    {
                        Debug.Log($"[GameStudioMCP] AudioMixer creation requires Unity's Audio Mixer window. Create via: Window > Audio > Audio Mixer, then save to {assetPath}");
                    }
                };
                return $"{{\"action\":\"create\",\"note\":\"Open Window > Audio > Audio Mixer in Unity to create. Save to: {assetPath}\",\"asset\":\"{assetPath}\"}}";
            }

            if (action == "set_volume" && !string.IsNullOrEmpty(group) && !string.IsNullOrEmpty(volume))
            {
                EditorApplication.delayCall += () =>
                {
                    var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(assetPath);
                    if (mixer == null) { Debug.LogWarning($"[GameStudioMCP] AudioMixer not found: {assetPath}"); return; }
                    if (float.TryParse(volume, out float dB))
                        mixer.SetFloat(group + "Volume", dB);
                };
                return $"{{\"action\":\"set_volume\",\"group\":\"{group}\",\"volume\":\"{volume}\",\"status\":\"queued\"}}";
            }

            if (action == "list_groups")
            {
                EditorApplication.delayCall += () =>
                {
                    var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(assetPath);
                    if (mixer == null) Debug.LogWarning($"[GameStudioMCP] AudioMixer not found: {assetPath}");
                };
                return $"{{\"action\":\"list_groups\",\"note\":\"AudioMixer group listing requires inspector access\",\"mixer\":\"{assetPath}\"}}";
            }

            return $"{{\"action\":\"{action}\",\"name\":\"{name}\",\"status\":\"queued\"}}";
        }

        private static string ManageAudioClip(string args)
        {
            string path        = ParseArg(args, "path");
            string loadTypeStr = ParseArg(args, "load_type");
            string compressStr = ParseArg(args, "compression");
            string qualityStr  = ParseArg(args, "quality");
            string forceMono   = ParseArg(args, "force_mono");

            if (string.IsNullOrEmpty(path)) return Error("path is required");

            EditorApplication.delayCall += () =>
            {
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) { Debug.LogWarning($"[GameStudioMCP] No AudioImporter for: {path}"); return; }

                var settings = importer.defaultSampleSettings;

                if (!string.IsNullOrEmpty(loadTypeStr) && System.Enum.TryParse<AudioClipLoadType>(loadTypeStr, out var lt))
                    settings.loadType = lt;

                if (!string.IsNullOrEmpty(compressStr))
                {
                    switch (compressStr.ToUpper())
                    {
                        case "PCM":    settings.compressionFormat = AudioCompressionFormat.PCM;   break;
                        case "VORBIS": settings.compressionFormat = AudioCompressionFormat.Vorbis; break;
                        case "ADPCM":  settings.compressionFormat = AudioCompressionFormat.ADPCM; break;
                    }
                }

                if (!string.IsNullOrEmpty(qualityStr) && float.TryParse(qualityStr, out float q))
                    settings.quality = q / 100f;

                if (!string.IsNullOrEmpty(forceMono)) importer.forceToMono = forceMono.ToLower() == "true";

                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            };

            return $"{{\"action\":\"configure\",\"path\":\"{path}\",\"status\":\"queued\"}}";
        }

        private static string PlayAudio(string args)
        {
            string action   = ParseArg(args, "action")    ?? "play";
            string clipPath = ParseArg(args, "clip_path");

            EditorApplication.delayCall += () =>
            {
                if (action == "stop_all")
                {
                    // Use reflection to access Unity's internal AudioUtil.StopAllClips
                    var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                    audioUtil?.GetMethod("StopAllClips")?.Invoke(null, null);
                    return;
                }

                if (string.IsNullOrEmpty(clipPath)) { Debug.LogWarning("[GameStudioMCP] play_audio: clip_path required"); return; }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null) { Debug.LogWarning($"[GameStudioMCP] play_audio: clip not found {clipPath}"); return; }

                var audioUtil2 = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                if (action == "stop")
                    audioUtil2?.GetMethod("StopClip")?.Invoke(null, new object[] { clip });
                else
                    audioUtil2?.GetMethod("PlayPreviewClip")?.Invoke(null, new object[] { clip, 0, false });
            };

            return $"{{\"action\":\"{action}\",\"clip\":\"{clipPath}\",\"status\":\"queued\"}}";
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
