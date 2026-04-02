using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// 2D/Sprite tools: manage_sprite, manage_sprite_renderer, manage_sprite_atlas, create_tilemap
    /// </summary>
    public static class SpriteTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_sprite",
                ToolDef("manage_sprite",
                    "Configure sprite import settings: texture type, pivot, pixels per unit, compression, filter mode.",
                    Param("action",        "string", "configure | get | list"),
                    Param("path",          "string", "Asset path to texture e.g. Assets/Sprites/player.png"),
                    Param("pivot",         "string", "Optional: Center|TopLeft|TopRight|BottomLeft|BottomRight|Top|Bottom|Left|Right or 'x,y' normalized"),
                    Param("ppu",           "string", "Optional: pixels per unit e.g. 100"),
                    Param("filter_mode",   "string", "Optional: Point|Bilinear|Trilinear"),
                    Param("compression",   "string", "Optional: None|Normal|High|Low"),
                    Param("sprite_mode",   "string", "Optional: Single|Multiple|Polygon"),
                    Param("wrap_mode",     "string", "Optional: Clamp|Repeat|Mirror")),
                ManageSprite);

            MCPToolRegistry.Register("manage_sprite_renderer",
                ToolDef("manage_sprite_renderer",
                    "Add or configure a SpriteRenderer on a GameObject. Set sprite, color, sorting layer, flip.",
                    Param("action",        "string", "add | configure | remove | get"),
                    Param("gameobject",    "string", "Target GameObject name"),
                    Param("sprite_path",   "string", "Optional: asset path to sprite texture"),
                    Param("color",         "string", "Optional: hex color e.g. #FFFFFF"),
                    Param("sorting_layer", "string", "Optional: sorting layer name e.g. 'Gameplay'"),
                    Param("order",         "string", "Optional: order in layer e.g. 0"),
                    Param("flip_x",        "string", "Optional: true|false"),
                    Param("flip_y",        "string", "Optional: true|false"),
                    Param("material_path", "string", "Optional: asset path to override material")),
                ManageSpriteRenderer);

            MCPToolRegistry.Register("manage_sprite_atlas",
                ToolDef("manage_sprite_atlas",
                    "Create or configure a Sprite Atlas asset for batching sprites.",
                    Param("action",          "string", "create | add_folder | pack | get_info"),
                    Param("name",            "string", "Atlas name or asset path"),
                    Param("folder",          "string", "Optional: folder path to add sprites from e.g. Assets/Sprites/UI"),
                    Param("allow_rotation",  "string", "Optional: true|false — allow sprite rotation in atlas"),
                    Param("tight_packing",   "string", "Optional: true|false — use tight packing"),
                    Param("padding",         "string", "Optional: padding between sprites in pixels")),
                ManageSpriteAtlas);

            MCPToolRegistry.Register("create_tilemap",
                ToolDef("create_tilemap",
                    "Create a Tilemap with Grid. Configure cell size, orientation, and sorting.",
                    Param("action",        "string", "create | configure | add_rule_tile"),
                    Param("name",          "string", "Tilemap GameObject name"),
                    Param("parent",        "string", "Optional: parent GameObject (usually the Grid)"),
                    Param("cell_size",     "string", "Optional: cell size as 'x,y,z' e.g. '1,1,0'"),
                    Param("cell_layout",   "string", "Optional: Rectangle|Hexagon|Isometric|IsometricZAsY"),
                    Param("sorting_layer", "string", "Optional: sorting layer name"),
                    Param("order",         "string", "Optional: order in layer")),
                CreateTilemap);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageSprite(string args)
        {
            string action     = ParseArg(args, "action")      ?? "configure";
            string path       = ParseArg(args, "path");
            string pivotStr   = ParseArg(args, "pivot");
            string ppuStr     = ParseArg(args, "ppu");
            string filterMode = ParseArg(args, "filter_mode");
            string compress   = ParseArg(args, "compression");
            string spriteMode = ParseArg(args, "sprite_mode");
            string wrapMode   = ParseArg(args, "wrap_mode");

            if (action == "list")
            {
                var sprites = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites" });
                var sb = new StringBuilder("[");
                for (int i = 0; i < sprites.Length; i++)
                {
                    var p = AssetDatabase.GUIDToAssetPath(sprites[i]);
                    sb.Append($"\"{p}\"");
                    if (i < sprites.Length - 1) sb.Append(",");
                }
                sb.Append("]");
                return $"{{\"sprites\":{sb}}}";
            }

            if (string.IsNullOrEmpty(path)) return Error("path is required");

            EditorApplication.delayCall += () =>
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogWarning($"[GameStudioMCP] manage_sprite: no TextureImporter for {path}"); return; }

                importer.textureType = TextureImporterType.Sprite;

                if (!string.IsNullOrEmpty(ppuStr) && float.TryParse(ppuStr, out float ppu)) importer.spritePixelsPerUnit = ppu;

                if (!string.IsNullOrEmpty(pivotStr))
                {
                    var pivotParts = pivotStr.Split(',');
                    if (pivotParts.Length >= 2 && float.TryParse(pivotParts[0].Trim(), out float px) && float.TryParse(pivotParts[1].Trim(), out float py))
                    {
                        importer.spritePivot = new Vector2(px, py);
                    }
                }

                if (!string.IsNullOrEmpty(filterMode) && System.Enum.TryParse<FilterMode>(filterMode, true, out var fm))
                    importer.filterMode = fm;

                if (!string.IsNullOrEmpty(wrapMode) && System.Enum.TryParse<TextureWrapMode>(wrapMode, true, out var wm))
                    importer.wrapMode = wm;

                if (!string.IsNullOrEmpty(spriteMode))
                {
                    switch (spriteMode.ToLower())
                    {
                        case "single":   importer.spriteImportMode = SpriteImportMode.Single;   break;
                        case "multiple": importer.spriteImportMode = SpriteImportMode.Multiple; break;
                        case "polygon":  importer.spriteImportMode = SpriteImportMode.Polygon;  break;
                    }
                }

                importer.SaveAndReimport();
            };

            return $"{{\"action\":\"{action}\",\"path\":\"{path}\",\"status\":\"queued\"}}";
        }

        private static string ManageSpriteRenderer(string args)
        {
            string action       = ParseArg(args, "action")       ?? "add";
            string goName       = ParseArg(args, "gameobject");
            string spritePath   = ParseArg(args, "sprite_path");
            string colorStr     = ParseArg(args, "color");
            string sortingLayer = ParseArg(args, "sorting_layer");
            string orderStr     = ParseArg(args, "order");
            string flipXStr     = ParseArg(args, "flip_x");
            string flipYStr     = ParseArg(args, "flip_y");
            string matPath      = ParseArg(args, "material_path");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            if (action == "get")
            {
                var go = GameObject.Find(goName);
                var sr = go?.GetComponent<SpriteRenderer>();
                if (sr == null) return $"{{\"error\":\"No SpriteRenderer on '{goName}'\"}}";
                return $"{{\"gameobject\":\"{goName}\",\"sprite\":\"{(sr.sprite != null ? sr.sprite.name : "none")}\",\"sortingLayer\":\"{sr.sortingLayerName}\",\"order\":{sr.sortingOrder},\"flipX\":{sr.flipX.ToString().ToLower()},\"flipY\":{sr.flipY.ToString().ToLower()}}}";
            }

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                if (go == null) { Debug.LogWarning($"[GameStudioMCP] manage_sprite_renderer: '{goName}' not found"); return; }

                if (action == "remove") { var sr2 = go.GetComponent<SpriteRenderer>(); if (sr2) UnityEngine.Object.DestroyImmediate(sr2); return; }

                var renderer = go.GetComponent<SpriteRenderer>() ?? go.AddComponent<SpriteRenderer>();

                if (!string.IsNullOrEmpty(spritePath))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null) sprite = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath) != null
                        ? AssetDatabase.LoadAssetAtPath<Sprite>(spritePath) : null;
                    if (sprite != null) renderer.sprite = sprite;
                }

                if (!string.IsNullOrEmpty(colorStr) && ColorUtility.TryParseHtmlString(colorStr.StartsWith("#") ? colorStr : "#" + colorStr, out Color c))
                    renderer.color = c;

                if (!string.IsNullOrEmpty(sortingLayer)) renderer.sortingLayerName = sortingLayer;
                if (!string.IsNullOrEmpty(orderStr)   && int.TryParse(orderStr, out int ord)) renderer.sortingOrder = ord;
                if (!string.IsNullOrEmpty(flipYStr))   renderer.flipY = flipYStr.ToLower() == "true";

                if (!string.IsNullOrEmpty(matPath))
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat != null) renderer.material = mat;
                }

                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"status\":\"queued\"}}";
        }

        private static string ManageSpriteAtlas(string args)
        {
            string action        = ParseArg(args, "action") ?? "create";
            string name          = ParseArg(args, "name")   ?? "GameAtlas";
            string folder        = ParseArg(args, "folder");
            string allowRotation = ParseArg(args, "allow_rotation");
            string tightPacking  = ParseArg(args, "tight_packing");
            string paddingStr    = ParseArg(args, "padding");

            string assetPath = name.StartsWith("Assets/") ? name : $"Assets/Sprites/{name}.spriteatlas";

            if (action == "pack")
            {
                EditorApplication.delayCall += () =>
                {
#if UNITY_2020_1_OR_NEWER
                    UnityEditor.U2D.SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
#endif
                };
                return "{\"action\":\"pack\",\"status\":\"pack_queued\"}";
            }

            EditorApplication.delayCall += () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

#if UNITY_2020_1_OR_NEWER
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(assetPath);
                if (atlas == null)
                {
                    atlas = new UnityEngine.U2D.SpriteAtlas();
                    AssetDatabase.CreateAsset(atlas, assetPath);
                }

                if (!string.IsNullOrEmpty(folder))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
#if UNITY_2020_1_OR_NEWER
                    if (obj != null) Debug.Log("[GameStudioMCP] SpriteAtlas packing requires UnityEditor.U2D namespace");
#endif
                }

                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssets();
#endif
            };

            return $"{{\"action\":\"{action}\",\"asset\":\"{assetPath}\",\"status\":\"queued\"}}";
        }

        private static string CreateTilemap(string args)
        {
            string action      = ParseArg(args, "action")      ?? "create";
            string name        = ParseArg(args, "name")        ?? "Tilemap";
            string parentName  = ParseArg(args, "parent");
            string cellSizeStr = ParseArg(args, "cell_size");
            string cellLayout  = ParseArg(args, "cell_layout");
            string sortLayer   = ParseArg(args, "sorting_layer");
            string orderStr    = ParseArg(args, "order");

            EditorApplication.delayCall += () =>
            {
                GameObject gridGO;
                if (!string.IsNullOrEmpty(parentName))
                {
                    gridGO = GameObject.Find(parentName) ?? new GameObject(parentName);
                }
                else
                {
                    gridGO = new GameObject("Grid");
                    var grid = gridGO.AddComponent<Grid>();

                    if (!string.IsNullOrEmpty(cellLayout) && System.Enum.TryParse<GridLayout.CellLayout>(cellLayout, true, out var cl))
                        grid.cellLayout = cl;

                    if (!string.IsNullOrEmpty(cellSizeStr))
                    {
                        var parts = cellSizeStr.Split(',');
                        if (parts.Length >= 2 && float.TryParse(parts[0].Trim(), out float cx) && float.TryParse(parts[1].Trim(), out float cy))
                        {
                            float cz = parts.Length >= 3 && float.TryParse(parts[2].Trim(), out float czv) ? czv : 0f;
                            grid.cellSize = new Vector3(cx, cy, cz);
                        }
                    }
                }

                var tilemapGO = new GameObject(name);
                tilemapGO.transform.SetParent(gridGO.transform, false);

#if UNITY_2017_2_OR_NEWER
                var tm  = tilemapGO.AddComponent<UnityEngine.Tilemaps.Tilemap>();
                var tmr = tilemapGO.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();

                if (!string.IsNullOrEmpty(sortLayer)) tmr.sortingLayerName = sortLayer;
                if (!string.IsNullOrEmpty(orderStr) && int.TryParse(orderStr, out int ord)) tmr.sortingOrder = ord;
#endif

                Undo.RegisterCreatedObjectUndo(gridGO, $"Create Tilemap {name}");
                EditorUtility.SetDirty(gridGO);
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
