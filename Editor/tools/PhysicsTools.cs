using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// Physics tools: manage_rigidbody, manage_collider, manage_physics_material,
    /// set_physics_settings, manage_joint
    /// </summary>
    public static class PhysicsTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("manage_rigidbody",
                ToolDef("manage_rigidbody",
                    "Add or configure a Rigidbody (or Rigidbody2D) on a GameObject. Control mass, drag, gravity, constraints.",
                    Param("action",     "string", "add | configure | remove | get"),
                    Param("gameobject", "string", "Target GameObject name"),
                    Param("mode",       "string", "Optional: 2d | 3d (default: 3d)"),
                    Param("mass",       "string", "Optional: mass value e.g. 1.0"),
                    Param("drag",       "string", "Optional: linear drag e.g. 0"),
                    Param("gravity",    "string", "Optional: use_gravity true|false"),
                    Param("kinematic",  "string", "Optional: is_kinematic true|false"),
                    Param("constraints","string", "Optional: freeze axes e.g. 'FreezeRotation' or 'FreezePositionX'")),
                ManageRigidbody);

            MCPToolRegistry.Register("manage_collider",
                ToolDef("manage_collider",
                    "Add or configure colliders on a GameObject. Supports Box, Sphere, Capsule, Mesh, PolygonCollider2D, BoxCollider2D.",
                    Param("action",     "string", "add | configure | remove | get"),
                    Param("gameobject", "string", "Target GameObject name"),
                    Param("type",       "string", "BoxCollider | SphereCollider | CapsuleCollider | MeshCollider | BoxCollider2D | CircleCollider2D | PolygonCollider2D"),
                    Param("is_trigger", "string", "Optional: true|false — make collider a trigger"),
                    Param("size",       "string", "Optional: size as 'x,y,z' (or 'x,y' for 2D)"),
                    Param("center",     "string", "Optional: center offset as 'x,y,z'"),
                    Param("radius",     "string", "Optional: radius for sphere/capsule/circle colliders")),
                ManageCollider);

            MCPToolRegistry.Register("manage_physics_material",
                ToolDef("manage_physics_material",
                    "Create or apply a Physics Material (friction, bounciness) to a collider.",
                    Param("action",      "string", "create | apply | get"),
                    Param("name",        "string", "Material name or asset path"),
                    Param("gameobject",  "string", "Optional: GameObject to apply material to"),
                    Param("friction",    "string", "Optional: dynamic friction 0-1"),
                    Param("bounciness",  "string", "Optional: bounciness 0-1"),
                    Param("friction_combine",   "string", "Optional: Average|Minimum|Maximum|Multiply"),
                    Param("bounce_combine",     "string", "Optional: Average|Minimum|Maximum|Multiply")),
                ManagePhysicsMaterial);

            MCPToolRegistry.Register("set_physics_settings",
                ToolDef("set_physics_settings",
                    "Configure global physics settings: gravity, fixed timestep, solver iterations, layer collision matrix.",
                    Param("gravity_x",    "string", "Optional: gravity X component (default 0)"),
                    Param("gravity_y",    "string", "Optional: gravity Y component (default -9.81)"),
                    Param("gravity_z",    "string", "Optional: gravity Z component (default 0)"),
                    Param("fixed_step",   "string", "Optional: fixed timestep e.g. 0.02"),
                    Param("solver_iters", "string", "Optional: solver iteration count e.g. 6"),
                    Param("bounce_threshold", "string", "Optional: bounce velocity threshold")),
                SetPhysicsSettings);

            MCPToolRegistry.Register("manage_joint",
                ToolDef("manage_joint",
                    "Add or configure physics joints (HingeJoint, FixedJoint, SpringJoint, DistanceJoint2D).",
                    Param("action",      "string", "add | configure | remove"),
                    Param("gameobject",  "string", "Target GameObject name"),
                    Param("type",        "string", "HingeJoint | FixedJoint | SpringJoint | CharacterJoint | HingeJoint2D | DistanceJoint2D | SpringJoint2D"),
                    Param("connected_body", "string", "Optional: name of connected Rigidbody GameObject"),
                    Param("spring",      "string", "Optional: spring value for SpringJoint"),
                    Param("damper",      "string", "Optional: damper value"),
                    Param("limit",       "string", "Optional: limit values as 'min,max'")),
                ManageJoint);
        }

        // ── Implementations ────────────────────────────────────────────────────

        private static string ManageRigidbody(string args)
        {
            string action     = ParseArg(args, "action")     ?? "add";
            string goName     = ParseArg(args, "gameobject");
            string mode       = ParseArg(args, "mode")       ?? "3d";
            string massStr    = ParseArg(args, "mass");
            string dragStr    = ParseArg(args, "drag");
            string gravityStr = ParseArg(args, "gravity");
            string kinStr     = ParseArg(args, "kinematic");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                if (go == null) { Debug.LogWarning($"[GameStudioMCP] manage_rigidbody: GameObject '{goName}' not found"); return; }

                if (mode == "2d")
                {
                    var rb = go.GetComponent<Rigidbody2D>() ?? go.AddComponent<Rigidbody2D>();
                    if (!string.IsNullOrEmpty(massStr)    && float.TryParse(massStr, out float m))  rb.mass = m;
                    if (!string.IsNullOrEmpty(dragStr)    && float.TryParse(dragStr, out float d))  rb.linearDamping = d;
                    if (!string.IsNullOrEmpty(gravityStr)) rb.gravityScale = gravityStr.ToLower() == "false" ? 0f : 1f;
                    if (!string.IsNullOrEmpty(kinStr))     rb.bodyType = kinStr.ToLower() == "true" ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
                    if (action == "remove") UnityEngine.Object.DestroyImmediate(rb);
                }
                else
                {
                    var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
                    if (!string.IsNullOrEmpty(massStr)    && float.TryParse(massStr, out float m))  rb.mass = m;
                    if (!string.IsNullOrEmpty(dragStr)    && float.TryParse(dragStr, out float d))  rb.linearDamping = d;
                    if (!string.IsNullOrEmpty(gravityStr)) rb.useGravity = gravityStr.ToLower() != "false";
                    if (!string.IsNullOrEmpty(kinStr))     rb.isKinematic = kinStr.ToLower() == "true";
                    if (action == "remove") UnityEngine.Object.DestroyImmediate(rb);
                }

                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"mode\":\"{mode}\",\"status\":\"queued\"}}";
        }

        private static string ManageCollider(string args)
        {
            string action    = ParseArg(args, "action")     ?? "add";
            string goName    = ParseArg(args, "gameobject");
            string colType   = ParseArg(args, "type")       ?? "BoxCollider";
            string trigger   = ParseArg(args, "is_trigger");
            string sizeStr   = ParseArg(args, "size");
            string radiusStr = ParseArg(args, "radius");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                if (go == null) { Debug.LogWarning($"[GameStudioMCP] manage_collider: '{goName}' not found"); return; }

                bool isTrigger = trigger?.ToLower() == "true";

                switch (colType)
                {
                    case "BoxCollider":
                    {
                        var c = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
                        c.isTrigger = isTrigger;
                        if (!string.IsNullOrEmpty(sizeStr)) { var v = ParseVec3(sizeStr); if (v.HasValue) c.size = v.Value; }
                        if (action == "remove") UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                    case "SphereCollider":
                    {
                        var c = go.GetComponent<SphereCollider>() ?? go.AddComponent<SphereCollider>();
                        c.isTrigger = isTrigger;
                        if (!string.IsNullOrEmpty(radiusStr) && float.TryParse(radiusStr, out float r)) c.radius = r;
                        if (action == "remove") UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                    case "BoxCollider2D":
                    {
                        var c = go.GetComponent<BoxCollider2D>() ?? go.AddComponent<BoxCollider2D>();
                        c.isTrigger = isTrigger;
                        if (!string.IsNullOrEmpty(sizeStr)) { var v = ParseVec2(sizeStr); if (v.HasValue) c.size = v.Value; }
                        if (action == "remove") UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                    case "CircleCollider2D":
                    {
                        var c = go.GetComponent<CircleCollider2D>() ?? go.AddComponent<CircleCollider2D>();
                        c.isTrigger = isTrigger;
                        if (!string.IsNullOrEmpty(radiusStr) && float.TryParse(radiusStr, out float r)) c.radius = r;
                        if (action == "remove") UnityEngine.Object.DestroyImmediate(c);
                        break;
                    }
                    default:
                        Debug.LogWarning($"[GameStudioMCP] manage_collider: unsupported type {colType}");
                        break;
                }

                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"type\":\"{colType}\",\"status\":\"queued\"}}";
        }

        private static string ManagePhysicsMaterial(string args)
        {
            string action     = ParseArg(args, "action")   ?? "create";
            string name       = ParseArg(args, "name")     ?? "NewPhysicsMaterial";
            string goName     = ParseArg(args, "gameobject");
            string frictionStr   = ParseArg(args, "friction");
            string bounceStr     = ParseArg(args, "bounciness");

            string assetPath = name.StartsWith("Assets/") ? name : $"Assets/PhysicsMaterials/{name}.physicMaterial";

            EditorApplication.delayCall += () =>
            {
                if (action == "create" || action == "apply")
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(assetPath));
                    var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
                    if (mat == null)
                    {
                        mat = new PhysicsMaterial(System.IO.Path.GetFileNameWithoutExtension(assetPath));
                        AssetDatabase.CreateAsset(mat, assetPath);
                    }
                    if (!string.IsNullOrEmpty(frictionStr)  && float.TryParse(frictionStr,  out float f)) mat.dynamicFriction = f;
                    if (!string.IsNullOrEmpty(bounceStr)    && float.TryParse(bounceStr,    out float b)) mat.bounciness     = b;
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();

                    if (!string.IsNullOrEmpty(goName) && action == "apply")
                    {
                        var go = GameObject.Find(goName);
                        var col = go?.GetComponent<Collider>();
                        if (col != null) { col.material = mat; EditorUtility.SetDirty(go); }
                    }
                }
            };

            return $"{{\"action\":\"{action}\",\"asset\":\"{assetPath}\",\"status\":\"queued\"}}";
        }

        private static string SetPhysicsSettings(string args)
        {
            string gx   = ParseArg(args, "gravity_x");
            string gy   = ParseArg(args, "gravity_y");
            string gz   = ParseArg(args, "gravity_z");
            string step = ParseArg(args, "fixed_step");
            string iter = ParseArg(args, "solver_iters");

            EditorApplication.delayCall += () =>
            {
                float x = float.TryParse(gx, out float gxv) ? gxv : Physics.gravity.x;
                float y = float.TryParse(gy, out float gyv) ? gyv : Physics.gravity.y;
                float z = float.TryParse(gz, out float gzv) ? gzv : Physics.gravity.z;
                Physics.gravity = new Vector3(x, y, z);

                if (!string.IsNullOrEmpty(step) && float.TryParse(step, out float s)) Time.fixedDeltaTime = s;
                if (!string.IsNullOrEmpty(iter) && int.TryParse(iter, out int it))   Physics.defaultSolverIterations = it;
            };

            return $"{{\"action\":\"set_physics_settings\",\"gravity\":[\"{gx}\",\"{gy}\",\"{gz}\"],\"status\":\"queued\"}}";
        }

        private static string ManageJoint(string args)
        {
            string action   = ParseArg(args, "action")    ?? "add";
            string goName   = ParseArg(args, "gameobject");
            string type     = ParseArg(args, "type")      ?? "HingeJoint";
            string connBody = ParseArg(args, "connected_body");

            if (string.IsNullOrEmpty(goName)) return Error("gameobject is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(goName);
                if (go == null) { Debug.LogWarning($"[GameStudioMCP] manage_joint: '{goName}' not found"); return; }

                switch (type)
                {
                    case "HingeJoint":   { var j = go.AddComponent<HingeJoint>();   ConnectBody(j, connBody); break; }
                    case "FixedJoint":   { var j = go.AddComponent<FixedJoint>();   ConnectBody(j, connBody); break; }
                    case "SpringJoint":  { var j = go.AddComponent<SpringJoint>();  ConnectBody(j, connBody); break; }
                    default: Debug.LogWarning($"[GameStudioMCP] manage_joint: unsupported type {type}"); break;
                }
                EditorUtility.SetDirty(go);
            };

            return $"{{\"action\":\"{action}\",\"gameobject\":\"{goName}\",\"type\":\"{type}\",\"status\":\"queued\"}}";
        }

        private static void ConnectBody(Joint joint, string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName)) return;
            var go = GameObject.Find(bodyName);
            if (go != null) { var rb = go.GetComponent<Rigidbody>(); if (rb != null) joint.connectedBody = rb; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Vector3? ParseVec3(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(',');
            if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), out float x) &&
                float.TryParse(parts[1].Trim(), out float y) && float.TryParse(parts[2].Trim(), out float z))
                return new Vector3(x, y, z);
            return null;
        }

        private static Vector2? ParseVec2(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split(',');
            if (parts.Length >= 2 && float.TryParse(parts[0].Trim(), out float x) &&
                float.TryParse(parts[1].Trim(), out float y))
                return new Vector2(x, y);
            return null;
        }

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
