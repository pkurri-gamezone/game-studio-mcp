using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    public class MCPEditorWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string  _logText = "";
        private int     _port    = 8090;
        private bool    _autoStart = true;

        [MenuItem("Window/Game Studio MCP")]
        public static void ShowWindow()
        {
            var win = GetWindow<MCPEditorWindow>("Game Studio MCP");
            win.minSize = new Vector2(420, 560);
        }

        private void OnEnable()
        {
            _port      = EditorPrefs.GetInt("GameStudioMCP_Port", 8090);
            _autoStart = EditorPrefs.GetBool("GameStudioMCP_AutoStart", true);
            MCPServer.OnToolCalled    += OnToolCalled;
            MCPServer.OnRunningChanged += OnRunningChanged;
        }

        private void OnDisable()
        {
            MCPServer.OnToolCalled    -= OnToolCalled;
            MCPServer.OnRunningChanged -= OnRunningChanged;
        }

        private void OnToolCalled(string tool)
        {
            _logText = $"[{System.DateTime.Now:HH:mm:ss}] Tool called: {tool}\n" + _logText;
            if (_logText.Length > 3000) _logText = _logText.Substring(0, 3000);
            Repaint();
        }

        private void OnRunningChanged(bool running) => Repaint();

        private void OnGUI()
        {
            // ── Header ─────────────────────────────────────────────────────────
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal   = { textColor = new Color(0.4f, 0.8f, 1f) }
            };
            GUILayout.Space(8);
            GUILayout.Label("⚙ Game Studio MCP", headerStyle);
            GUILayout.Label("Advanced Unity ↔ AI Bridge | Claude · Windsurf · Cursor · VS Code",
                EditorStyles.miniLabel);

            DrawHR();

            // ── Status ─────────────────────────────────────────────────────────
            bool running = MCPServer.IsRunning;
            var statusColor = running ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.3f);
            var statusStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = statusColor } };
            GUILayout.Label(running ? "🟢  Server Running" : "🔴  Server Stopped", statusStyle);

            if (running)
                GUILayout.Label($"   URL: http://localhost:{MCPServer.Port}/mcp", EditorStyles.miniLabel);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUI.enabled = !running;
            if (GUILayout.Button("▶ Start",  GUILayout.Height(28))) MCPServer.Start();
            GUI.enabled = running;
            if (GUILayout.Button("■ Stop",   GUILayout.Height(28))) MCPServer.Stop();
            GUI.enabled = true;
            if (GUILayout.Button("↺ Restart",GUILayout.Height(28))) MCPServer.Restart();
            GUILayout.EndHorizontal();

            DrawHR();

            // ── Settings ───────────────────────────────────────────────────────
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _port      = EditorGUILayout.IntField("Port",       _port);
            _autoStart = EditorGUILayout.Toggle("Auto-start",  _autoStart);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetInt("GameStudioMCP_Port",      _port);
                EditorPrefs.SetBool("GameStudioMCP_AutoStart", _autoStart);
            }

            DrawHR();

            // ── IDE Configure ──────────────────────────────────────────────────
            GUILayout.Label("Configure IDE", EditorStyles.boldLabel);
            GUILayout.Label("Click to copy the config snippet and paste into your IDE's MCP settings.",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4);

            DrawIDEButton("Windsurf",       "windsurf",       new Color(0.3f, 0.6f, 1.0f));
            DrawIDEButton("Cursor",         "cursor",         new Color(0.7f, 0.4f, 1.0f));
            DrawIDEButton("Claude Desktop", "claude-desktop", new Color(1.0f, 0.6f, 0.2f));
            DrawIDEButton("VS Code",        "vscode",         new Color(0.2f, 0.7f, 0.9f));

            DrawHR();

            // ── Tool Log ───────────────────────────────────────────────────────
            GUILayout.Label("Tool Call Log", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(140));
            GUILayout.Label(string.IsNullOrEmpty(_logText) ? "No tools called yet." : _logText,
                EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();

            DrawHR();

            // ── Available Tools ────────────────────────────────────────────────
            GUILayout.Label("📦 47 Tools Available  (Full unity-mcp parity + game-studio extras)", EditorStyles.boldLabel);
            string[] categories = {
                "Scene:     create_gameobject · delete_gameobject · find_gameobject · manage_scene",
                "Scripts:   create_script · edit_script · read_script · validate_script · delete_script · read_console",
                "Levels:    list_levels · load_level · generate_level · get_level_data",
                "Monetize:  toggle_test_ads · get_iap_status · check_gdpr_consent · get_monetization_summary",
                "Pipeline:  get_sprint_status · get_project_info · run_audit · get_game_metrics",
                "Build:     trigger_build · get_build_settings · set_bundle_id · manage_packages",
                "Tests:     run_tests · get_test_files · create_test",
                "Core:      batch_execute · execute_menu_item · refresh_unity · manage_editor · manage_components",
                "           find_in_file · manage_asset · unity_docs · get_editor_state · apply_text_edits",
                "Assets:    manage_material · manage_prefabs · manage_ui · manage_animation · manage_camera",
                "           manage_texture · manage_scriptable_object"
            };
            foreach (var cat in categories)
                GUILayout.Label("  " + cat, EditorStyles.miniLabel);

            DrawHR();

            // ── Install via UPM ────────────────────────────────────────────────
            GUILayout.Label("UPM Install URL", EditorStyles.boldLabel);
            string upmUrl = "https://github.com/pkurri-gamezone/game-studio-mcp.git";
            EditorGUILayout.SelectableLabel(upmUrl, EditorStyles.textField, GUILayout.Height(18));
            if (GUILayout.Button("Copy UPM URL"))
            {
                GUIUtility.systemCopyBuffer = upmUrl;
                ShowNotification(new GUIContent("UPM URL copied!"));
            }
        }

        private void DrawIDEButton(string label, string ide, Color color)
        {
            var style = new GUIStyle(GUI.skin.button) { normal = { textColor = color }, fontStyle = FontStyle.Bold };
            if (GUILayout.Button($"Copy config for {label}", style, GUILayout.Height(24)))
            {
                string config = MCPServer.GetIDEConfig(ide);
                GUIUtility.systemCopyBuffer = config;
                ShowNotification(new GUIContent($"Config copied for {label}!"));
            }
        }

        private void DrawHR()
        {
            GUILayout.Space(6);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(2);
        }
    }
}
