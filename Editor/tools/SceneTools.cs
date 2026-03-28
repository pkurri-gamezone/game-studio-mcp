using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    public static class SceneTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("create_gameobject",
                ToolDef("create_gameobject", "Create a new GameObject in the current scene",
                    Param("name", "string", "Name of the new GameObject"),
                    Param("primitive", "string", "Optional: Cube, Sphere, Plane, Cylinder, Capsule, Quad"),
                    Param("parent", "string", "Optional: name of parent GameObject")),
                CreateGameObject);

            MCPToolRegistry.Register("delete_gameobject",
                ToolDef("delete_gameobject", "Delete a GameObject from the scene by name",
                    Param("name", "string", "Name of the GameObject to delete")),
                DeleteGameObject);

            MCPToolRegistry.Register("find_gameobject",
                ToolDef("find_gameobject", "Find a GameObject by name and return its properties",
                    Param("name", "string", "Name to search for")),
                FindGameObject);

            MCPToolRegistry.Register("manage_scene",
                ToolDef("manage_scene", "Scene operations: new, save, open",
                    Param("action", "string", "Action: save | new | hierarchy"),
                    Param("path", "string", "Optional: scene path for open action")),
                ManageScene);
        }

        private static string CreateGameObject(string args)
        {
            string name      = ParseArg(args, "name")      ?? "New GameObject";
            string primitive = ParseArg(args, "primitive");
            string parent    = ParseArg(args, "parent");

            GameObject go = null;

            EditorApplication.delayCall += () =>
            {
                if (!string.IsNullOrEmpty(primitive) &&
                    System.Enum.TryParse<PrimitiveType>(primitive, true, out var pt))
                    go = GameObject.CreatePrimitive(pt);
                else
                    go = new GameObject(name);

                go.name = name;

                if (!string.IsNullOrEmpty(parent))
                {
                    var p = GameObject.Find(parent);
                    if (p != null) go.transform.SetParent(p.transform);
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                Selection.activeGameObject = go;
            };

            return $"{{\"created\":\"{name}\",\"message\":\"GameObject '{name}' created successfully\"}}";
        }

        private static string DeleteGameObject(string args)
        {
            string name = ParseArg(args, "name");
            if (string.IsNullOrEmpty(name)) return Error("name is required");

            EditorApplication.delayCall += () =>
            {
                var go = GameObject.Find(name);
                if (go != null) { Undo.DestroyObjectImmediate(go); }
            };

            return $"{{\"deleted\":\"{name}\"}}";
        }

        private static string FindGameObject(string args)
        {
            string name = ParseArg(args, "name");
            if (string.IsNullOrEmpty(name)) return Error("name is required");

            var go = GameObject.Find(name);
            if (go == null) return $"{{\"found\":false,\"name\":\"{name}\"}}";

            var sb = new StringBuilder();
            sb.Append($"{{\"found\":true,\"name\":\"{go.name}\"");
            sb.Append($",\"active\":{go.activeSelf.ToString().ToLower()}");
            sb.Append($",\"position\":{{\"x\":{go.transform.position.x},\"y\":{go.transform.position.y},\"z\":{go.transform.position.z}}}");
            sb.Append($",\"childCount\":{go.transform.childCount}");
            sb.Append($",\"components\":[");
            var comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null) continue;
                sb.Append($"\"{comps[i].GetType().Name}\"");
                if (i < comps.Length - 1) sb.Append(",");
            }
            sb.Append("]}}");
            return sb.ToString();
        }

        private static string ManageScene(string args)
        {
            string action = ParseArg(args, "action") ?? "hierarchy";
            switch (action.ToLower())
            {
                case "save":
                    EditorApplication.delayCall += () => UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                    return "{\"action\":\"save\",\"status\":\"ok\"}";
                case "new":
                    EditorApplication.delayCall += () => UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                        UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);
                    return "{\"action\":\"new\",\"status\":\"ok\"}";
                case "hierarchy":
                    return $"{{\"action\":\"hierarchy\",\"hierarchy\":{GetHierarchyJson()}}}";
                default:
                    return Error($"Unknown action: {action}");
            }
        }

        public static string GetHierarchyJson()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var sb = new StringBuilder("[");
            for (int i = 0; i < roots.Length; i++)
            {
                sb.Append(SerializeGO(roots[i]));
                if (i < roots.Length - 1) sb.Append(",");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string SerializeGO(GameObject go, int depth = 0)
        {
            var sb = new StringBuilder();
            sb.Append($"{{\"name\":\"{go.name}\",\"active\":{go.activeSelf.ToString().ToLower()}");
            if (go.transform.childCount > 0 && depth < 3)
            {
                sb.Append(",\"children\":[");
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    sb.Append(SerializeGO(go.transform.GetChild(i).gameObject, depth + 1));
                    if (i < go.transform.childCount - 1) sb.Append(",");
                }
                sb.Append("]");
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string ParseArg(string json, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, $"\"{key}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string Error(string msg) => $"{{\"error\":\"{msg}\"}}";

        private static string ToolDef(string name, string desc, params string[] inputProps)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", inputProps)}}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
