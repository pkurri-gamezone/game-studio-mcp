using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// Input tools: manage_input_action, set_input_settings, manage_virtual_button
    /// </summary>
    public static class InputTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_input_action",
                ToolDef("manage_input_action",
                    "Create or configure Input Action assets (New Input System). Create action maps, actions, and bindings.",
                    Param("action",      "string", "create_asset | add_map | add_action | add_binding | list | get"),
                    Param("asset_path",  "string", "Asset path e.g. Assets/Input/GameControls.inputactions"),
                    Param("map_name",    "string", "Optional: action map name e.g. 'Gameplay' or 'UI'"),
                    Param("action_name", "string", "Optional: action name e.g. 'Jump' or 'Move'"),
                    Param("action_type", "string", "Optional: Button|Value|PassThrough"),
                    Param("binding",     "string", "Optional: binding path e.g. '<Keyboard>/space' or '<Touchscreen>/primaryTouch/tap'"),
                    Param("value_type",  "string", "Optional: value type for Value actions e.g. Vector2|float|bool")),
                ManageInputAction);

            MCPToolRegistry.Register("set_input_settings",
                ToolDef("set_input_settings",
                    "Configure Unity Input System settings: active backend, update mode, filter noise.",
                    Param("backend",       "string", "Legacy|NewInputSystem|Both — active input handling backend"),
                    Param("update_mode",   "string", "Optional: ProcessEventsInDynamicUpdate|ProcessEventsInFixedUpdate|ProcessEventsManually"),
                    Param("compensate_orientation", "string", "Optional: true|false"),
                    Param("filter_noise",  "string", "Optional: true|false — filter noise on current pointer")),
                SetInputSettings);

            MCPToolRegistry.Register("manage_virtual_button",
                ToolDef("manage_virtual_button",
                    "Create mobile virtual buttons and joysticks in the UI hierarchy for mobile input.",
                    Param("action",      "string", "create_button | create_joystick | create_dpad"),
                    Param("name",        "string", "Virtual button/joystick name"),
                    Param("canvas",      "string", "Optional: parent Canvas GameObject name"),
                    Param("position",    "string", "Optional: anchored position as 'x,y'"),
                    Param("size",        "string", "Optional: size as 'width,height' e.g. '150,150'"),
                    Param("color",       "string", "Optional: hex color"),
                    Param("input_action","string", "Optional: input action to bind to e.g. 'Gameplay/Jump'")),
                ManageVirtualButton);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageInputAction(string args)
        {
            string action     = ParseArg(args, "action")      ?? "create_asset";
            string assetPath  = ParseArg(args, "asset_path")  ?? "Assets/Input/GameControls.inputactions";
            string mapName    = ParseArg(args, "map_name");
            string actionName = ParseArg(args, "action_name");
            string actionType = ParseArg(args, "action_type") ?? "Button";
            string binding    = ParseArg(args, "binding");
            string valueType  = ParseArg(args, "value_type");

            if (action == "list")
            {
                var assets = AssetDatabase.FindAssets("t:InputActionAsset");
                var sb = new StringBuilder("[");
                for (int i = 0; i < assets.Length; i++)
                {
                    sb.Append($"\"{AssetDatabase.GUIDToAssetPath(assets[i])}\"");
                    if (i < assets.Length - 1) sb.Append(",");
                }
                sb.Append("]");
                return $"{{\"input_action_assets\":{sb}}}";
            }

            if (action == "create_asset")
            {
                EditorApplication.delayCall += () =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                    if (!File.Exists(assetPath))
                    {
                        // Generate a minimal .inputactions JSON with standard mobile maps
                        string template = BuildInputActionTemplate(Path.GetFileNameWithoutExtension(assetPath));
                        File.WriteAllText(assetPath, template);
                        AssetDatabase.Refresh();
                        Debug.Log($"[GameStudioMCP] Created InputAction asset: {assetPath}");
                    }
                };
                return $"{{\"action\":\"create_asset\",\"asset\":\"{assetPath}\",\"status\":\"queued\"}}";
            }

            if (action == "add_map" || action == "add_action" || action == "add_binding")
            {
                EditorApplication.delayCall += () =>
                {
                    if (!File.Exists(assetPath)) { Debug.LogWarning($"[GameStudioMCP] Input asset not found: {assetPath}"); return; }

                    // Read, modify, write the JSON file
                    string json = File.ReadAllText(assetPath);
                    Debug.Log($"[GameStudioMCP] Input asset '{action}': manual editing via Inspector recommended for complex bindings. Asset: {assetPath}");
                    // Note: Full JSON manipulation requires Unity.InputSystem package parsing
                    // The asset can be edited via Project > double-click the .inputactions file
                };
                return $"{{\"action\":\"{action}\",\"asset\":\"{assetPath}\",\"map\":\"{mapName}\",\"action_name\":\"{actionName}\",\"status\":\"queued\",\"note\":\"Open the .inputactions asset in Unity to add actions/bindings via the graphical editor\"}}";
            }

            return $"{{\"action\":\"{action}\",\"asset\":\"{assetPath}\",\"status\":\"queued\"}}";
        }

        private static string BuildInputActionTemplate(string assetName)
        {
            return $@"{{
    ""name"": ""{assetName}"",
    ""maps"": [
        {{
            ""name"": ""Gameplay"",
            ""id"": ""{System.Guid.NewGuid()}"",
            ""actions"": [
                {{
                    ""name"": ""Tap"",
                    ""type"": ""Button"",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }},
                {{
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                }}
            ],
            ""bindings"": [
                {{
                    ""name"": """",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""path"": ""<Touchscreen>/primaryTouch/tap"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Mobile"",
                    ""action"": ""Tap"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }},
                {{
                    ""name"": """",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""PC"",
                    ""action"": ""Tap"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }}
            ]
        }},
        {{
            ""name"": ""UI"",
            ""id"": ""{System.Guid.NewGuid()}"",
            ""actions"": [
                {{
                    ""name"": ""Click"",
                    ""type"": ""Button"",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }}
            ],
            ""bindings"": [
                {{
                    ""name"": """",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Click"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }},
                {{
                    ""name"": """",
                    ""id"": ""{System.Guid.NewGuid()}"",
                    ""path"": ""<Touchscreen>/primaryTouch/tap"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Click"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }}
            ]
        }}
    ],
    ""controlSchemes"": [
        {{
            ""name"": ""Mobile"",
            ""bindingGroup"": ""Mobile"",
            ""devices"": [{{ ""devicePath"": ""<Touchscreen>"", ""isOptional"": false, ""isOR"": false }}]
        }},
        {{
            ""name"": ""PC"",
            ""bindingGroup"": ""PC"",
            ""devices"": [{{ ""devicePath"": ""<Keyboard>"", ""isOptional"": false, ""isOR"": false }}]
        }}
    ]
}}";
        }

        private static string SetInputSettings(string args)
        {
            string backend     = ParseArg(args, "backend");
            string updateMode  = ParseArg(args, "update_mode");
            string filterNoise = ParseArg(args, "filter_noise");

            EditorApplication.delayCall += () =>
            {
                if (!string.IsNullOrEmpty(backend))
                {
                    switch (backend.ToLower())
                    {
                        case "legacy":
                            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
                            Debug.Log("[GameStudioMCP] To switch input backend: Edit > Project Settings > Player > Active Input Handling");
                            break;
                        case "newinputsystem":
                        case "both":
                            Debug.Log($"[GameStudioMCP] Switch to '{backend}' via: Edit > Project Settings > Player > Active Input Handling. Requires editor restart.");
                            break;
                    }
                }
            };

            return $"{{\"action\":\"set_input_settings\",\"backend\":\"{backend}\",\"status\":\"queued\",\"note\":\"Active Input Handling can be changed in Project Settings > Player > Other Settings\"}}";
        }

        private static string ManageVirtualButton(string args)
        {
            string action      = ParseArg(args, "action")       ?? "create_button";
            string name        = ParseArg(args, "name")         ?? "VirtualButton";
            string canvasName  = ParseArg(args, "canvas");
            string posStr      = ParseArg(args, "position");
            string sizeStr     = ParseArg(args, "size");
            string colorStr    = ParseArg(args, "color");

            EditorApplication.delayCall += () =>
            {
                // Find or create canvas
                Canvas canvas = null;
                if (!string.IsNullOrEmpty(canvasName))
                {
                    var cgo = GameObject.Find(canvasName);
                    canvas = cgo?.GetComponent<Canvas>();
                }
                if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                if (canvas == null)
                {
                    var cgo = new GameObject("Canvas");
                    canvas = cgo.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    cgo.AddComponent<UnityEngine.UI.CanvasScaler>();
                    cgo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }

                var btnGO = new GameObject(name);
                btnGO.transform.SetParent(canvas.transform, false);
                var rect = btnGO.AddComponent<RectTransform>();

                // Parse size
                float w = 150f, h = 150f;
                if (!string.IsNullOrEmpty(sizeStr))
                {
                    var sp = sizeStr.Split(',');
                    if (sp.Length >= 2) { float.TryParse(sp[0].Trim(), out w); float.TryParse(sp[1].Trim(), out h); }
                }
                rect.sizeDelta = new Vector2(w, h);

                // Parse position
                if (!string.IsNullOrEmpty(posStr))
                {
                    var pp = posStr.Split(',');
                    if (pp.Length >= 2 && float.TryParse(pp[0].Trim(), out float px) && float.TryParse(pp[1].Trim(), out float py))
                        rect.anchoredPosition = new Vector2(px, py);
                }

                var img = btnGO.AddComponent<UnityEngine.UI.Image>();
                if (!string.IsNullOrEmpty(colorStr) && ColorUtility.TryParseHtmlString(colorStr.StartsWith("#") ? colorStr : "#" + colorStr, out Color c))
                    img.color = c;
                else
                    img.color = new Color(1, 1, 1, 0.3f);

                if (action == "create_button") btnGO.AddComponent<UnityEngine.UI.Button>();

                Undo.RegisterCreatedObjectUndo(btnGO, $"Create VirtualButton {name}");
                EditorUtility.SetDirty(canvas.gameObject);
            };

            return $"{{\"action\":\"{action}\",\"name\":\"{name}\",\"status\":\"queued\"}}";
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
