# Agent → MCP Tool Binding Map

Every CLI agent (`game <command>`) maps to specific MCP tools on the Unity side.  
The CLI calls these tools via `POST http://localhost:8090/mcp` after its AI step completes.

---

## Full Binding Table

| CLI Agent | Studio Role | MCP Tools Called | Trigger Point |
|-----------|-------------|------------------|---------------|
| `game trend` | PULSE | *(external only — no Unity call)* | — |
| `game analyze` | Creative Director | *(external only)* | — |
| `game gdd` | Creative Director | *(external only)* | — |
| `game sprint` | Producer | `manage_player_settings`, `refresh_unity` | After GDD parse — applies bundle ID + version |
| `game new` | Technical Director | `manage_tags`, `manage_layers`, `manage_sorting_layers`, `manage_player_settings`, `manage_input_action`, `refresh_unity` | After scaffold — configures fresh project |
| `game level` | Design Lead | `create_script` (LevelLoader), `refresh_unity` | After JSON write — drops loader + reimports |
| `game monetize` | Economy Designer | `create_script` (AdManager, IAPManager), `refresh_unity` | After template drop |
| `game review` | GLITCH + TURBO | `read_console`, `validate_script`, `find_in_file`, `get_editor_state` | Before + after review pass |
| `game audit` | GLITCH | `read_console`, `find_in_file`, `validate_script`, `manage_player_settings` | Full audit scan |
| `game aso` | Release Lead | `manage_player_settings` | Sets final bundle ID + display name |
| `game localize` | Narrative Lead | `create_script` (LocalizationManager), `refresh_unity` | After locale file write |
| `game build` | Release Manager | `manage_editor`, `execute_menu_item` | Triggers build from CLI |
| `game release` | GATEWAY | `get_editor_state`, `read_console`, `validate_script`, `manage_player_settings` | 25-point checklist verification |
| `game deploy` | Release Manager | `execute_menu_item` (build pipeline) | Pre-deploy Unity build trigger |
| `game pipeline` | DevOps | `get_editor_state`, `manage_player_settings` | CI/CD setup + status |
| `game mcp` | System | *(server lifecycle)* | Server status/configure/install |
| `game mechanics` | PIXEL | `create_gameobject`, `manage_components`, `create_script`, `refresh_unity` | After mechanic generation |
| `game qa` | GLITCH | `read_console`, `validate_script`, `find_in_file`, `get_editor_state` | QA sweep |
| `game autofix` | TURBO | `edit_script`, `read_script`, `validate_script`, `refresh_unity` | Auto-repair loop |
| `game npc` | Design Lead | `create_gameobject`, `manage_components`, `create_script`, `refresh_unity` | After NPC generation |
| `game sprite` | Design Lead | `manage_sprite`, `manage_sprite_renderer`, `manage_sprite_atlas`, `refresh_unity` | After sprite import |
| `game pixel` | PIXEL | `create_script`, `manage_sprite`, `refresh_unity` | Pixel art tooling |
| `game mesh` | PIXEL | `manage_material`, `manage_prefabs`, `refresh_unity` | After mesh generation |
| `game texture` | Design Lead | `manage_sprite`, `manage_material`, `refresh_unity` | After texture config |
| `game skybox` | Design Lead | `manage_material`, `set_lighting_settings`, `refresh_unity` | After skybox material write |
| `game voice` | Narrative Lead | `manage_audio_source`, `manage_audio_clip`, `refresh_unity` | After voice clip import |
| `game story` | Narrative Lead | `create_script`, `refresh_unity` | After dialogue data write |
| `game ship` | GATEWAY | `manage_player_settings`, `get_editor_state`, `validate_script`, `read_console` | Full ship checklist |

---

## Tool Category → Agent Usage

### Scene & GameObject Tools
```
create_gameobject    ← game mechanics, game npc, game new
delete_gameobject    ← game autofix (cleanup)
find_gameobject      ← game review, game qa
manage_scene         ← game sprint (scene setup), game build
```

### Script Tools
```
create_script        ← game level, game monetize, game localize, game mechanics, game npc, game story
edit_script          ← game autofix
read_script          ← game review, game audit, game autofix
validate_script      ← game review, game audit, game qa, game release, game ship
read_console         ← game review, game audit, game qa, game release
```

### Physics Tools  *(new)*
```
manage_rigidbody     ← game mechanics (physics-based puzzles like ScrewVault)
manage_collider      ← game mechanics, game npc
manage_physics_material ← game mechanics (bounce/friction for puzzle physics)
set_physics_settings ← game new (project-level gravity for 2D/3D)
manage_joint         ← game mechanics (hinge/spring puzzles)
```

### Lighting Tools  *(new)*
```
manage_light         ← game new (initial scene lighting), game sprint (art direction)
set_lighting_settings ← game new, game skybox
manage_reflection_probe ← game sprint (environment polish)
bake_lighting        ← game build (pre-bake before build)
```

### Audio Tools  *(new)*
```
manage_audio_source  ← game voice, game new (AudioManager setup)
manage_audio_mixer   ← game new (master mixer), game monetize (rewarded ad volume)
manage_audio_clip    ← game voice --sfx <preset>     (procedural WAV, no API key)
                    ← game voice --sfx-kit <name>    (batch: all presets at once)
                    ← game voice --search <query>    (Freesound CC-0 download + import)
                    ← game sprite (SFX import)
play_audio           ← game review (audio QA)
```

#### SFX Pipeline (game voice → MCP)
```
game voice --sfx screw                      # single preset WAV → manage_audio_clip
game voice --sfx-kit screw-vault            # 8-preset batch   → mcpImportAudioClips (batch_execute)
game voice --sfx-kit puzzle                 # 5 sounds: click, pop, win, unlock, error
game voice --sfx-kit ui                     # 4 sounds: click, coin, win, error
game voice --search "metal screw click"     # Freesound CC-0   → download → manage_audio_clip
```
SFX Presets (Screw Vault optimized): `screw | click | pop | plate_drop | win | coin | unlock | error`

> **Unity-Audio-Manager** (https://github.com/MathewHDYT/Unity-Audio-Manager) — Kavex/GameDev-Resources pick.
> Drop into `Assets/Plugins/` for a zero-boilerplate `AudioManager` that wraps all `manage_audio_*` MCP calls.

### Particle Tools  *(new)*
```
manage_particle_system ← game mechanics (success burst), game new (template VFX)
set_particle_emission  ← game mechanics (hit/explosion)
set_particle_renderer  ← game sprite (2D particle sprites)
preview_particles      ← game review (VFX QA)
```

### Sprite/2D Tools  *(new)*
```
manage_sprite          ← game sprite (DALL-E 3 generate → TinyPNG compress → import)
                       ← game sprite --compress-only <dir>  (batch compress existing PNGs)
manage_sprite_renderer ← game sprite, game mechanics (2D game objects)
manage_sprite_atlas    ← game sprite (atlas packing for mobile)
create_tilemap         ← game mechanics (tilemap-based levels like Flow City)
```

#### Sprite Compression Pipeline (game sprite → MCP)
```
game sprite --subject "bolt icon" --type icon --compress   # generate → TinyPNG → manage_sprite
game sprite --compress-only ./Assets/Art/Sprites           # compress existing → manage_sprite (batch)
game sprite --compress-only ./Assets/Art/Kenney            # compress downloaded Kenney packs
```

### Input Tools  *(new)*
```
manage_input_action    ← game new (GameControls.inputactions scaffold)
set_input_settings     ← game new (New Input System activation)
manage_virtual_button  ← game new (mobile D-pad/tap scaffold)
```

### Post-Processing Tools  *(new)*
```
manage_post_process_volume ← game new (URP global volume), game sprint (visual polish)
set_bloom              ← game sprint (day 7-8 polish step)
set_color_grading      ← game sprint (art direction final pass)
set_camera_effects     ← game sprint (DOF / vignette for menus)
```

### Project Tools  *(new)*
```
manage_tags            ← game new (Enemy, Collectable, Obstacle tags)
manage_layers          ← game new (Gameplay, UI, Background layers)
manage_sorting_layers  ← game new (Background, Gameplay, UI, Overlay sorting)
manage_player_settings ← game new, game sprint, game aso, game release, game ship
manage_player_prefs    ← game review (debug prefs), game qa
```

### Asset Tools
```
manage_material       ← game mesh, game texture, game skybox
manage_prefabs        ← game mechanics, game level, game npc
manage_ui             ← game new (UIManager setup)
manage_animation      ← game mechanics, game npc
manage_camera         ← game new, game sprint
manage_texture        ← game texture, game sprite
manage_scriptable_object ← game level (LevelData SO)
```

### Bug Reporting Tools  *(Kavex/GameDev-Resources)*
```
read_console         ← game audit (Instabug session replay + crash log correlation)
validate_script      ← game audit (verify Instabug SDK integration)
```
> **Instabug** (https://instabug.com/platforms/unity) — Unity-native in-app bug reporting.
> Detected by `game audit` — checks for `Instabug`/`IBG` in C#/ObjC files.
> Beta testers shake device → screenshot + console log auto-attached to report.

### Build & Pipeline Tools
```
trigger_build         ← game build, game ship
get_build_status      ← game pipeline
manage_build_settings ← game new, game build
generate_build_report ← game release, game ship
run_tests             ← game qa, game release
```

---

## MCP HTTP Call Format

Every CLI agent uses this helper to call MCP tools:

```javascript
async function mcpCall(tool, args, port = 8090) {
  try {
    const res = await fetch(`http://localhost:${port}/mcp`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        jsonrpc: '2.0', id: 1, method: 'tools/call',
        params: { name: tool, arguments: args }
      })
    });
    return await res.json();
  } catch {
    return null; // MCP not running — CLI continues without Unity sync
  }
}
```

### Example: `game level` → Unity Sync
```javascript
// After writing levels.json to Assets/Resources/Data/
await mcpCall('refresh_unity', {});
await mcpCall('create_script', {
  path: 'Assets/Scripts/Gameplay/LevelLoader.cs',
  content: levelLoaderBoilerplate
});
```

### Example: `game new` → Unity Project Bootstrap
```javascript
await mcpCall('manage_tags',   { action: 'add', tag: 'Collectable' });
await mcpCall('manage_tags',   { action: 'add', tag: 'Enemy' });
await mcpCall('manage_layers', { action: 'add', layer: 'Gameplay' });
await mcpCall('manage_layers', { action: 'add', layer: 'Background' });
await mcpCall('manage_sorting_layers', { action: 'add', layer: 'Background' });
await mcpCall('manage_sorting_layers', { action: 'add', layer: 'Gameplay' });
await mcpCall('manage_sorting_layers', { action: 'add', layer: 'UI' });
await mcpCall('manage_input_action', {
  action: 'create_asset',
  asset_path: 'Assets/Input/GameControls.inputactions'
});
await mcpCall('manage_player_settings', {
  bundle_id: bundleId, orientation: 'Portrait',
  scripting_backend: 'IL2CPP'
});
await mcpCall('refresh_unity', {});
```

### Example: `game review` → Unity QA
```javascript
await mcpCall('read_console',    { filter: 'Error' });
await mcpCall('get_editor_state', {});
await mcpCall('find_in_file',    { query: 'TODO|FIXME|HACK', path: 'Assets/Scripts' });
```

---

## Batch Execute Pattern (10x faster)

For `game new` which needs to configure 10+ Unity settings in one shot:

```javascript
await mcpCall('batch_execute', {
  calls: JSON.stringify([
    { tool: 'manage_tags',   arguments: { action: 'add', tag: 'Enemy' } },
    { tool: 'manage_tags',   arguments: { action: 'add', tag: 'Collectable' } },
    { tool: 'manage_layers', arguments: { action: 'add', layer: 'Gameplay' } },
    { tool: 'manage_layers', arguments: { action: 'add', layer: 'Background' } },
    { tool: 'manage_sorting_layers', arguments: { action: 'add', layer: 'Background' } },
    { tool: 'manage_sorting_layers', arguments: { action: 'add', layer: 'Gameplay' } },
    { tool: 'manage_sorting_layers', arguments: { action: 'add', layer: 'UI' } },
    { tool: 'manage_sorting_layers', arguments: { action: 'add', layer: 'Overlay' } },
    { tool: 'manage_player_settings', arguments: { orientation: 'Portrait', scripting_backend: 'IL2CPP' } },
    { tool: 'refresh_unity', arguments: {} }
  ])
});
```

---

## Version History

| Version | Changes |
|---------|---------|
| v1.0.0 | 47 tools across 9 categories (Scene, Script, Level, Monetize, Pipeline, Build, Test, CoreUnity, Asset) |
| v1.1.0 | +33 tools: PhysicsTools (5), LightingTools (4), AudioTools (4), ParticleTools (4), SpriteTools (4), InputTools (3), PostProcessTools (4), ProjectTools (5) = **80 total tools** |
| v1.2.0 | Kavex/GameDev-Resources integration: Unity-Audio-Manager ref in AudioTools, Instabug BugReportingTools section, Appodeal in monetize check |
| v1.3.0 | SFX pipeline: `game voice --sfx` / `--sfx-kit` / `--search` → `manage_audio_clip` via `mcpImportAudioClips`. Sprite pipeline: `--compress` / `--compress-only` → `manage_sprite` via `mcpImportSprites`. Dedicated `mcpImportAudioClips()` + `mcpImportSprites()` helpers in mcpHelper.js |
