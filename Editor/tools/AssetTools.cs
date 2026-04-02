using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace GameStudioMCP
{
    /// <summary>
    /// Asset-level tools matching / extending unity-mcp parity:
    /// manage_material, manage_prefabs, manage_ui, manage_animation,
    /// manage_camera, manage_texture, manage_scriptable_object
    /// </summary>
    public static class AssetTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_material",
                ToolDef("manage_material",
                    "Create, modify, or apply a material. Set color, shader, texture.",
                    Param("action",   "string", "create | set_color | set_shader | apply | get_info"),
                    Param("name",     "string", "Material name or asset path"),
                    Param("shader",   "string", "Optional: shader name e.g. 'Standard' or 'Unlit/Color'"),
                    Param("color",    "string", "Optional: hex color e.g. #FF0000 or RGBA e.g. 1,0,0,1"),
                    Param("gameobject","string","Optional: GameObject name to apply material to")),
                ManageMaterial);

            MCPToolRegistry.Register("manage_prefabs",
                ToolDef("manage_prefabs",
                    "Create prefabs, instantiate, unpack, or get prefab info",
                    Param("action",     "string", "create | instantiate | unpack | get_info | list"),
                    Param("gameobject", "string", "Optional: source GameObject name for create"),
                    Param("path",       "string", "Optional: prefab asset path e.g. Assets/Prefabs/Player.prefab"),
                    Param("name",       "string", "Optional: name for the new prefab or instance"),
                    Param("position",   "string", "Optional: position as 'x,y,z' for instantiate")),
                ManagePrefabs);

            MCPToolRegistry.Register("manage_ui",
                ToolDef("manage_ui",
                    "Create or modify Unity UI elements: Canvas, Text, Button, Image, Panel",
                    Param("action",  "string", "create_canvas | create_button | create_text | create_image | create_panel | set_text | set_color"),
                    Param("name",    "string", "Name for the new UI element"),
                    Param("parent",  "string", "Optional: parent GameObject name (defaults to Canvas)"),
                    Param("text",    "string", "Optional: text content for Text/Button"),
                    Param("color",   "string", "Optional: hex color e.g. #FFFFFF"),
                    Param("size",    "string", "Optional: size as 'width,height' e.g. '200,50'")),
                ManageUI);

            MCPToolRegistry.Register("manage_animation",
                ToolDef("manage_animation",
                    "Create animation clips, controllers, or set animation parameters",
                    Param("action",    "string", "create_clip | create_controller | set_trigger | set_bool | set_float | list"),
                    Param("gameobject","string", "Optional: target GameObject with Animator"),
                    Param("name",      "string", "Animation clip or controller name"),
                    Param("parameter", "string", "Optional: animator parameter name"),
                    Param("value",     "string", "Optional: parameter value")),
                ManageAnimation);

            MCPToolRegistry.Register("manage_camera",
                ToolDef("manage_camera",
                    "Get or set camera properties: FOV, near/far clip, background color, orthographic",
                    Param("action",    "string", "get | set | create"),
                    Param("gameobject","string", "Optional: camera GameObject name (defaults to main camera)"),
                    Param("fov",       "string", "Optional: field of view"),
                    Param("near_clip", "string", "Optional: near clip plane"),
                    Param("far_clip",  "string", "Optional: far clip plane"),
                    Param("orthographic","string","Optional: true/false for orthographic mode"),
                    Param("bg_color",  "string", "Optional: background hex color")),
                ManageCamera);

            MCPToolRegistry.Register("manage_texture",
                ToolDef("manage_texture",
                    "Get texture info or change import settings",
                    Param("action",  "string", "get_info | set_compression | set_max_size | reimport"),
                    Param("path",    "string", "Asset path to texture e.g. Assets/Textures/hero.png"),
                    Param("max_size","string", "Optional: max texture size e.g. 512, 1024, 2048"),
                    Param("compression","string","Optional: none | low | normal | high")),
                ManageTexture);

            MCPToolRegistry.Register("manage_scriptable_object",
                ToolDef("manage_scriptable_object",
                    "Create or read a ScriptableObject asset from a MonoScript class",
                    Param("action",     "string", "create | get_info | list"),
                    Param("class_name", "string", "ScriptableObject subclass name"),
                    Param("path",       "string", "Optional: asset path e.g. Assets/Data/MyConfig.asset"),
                    Param("search_dir", "string", "Optional: directory to list ScriptableObjects from")),
                ManageScriptableObject);
        }

        // ── Implementations ───────────────────────────────────────────────────

        private static string ManageMaterial(string args)
        {
            string action   = ParseArg(args, "action")     ?? "create";
            string name     = ParseArg(args, "name")       ?? "New Material";
            string shader   = ParseArg(args, "shader")     ?? "Standard";
            string colorStr = ParseArg(args, "color");
            string goName   = ParseArg(args, "gameobject");

            switch (action.ToLower())
            {
                case "create":
                    EditorApplication.delayCall += () =>
                    {
                        var sh  = Shader.Find(shader) ?? Shader.Find("Standard");
                        var mat = new Material(sh) { name = name };
                        if (!string.IsNullOrEmpty(colorStr) && TryParseColor(colorStr, out Color c))
                            mat.color = c;
                        string assetPath = $"Assets/Materials/{name}.mat";
                        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "Assets/Materials"));
                        AssetDatabase.CreateAsset(mat, assetPath);
                        AssetDatabase.SaveAssets();
                        if (!string.IsNullOrEmpty(goName))
                        {
                            var go = GameObject.Find(goName);
                            var r  = go?.GetComponent<Renderer>();
                            if (r != null) r.sharedMaterial = mat;
                        }
                    };
                    return $"{{\"action\":\"create\",\"name\":\"{name}\",\"shader\":\"{shader}\",\"path\":\"Assets/Materials/{name}.mat\"}}";

                case "set_color":
                    EditorApplication.delayCall += () =>
                    {
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(name.Contains("/") ? name : $"Assets/Materials/{name}.mat");
                        if (mat != null && TryParseColor(colorStr, out Color c)) { mat.color = c; EditorUtility.SetDirty(mat); }
                    };
                    return $"{{\"action\":\"set_color\",\"material\":\"{name}\",\"color\":\"{colorStr}\"}}";

                case "apply":
                    if (string.IsNullOrEmpty(goName)) return Error("gameobject required for apply");
                    EditorApplication.delayCall += () =>
                    {
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(name.Contains("/") ? name : $"Assets/Materials/{name}.mat");
                        var go  = GameObject.Find(goName);
                        var r   = go?.GetComponent<Renderer>();
                        if (r != null && mat != null) { Undo.RecordObject(r, "Apply Material"); r.sharedMaterial = mat; }
                    };
                    return $"{{\"action\":\"apply\",\"material\":\"{name}\",\"gameobject\":\"{goName}\"}}";

                case "get_info":
                    var m2 = AssetDatabase.LoadAssetAtPath<Material>(name.Contains("/") ? name : $"Assets/Materials/{name}.mat");
                    if (m2 == null) return Error($"Material not found: {name}");
                    return $"{{\"name\":\"{m2.name}\",\"shader\":\"{m2.shader.name}\",\"color\":\"#{ColorUtility.ToHtmlStringRGB(m2.color)}\"}}";

                default:
                    return Error($"Unknown action: {action}");
            }
        }

        private static string ManagePrefabs(string args)
        {
            string action = ParseArg(args, "action")     ?? "list";
            string goName = ParseArg(args, "gameobject");
            string path   = ParseArg(args, "path");
            string name   = ParseArg(args, "name");
            string posStr = ParseArg(args, "position");

            switch (action.ToLower())
            {
                case "create":
                    if (string.IsNullOrEmpty(goName)) return Error("gameobject required for create");
                    string savePath = path ?? $"Assets/Prefabs/{goName}.prefab";
                    EditorApplication.delayCall += () =>
                    {
                        var go = GameObject.Find(goName);
                        if (go == null) { Debug.LogError($"[GameStudioMCP] GameObject not found: {goName}"); return; }
                        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", Path.GetDirectoryName(savePath)));
                        PrefabUtility.SaveAsPrefabAssetAndConnect(go, savePath, InteractionMode.UserAction);
                    };
                    return $"{{\"action\":\"create\",\"source\":\"{goName}\",\"path\":\"{savePath}\"}}";

                case "instantiate":
                    if (string.IsNullOrEmpty(path)) return Error("path required for instantiate");
                    Vector3 pos = ParseVector3(posStr);
                    EditorApplication.delayCall += () =>
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                        {
                            var inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                            if (inst != null)
                            {
                                inst.transform.position = pos;
                                inst.name = name ?? prefab.name;
                                Undo.RegisterCreatedObjectUndo(inst, $"Instantiate {inst.name}");
                            }
                        }
                    };
                    return $"{{\"action\":\"instantiate\",\"prefab\":\"{path}\",\"position\":\"{posStr}\"}}";

                case "list":
                    string searchDir = path ?? "Assets/Prefabs";
                    string fullDir   = Path.Combine(Application.dataPath, "..", searchDir);
                    if (!Directory.Exists(fullDir)) return $"{{\"prefabs\":[],\"message\":\"Directory not found: {searchDir}\"}}";
                    string[] prefabs = Directory.GetFiles(fullDir, "*.prefab", SearchOption.AllDirectories);
                    var sb = new StringBuilder("[");
                    for (int i = 0; i < prefabs.Length; i++)
                    {
                        string rel = prefabs[i].Replace(Application.dataPath, "Assets").Replace("\\", "/");
                        if (i > 0) sb.Append(",");
                        sb.Append($"\"{rel}\"");
                    }
                    sb.Append("]");
                    return $"{{\"prefabs\":{sb},\"count\":{prefabs.Length}}}";

                case "get_info":
                    if (string.IsNullOrEmpty(path)) return Error("path required");
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (pf == null) return Error($"Prefab not found: {path}");
                    var pfComps = pf.GetComponents<Component>();
                    var cbuild = new StringBuilder("[");
                    for (int i = 0; i < pfComps.Length; i++) { if (i > 0) cbuild.Append(","); cbuild.Append($"\"{pfComps[i].GetType().Name}\""); }
                    cbuild.Append("]");
                    return $"{{\"name\":\"{pf.name}\",\"path\":\"{path}\",\"components\":{cbuild}}}";

                default:
                    return Error($"Unknown action: {action}. Use: create | instantiate | list | get_info");
            }
        }

        private static string ManageUI(string args)
        {
            string action  = ParseArg(args, "action") ?? "create_canvas";
            string uiName  = ParseArg(args, "name")   ?? "UIElement";
            string parent  = ParseArg(args, "parent");
            string text    = ParseArg(args, "text");
            string colorStr= ParseArg(args, "color");
            string sizeStr = ParseArg(args, "size");

            EditorApplication.delayCall += () =>
            {
                Canvas canvas = null;
                GameObject parentGO = null;

                if (!string.IsNullOrEmpty(parent))
                    parentGO = GameObject.Find(parent);

                if (parentGO == null)
                {
                    canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                    if (canvas == null)
                    {
                        var canvasGO = new GameObject("Canvas");
                        canvas = canvasGO.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasGO.AddComponent<CanvasScaler>();
                        canvasGO.AddComponent<GraphicRaycaster>();
                        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
                    }
                    parentGO = canvas.gameObject;
                }

                GameObject created = null;
                switch (action.ToLower())
                {
                    case "create_canvas":
                        var cgo = new GameObject(uiName);
                        var c = cgo.AddComponent<Canvas>();
                        c.renderMode = RenderMode.ScreenSpaceOverlay;
                        cgo.AddComponent<CanvasScaler>();
                        cgo.AddComponent<GraphicRaycaster>();
                        created = cgo;
                        break;
                    case "create_text":
                        created = new GameObject(uiName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                        created.transform.SetParent(parentGO.transform, false);
                        var t = created.GetComponent<Text>();
                        t.text     = text ?? uiName;
                        t.fontSize = 24;
                        t.color    = Color.white;
                        if (!string.IsNullOrEmpty(colorStr) && TryParseColor(colorStr, out Color tc)) t.color = tc;
                        ApplySize(created, sizeStr ?? "160,30");
                        break;
                    case "create_button":
                        created = new GameObject(uiName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                        created.transform.SetParent(parentGO.transform, false);
                        if (!string.IsNullOrEmpty(colorStr) && TryParseColor(colorStr, out Color bc)) created.GetComponent<Image>().color = bc;
                        ApplySize(created, sizeStr ?? "160,40");
                        var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                        label.transform.SetParent(created.transform, false);
                        label.GetComponent<Text>().text = text ?? uiName;
                        label.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
                        break;
                    case "create_image":
                        created = new GameObject(uiName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        created.transform.SetParent(parentGO.transform, false);
                        if (!string.IsNullOrEmpty(colorStr) && TryParseColor(colorStr, out Color ic)) created.GetComponent<Image>().color = ic;
                        ApplySize(created, sizeStr ?? "100,100");
                        break;
                    case "create_panel":
                        created = new GameObject(uiName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        created.transform.SetParent(parentGO.transform, false);
                        var panelImg = created.GetComponent<Image>();
                        panelImg.color = new Color(0, 0, 0, 0.8f);
                        var rt = created.GetComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                        rt.sizeDelta = Vector2.zero;
                        break;
                }
                if (created != null) Undo.RegisterCreatedObjectUndo(created, $"Create UI {uiName}");
            };

            return $"{{\"action\":\"{action}\",\"name\":\"{uiName}\",\"parent\":\"{parent ?? "Canvas"}\",\"message\":\"UI element creation queued\"}}";
        }

        private static string ManageAnimation(string args)
        {
            string action    = ParseArg(args, "action")     ?? "list";
            string goName    = ParseArg(args, "gameobject");
            string animName  = ParseArg(args, "name")       ?? "NewAnimation";
            string parameter = ParseArg(args, "parameter");
            string value     = ParseArg(args, "value");

            switch (action.ToLower())
            {
                case "create_clip":
                    EditorApplication.delayCall += () =>
                    {
                        var clip = new AnimationClip { name = animName };
                        string clipPath = $"Assets/Animations/{animName}.anim";
                        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", "Assets/Animations"));
                        AssetDatabase.CreateAsset(clip, clipPath);
                        AssetDatabase.SaveAssets();
                    };
                    return $"{{\"action\":\"create_clip\",\"name\":\"{animName}\",\"path\":\"Assets/Animations/{animName}.anim\"}}";

                case "create_controller":
                    EditorApplication.delayCall += () =>
                    {
                        var ctrl = AnimatorController.CreateAnimatorControllerAtPath($"Assets/Animations/{animName}.controller");
                        AssetDatabase.SaveAssets();
                    };
                    return $"{{\"action\":\"create_controller\",\"name\":\"{animName}\",\"path\":\"Assets/Animations/{animName}.controller\"}}";

                case "set_trigger":
                case "set_bool":
                case "set_float":
                    if (string.IsNullOrEmpty(goName)) return Error("gameobject required");
                    if (string.IsNullOrEmpty(parameter)) return Error("parameter required");
                    EditorApplication.delayCall += () =>
                    {
                        if (!EditorApplication.isPlaying) { Debug.LogWarning("[GameStudioMCP] Animator params only work in play mode"); return; }
                        var go = GameObject.Find(goName);
                        var anim = go?.GetComponent<Animator>();
                        if (anim == null) return;
                        if (action == "set_trigger") anim.SetTrigger(parameter);
                        else if (action == "set_bool") anim.SetBool(parameter, value?.ToLower() == "true");
                        else if (action == "set_float" && float.TryParse(value, out float f)) anim.SetFloat(parameter, f);
                    };
                    return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"parameter\":\"{parameter}\",\"value\":\"{value}\"}}";

                case "list":
                    string animDir = Path.Combine(Application.dataPath, "..", "Assets/Animations");
                    if (!Directory.Exists(animDir)) return "{\"clips\":[],\"controllers\":[]}";
                    string[] clips       = Directory.GetFiles(animDir, "*.anim",       SearchOption.AllDirectories);
                    string[] controllers = Directory.GetFiles(animDir, "*.controller", SearchOption.AllDirectories);
                    return $"{{\"clips\":{ToJsonArray(clips)},\"controllers\":{ToJsonArray(controllers)}}}";

                default:
                    return Error($"Unknown action: {action}");
            }
        }

        private static string ManageCamera(string args)
        {
            string action    = ParseArg(args, "action")      ?? "get";
            string goName    = ParseArg(args, "gameobject");
            string fovStr    = ParseArg(args, "fov");
            string nearStr   = ParseArg(args, "near_clip");
            string farStr    = ParseArg(args, "far_clip");
            string orthoStr  = ParseArg(args, "orthographic");
            string bgColorStr= ParseArg(args, "bg_color");

            Camera cam = string.IsNullOrEmpty(goName)
                ? Camera.main
                : GameObject.Find(goName)?.GetComponent<Camera>();

            if (action.ToLower() == "create")
            {
                EditorApplication.delayCall += () =>
                {
                    var go  = new GameObject(goName ?? "New Camera");
                    var c   = go.AddComponent<Camera>();
                    go.AddComponent<AudioListener>();
                    Undo.RegisterCreatedObjectUndo(go, "Create Camera");
                };
                return $"{{\"action\":\"create\",\"name\":\"{goName ?? "New Camera"}\"}}";
            }

            if (cam == null) return Error("No camera found. Specify gameobject or ensure Main Camera exists.");

            if (action.ToLower() == "get")
            {
                return $@"{{
  ""name"":""{cam.name}"",
  ""fov"":{cam.fieldOfView},
  ""near_clip"":{cam.nearClipPlane},
  ""far_clip"":{cam.farClipPlane},
  ""orthographic"":{cam.orthographic.ToString().ToLower()},
  ""bg_color"":""#{ColorUtility.ToHtmlStringRGB(cam.backgroundColor)}"",
  ""depth"":{cam.depth}
}}";
            }

            if (action.ToLower() == "set")
            {
                EditorApplication.delayCall += () =>
                {
                    Undo.RecordObject(cam, "Modify Camera");
                    if (float.TryParse(fovStr,  out float f)) cam.fieldOfView   = f;
                    if (float.TryParse(nearStr, out float n)) cam.nearClipPlane = n;
                    if (float.TryParse(farStr,  out float fa))cam.farClipPlane  = fa;
                    if (!string.IsNullOrEmpty(orthoStr)) cam.orthographic = orthoStr.ToLower() == "true";
                    if (!string.IsNullOrEmpty(bgColorStr) && TryParseColor(bgColorStr, out Color bg)) cam.backgroundColor = bg;
                };
                return $"{{\"action\":\"set\",\"camera\":\"{cam.name}\",\"message\":\"Camera properties updated\"}}";
            }

            return Error($"Unknown action: {action}. Use: get | set | create");
        }

        private static string ManageTexture(string args)
        {
            string action  = ParseArg(args, "action")      ?? "get_info";
            string path    = ParseArg(args, "path");
            string maxSize = ParseArg(args, "max_size");
            string compression = ParseArg(args, "compression");

            if (string.IsNullOrEmpty(path)) return Error("path is required");

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return Error($"Not a texture or not found: {path}");

            switch (action.ToLower())
            {
                case "get_info":
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    return $"{{\"path\":\"{path}\",\"width\":{tex?.width ?? 0},\"height\":{tex?.height ?? 0},\"format\":\"{importer.textureType}\",\"max_size\":{importer.maxTextureSize},\"readable\":{importer.isReadable.ToString().ToLower()}}}";

                case "set_max_size":
                    if (int.TryParse(maxSize, out int sz))
                    {
                        importer.maxTextureSize = sz;
                        AssetDatabase.ImportAsset(path);
                    }
                    return $"{{\"action\":\"set_max_size\",\"path\":\"{path}\",\"max_size\":{sz}}}";

                case "set_compression":
                    importer.textureCompression = compression?.ToLower() switch
                    {
                        "none"   => TextureImporterCompression.Uncompressed,
                        "low"    => TextureImporterCompression.CompressedLQ,
                        "high"   => TextureImporterCompression.CompressedHQ,
                        _        => TextureImporterCompression.Compressed
                    };
                    AssetDatabase.ImportAsset(path);
                    return $"{{\"action\":\"set_compression\",\"path\":\"{path}\",\"compression\":\"{compression}\"}}";

                case "reimport":
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    return $"{{\"action\":\"reimport\",\"path\":\"{path}\"}}";

                default:
                    return Error($"Unknown action: {action}");
            }
        }

        private static string ManageScriptableObject(string args)
        {
            string action    = ParseArg(args, "action")     ?? "list";
            string className = ParseArg(args, "class_name");
            string path      = ParseArg(args, "path");
            string searchDir = ParseArg(args, "search_dir") ?? "Assets/Data";

            switch (action.ToLower())
            {
                case "create":
                    if (string.IsNullOrEmpty(className)) return Error("class_name is required");
                    Type soType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        soType = asm.GetType(className);
                        if (soType != null) break;
                    }
                    if (soType == null || !soType.IsSubclassOf(typeof(ScriptableObject)))
                        return Error($"ScriptableObject subclass not found: {className}");

                    string assetPath = path ?? $"Assets/Data/{className}.asset";
                    EditorApplication.delayCall += () =>
                    {
                        var so = ScriptableObject.CreateInstance(soType);
                        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", Path.GetDirectoryName(assetPath)));
                        AssetDatabase.CreateAsset(so, assetPath);
                        AssetDatabase.SaveAssets();
                    };
                    return $"{{\"action\":\"create\",\"class\":\"{className}\",\"path\":\"{assetPath}\"}}";

                case "list":
                    string fullDir = Path.Combine(Application.dataPath, "..", searchDir);
                    if (!Directory.Exists(fullDir)) return $"{{\"assets\":[],\"message\":\"Directory not found: {searchDir}\"}}";
                    string[] assets = Directory.GetFiles(fullDir, "*.asset", SearchOption.AllDirectories);
                    return $"{{\"assets\":{ToJsonArray(assets)},\"count\":{assets.Length}}}";

                case "get_info":
                    if (string.IsNullOrEmpty(path)) return Error("path is required");
                    var loaded = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (loaded == null) return Error($"ScriptableObject not found: {path}");
                    return $"{{\"path\":\"{path}\",\"type\":\"{loaded.GetType().Name}\",\"name\":\"{loaded.name}\"}}";

                default:
                    return Error($"Unknown action: {action}. Use: create | list | get_info");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool TryParseColor(string colorStr, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(colorStr)) return false;
            if (colorStr.StartsWith("#")) return ColorUtility.TryParseHtmlString(colorStr, out color);
            var parts = colorStr.Split(',');
            if (parts.Length >= 3 &&
                float.TryParse(parts[0], out float r) &&
                float.TryParse(parts[1], out float g) &&
                float.TryParse(parts[2], out float b))
            {
                float a = parts.Length >= 4 && float.TryParse(parts[3], out float av) ? av : 1f;
                color = new Color(r, g, b, a);
                return true;
            }
            return false;
        }

        private static Vector3 ParseVector3(string s)
        {
            if (string.IsNullOrEmpty(s)) return Vector3.zero;
            var p = s.Split(',');
            return p.Length >= 3
                ? new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2]))
                : Vector3.zero;
        }

        private static void ApplySize(GameObject go, string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr)) return;
            var parts = sizeStr.Split(',');
            if (parts.Length < 2) return;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null && float.TryParse(parts[0], out float w) && float.TryParse(parts[1], out float h))
                rt.sizeDelta = new Vector2(w, h);
        }

        private static string ToJsonArray(string[] paths)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < paths.Length; i++)
            {
                string rel = paths[i].Replace(Application.dataPath, "Assets").Replace("\\", "/");
                if (i > 0) sb.Append(",");
                sb.Append($"\"{rel}\"");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string Error(string msg) => $"{{\"error\":\"{msg}\"}}";

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
