# Game Studio MCP

> Advanced Unity ↔ AI Bridge — embedded MCP server with **zero external dependencies**

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black?logo=unity)](https://unity.com)
[![MCP Protocol 2024-11-05](https://img.shields.io/badge/MCP-2024--11--05-blue)](https://spec.modelcontextprotocol.io)
[![License: MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)

Connect **Claude Desktop, Windsurf, Cursor, VS Code, and GitHub Copilot CLI** directly to your Unity Editor — no Python, no Node.js, no external servers.

---

## Why Game Studio MCP vs unity-mcp?

| Feature | unity-mcp | **game-studio-mcp** |
|---|---|---|
| Dependencies | Python 3.10 + uv | **Zero — pure C#** |
| Windows support | Needs PATH setup | ✅ Works natively |
| Game-studio tools | ❌ | ✅ Ads, IAP, GDPR, Levels, Sprint |
| Level generation | ❌ | ✅ Procedural + AI-driven |
| Compliance tools | ❌ | ✅ GDPR/ATT/IAP validation |
| Sprint tracking | ❌ | ✅ Reads SPRINT_PLAN.md |
| One-click IDE setup | ❌ | ✅ In-editor button per IDE |
| CLI integration | ❌ | ✅ `game mcp configure --ide windsurf` |
| Port conflict | 8080 | **8090 (no conflicts)** |

---

## Install

### Option 1 — Package Manager (Recommended)

In Unity: **Window → Package Manager → + → Add package from git URL**

```
https://github.com/pkurri-gamezone/game-studio-mcp.git
```

### Option 2 — OpenUPM (coming soon)

```bash
openupm add com.pkurri.gamestudiomcp
```

### Option 3 — via CLI
```bash
game mcp install
```

---

## Quick Start

1. **Install** the package (above)
2. In Unity: **Window → Game Studio MCP**
3. Click **▶ Start**
4. Click **Copy config for [your IDE]**
5. Paste into your IDE's MCP settings
6. Restart your IDE — look for 🟢

---

## Configure IDEs

### Windsurf / Cursor / Claude Desktop
```json
{
  "mcpServers": {
    "gameStudioMCP": {
      "url": "http://localhost:8090/mcp"
    }
  }
}
```

**Config file locations:**
- **Windsurf**: `~/.codeium/windsurf/mcp_config.json`
- **Cursor**: `~/.cursor/mcp.json`
- **Claude Desktop**: `~/Library/Application Support/Claude/claude_desktop_config.json`

### VS Code
```json
{
  "servers": {
    "gameStudioMCP": {
      "type": "http",
      "url": "http://localhost:8090/mcp"
    }
  }
}
```

### One-command setup via CLI
```bash
game mcp configure --ide all         # all IDEs
game mcp configure --ide windsurf    # Windsurf only
game mcp configure --ide cursor      # Cursor only
game mcp configure --ide claude-desktop
game mcp configure --ide vscode
```

---

## Available Tools (30)

### 🎮 Scene Tools
| Tool | What it does |
|---|---|
| `create_gameobject` | Create a new GameObject or primitive |
| `delete_gameobject` | Delete a GameObject by name |
| `find_gameobject` | Inspect a GameObject's components + position |
| `manage_scene` | Save scene, new scene, get full hierarchy |

### 📝 Script Tools
| Tool | What it does |
|---|---|
| `create_script` | Write a new C# script to disk + auto-import |
| `edit_script` | Replace a block of code in an existing script |
| `read_script` | Read a C# script's contents |
| `validate_script` | Check for compile errors |
| `read_console` | Read recent Unity console output |

### 🗺 Level Tools
| Tool | What it does |
|---|---|
| `list_levels` | List all level JSON files |
| `load_level` | Request level load by index |
| `generate_level` | Generate N levels (easy/medium/hard) as JSON |
| `get_level_data` | Read level JSON content |

### 💰 Monetization Tools
| Tool | What it does |
|---|---|
| `toggle_test_ads` | Hot-swap AdManager between test / production AdMob IDs |
| `get_iap_status` | Check IAP purchase state from PlayerPrefs |
| `check_gdpr_consent` | Read or reset GDPR consent state |
| `get_monetization_summary` | Full ads + IAP + GDPR + Remote Config status |

### 🚀 Pipeline Tools
| Tool | What it does |
|---|---|
| `get_sprint_status` | Read SPRINT_PLAN.md — current day + tasks |
| `get_project_info` | Project name, version, asset count, platform |
| `run_audit` | Read latest compliance audit report |
| `get_game_metrics` | Memory, scene stats, GameObject count |

### 🔨 Build Tools
| Tool | What it does |
|---|---|
| `trigger_build` | Trigger a Unity build (android/ios/webgl/windows) |
| `get_build_settings` | Current platform, bundle IDs, scenes |
| `set_bundle_id` | Update `com.studio.gamename` |
| `manage_packages` | List or install UPM packages |

### 🧪 Test Tools
| Tool | What it does |
|---|---|
| `run_tests` | Run Unity Test Runner (EditMode or PlayMode) |
| `get_test_files` | List all `*Tests.cs` files |
| `create_test` | Generate a new test file with boilerplate |

---

## Resources

Read static project data with `resources/read`:

| URI | Description |
|---|---|
| `unity://project/info` | Project name, version, asset count |
| `unity://scene/hierarchy` | Full scene hierarchy as JSON |
| `unity://sprint/status` | SPRINT_PLAN.md summary |
| `unity://console/log` | Recent console log |

---

## Example Prompts

```
Create a red cube at (0, 1, 0) called "Player"
```
```
Read the GameManager.cs script and add a HighScore property
```
```
Generate 10 medium difficulty levels and save them
```
```
Toggle ads to test mode and show me the GDPR consent status
```
```
What's the current sprint status? What's left for today?
```
```
Run all EditMode tests and report results
```
```
Trigger an Android development build and tell me when it's done
```
```
Check the monetization summary — are we ready to ship?
```

---

## Architecture

```
Unity Editor Process
  └── MCPServer.cs  [InitializeOnLoad]
        └── System.Net.HttpListener → localhost:8090
              ├── GET  /health       → server info
              └── POST /mcp          → JSON-RPC 2.0
                    ├── initialize
                    ├── tools/list
                    ├── tools/call   → MCPToolRegistry → SceneTools
                    ├── resources/list                 → ScriptTools
                    └── resources/read                 → LevelTools
                                                       → MonetizeTools
                                                       → PipelineTools
                                                       → BuildTools
                                                       → TestTools
```

**No external server. No Python. No Node.js. Pure C# running inside Unity.**

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Server won't start | Try a different port in Window → Game Studio MCP |
| IDE not connecting | Restart IDE after adding config |
| Port conflict with unity-mcp | This server uses port 8090 by default |
| Windows path issues | No special PATH setup needed — pure C# |
| Assembly errors | Ensure Unity 2021.3 LTS or newer |

---

## Works With

- Claude Desktop
- Claude Code
- Windsurf
- Cursor
- VS Code + GitHub Copilot
- Any MCP-compatible client

---

## License

MIT — © pkurri-gamezone
