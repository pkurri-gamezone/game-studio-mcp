using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// Central registry for all MCP tools. Each tool category registers itself here.
    /// </summary>
    public static class MCPToolRegistry
    {
        private static readonly Dictionary<string, Func<string, string>> _tools =
            new Dictionary<string, Func<string, string>>();

        private static readonly Dictionary<string, string> _definitions =
            new Dictionary<string, string>();

        static MCPToolRegistry()
        {
            SceneTools.Register();
            ScriptTools.Register();
            LevelTools.Register();
            MonetizeTools.Register();
            PipelineTools.Register();
            BuildTools.Register();
            TestTools.Register();
            CoreUnityTools.Register();
            AssetTools.Register();
            PhysicsTools.Register();
            LightingTools.Register();
            AudioTools.Register();
            ParticleTools.Register();
            SpriteTools.Register();
            InputTools.Register();
            PostProcessTools.Register();
            ProjectTools.Register();
        }

        public static void Register(string name, string definition, Func<string, string> handler)
        {
            _tools[name]       = handler;
            _definitions[name] = definition;
        }

        public static string Call(string name, string argsJson)
        {
            if (_tools.TryGetValue(name, out var handler))
                return handler(argsJson);
            throw new Exception($"Unknown tool: {name}");
        }

        public static IEnumerable<string> GetAllToolDefinitions() => _definitions.Values;

        public static string ReadResource(string uri)
        {
            if (uri.StartsWith("unity://project/info"))   return PipelineTools.GetProjectInfo();
            if (uri.StartsWith("unity://scene/hierarchy")) return SceneTools.GetHierarchyJson();
            if (uri.StartsWith("unity://sprint/status"))  return PipelineTools.GetSprintStatus();
            if (uri.StartsWith("unity://console/log"))    return ScriptTools.GetConsoleLogs();
            return $"{{\"error\":\"Unknown resource: {uri}\"}}";
        }
    }
}
