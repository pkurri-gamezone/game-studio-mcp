using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameStudioMCP
{
    /// <summary>
    /// MCP JSON-RPC 2.0 protocol handler.
    /// Implements: initialize, tools/list, tools/call, resources/list, resources/read
    /// </summary>
    public static class MCPProtocol
    {
        private const string PROTOCOL_VERSION = "2024-11-05";

        public static string Handle(string rawJson)
        {
            try
            {
                string id    = ParseString(rawJson, "id")   ?? "null";
                string method = ParseString(rawJson, "method");
                string idVal  = id == "null" ? "null" : $"\"{id}\"";

                if (string.IsNullOrEmpty(method))
                    return Error(idVal, -32600, "Invalid Request: missing method");

                switch (method)
                {
                    case "initialize":          return Initialize(idVal);
                    case "notifications/initialized": return ""; // fire-and-forget
                    case "ping":                return Result(idVal, "{}");
                    case "tools/list":          return ToolsList(idVal);
                    case "tools/call":          return ToolsCall(idVal, rawJson);
                    case "resources/list":      return ResourcesList(idVal);
                    case "resources/read":      return ResourcesRead(idVal, rawJson);
                    default:
                        return Error(idVal, -32601, $"Method not found: {method}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameStudioMCP] Protocol error: {e.Message}");
                return Error("null", -32603, $"Internal error: {e.Message}");
            }
        }

        // ── Protocol methods ──────────────────────────────────────────────────

        private static string Initialize(string id)
        {
            return Result(id, $@"{{
  ""protocolVersion"": ""{PROTOCOL_VERSION}"",
  ""capabilities"": {{
    ""tools"": {{}},
    ""resources"": {{}}
  }},
  ""serverInfo"": {{
    ""name"": ""game-studio-mcp"",
    ""version"": ""1.0.0"",
    ""description"": ""Advanced Unity MCP server with game-studio-specific tools""
  }}
}}");
        }

        private static string ToolsList(string id)
        {
            var tools = MCPToolRegistry.GetAllToolDefinitions();
            return Result(id, $"{{\"tools\":[{string.Join(",", tools)}]}}");
        }

        private static string ToolsCall(string id, string rawJson)
        {
            string toolName = ParseNestedString(rawJson, "params", "name");
            string argsJson = ParseNestedObject(rawJson, "arguments") ?? "{}";

            if (string.IsNullOrEmpty(toolName))
                return Error(id, -32602, "Invalid params: missing tool name");

            MCPServer.NotifyToolCalled(toolName);

            try
            {
                string result = MCPToolRegistry.Call(toolName, argsJson);
                return Result(id, $"{{\"content\":[{{\"type\":\"text\",\"text\":{EscapeJson(result)}}}]}}");
            }
            catch (Exception e)
            {
                return Result(id, $"{{\"content\":[{{\"type\":\"text\",\"text\":{EscapeJson($"Error: {e.Message}")}}}],\"isError\":true}}");
            }
        }

        private static string ResourcesList(string id)
        {
            return Result(id, @"{""resources"":[
  {""uri"":""unity://project/info"",   ""name"":""Project Info"",   ""mimeType"":""application/json""},
  {""uri"":""unity://scene/hierarchy"",""name"":""Scene Hierarchy"",""mimeType"":""application/json""},
  {""uri"":""unity://sprint/status"",  ""name"":""Sprint Status"",  ""mimeType"":""text/markdown""},
  {""uri"":""unity://console/log"",    ""name"":""Console Log"",    ""mimeType"":""text/plain""}
]}");
        }

        private static string ResourcesRead(string id, string rawJson)
        {
            string uri = ParseNestedString(rawJson, "params", "uri");
            if (string.IsNullOrEmpty(uri))
                return Error(id, -32602, "Invalid params: missing uri");

            string content = MCPToolRegistry.ReadResource(uri);
            return Result(id, $"{{\"contents\":[{{\"uri\":\"{uri}\",\"mimeType\":\"application/json\",\"text\":{EscapeJson(content)}}}]}}");
        }

        // ── JSON helpers ──────────────────────────────────────────────────────

        private static string Result(string id, string result)
            => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}";

        private static string Error(string id, int code, string message)
            => $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"code\":{code},\"message\":{EscapeJson(message)}}}}}";

        private static string EscapeJson(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\n", "\\n").Replace("\r", "\\r")
                           .Replace("\t", "\\t") + "\"";
        }

        private static string ParseString(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ParseNestedString(string json, string parent, string key)
        {
            // Try direct key first
            var direct = ParseString(json, key);
            if (!string.IsNullOrEmpty(direct)) return direct;
            return null;
        }

        private static string ParseNestedObject(string json, string key)
        {
            int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = json.IndexOf('{', idx);
            if (start < 0) return null;
            int depth = 0; var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                sb.Append(json[i]);
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) break; }
            }
            return sb.ToString();
        }
    }
}
