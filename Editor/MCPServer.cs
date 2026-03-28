using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    [InitializeOnLoad]
    public static class MCPServer
    {
        private static HttpListener _listener;
        private static Thread _serverThread;
        private static bool _running;

        public static int Port => EditorPrefs.GetInt("GameStudioMCP_Port", 8090);
        public static bool IsRunning => _running;
        public static event Action<string> OnToolCalled;
        public static event Action<bool> OnRunningChanged;

        static MCPServer()
        {
            if (EditorPrefs.GetBool("GameStudioMCP_AutoStart", true))
                EditorApplication.delayCall += Start;

            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        public static void Start()
        {
            if (_running) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");

            try
            {
                _listener.Start();
                _running = true;
                _serverThread = new Thread(ServerLoop) { IsBackground = true, Name = "GameStudioMCP" };
                _serverThread.Start();
                Debug.Log($"[GameStudioMCP] ✅ Server started → http://localhost:{Port}/mcp");
                EditorApplication.delayCall += () => OnRunningChanged?.Invoke(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameStudioMCP] Failed to start: {e.Message}. Try a different port in Window > Game Studio MCP.");
                _running = false;
            }
        }

        public static void Stop()
        {
            if (!_running) return;
            _running = false;
            try { _listener?.Stop(); } catch { }
            _serverThread?.Join(500);
            Debug.Log("[GameStudioMCP] Server stopped.");
            EditorApplication.delayCall += () => OnRunningChanged?.Invoke(false);
        }

        public static void Restart()
        {
            Stop();
            EditorApplication.delayCall += Start;
        }

        private static void ServerLoop()
        {
            while (_running)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(HandleRequest, context);
                }
                catch (HttpListenerException) { break; }
                catch (Exception e)
                {
                    if (_running) Debug.LogError($"[GameStudioMCP] Error: {e.Message}");
                }
            }
        }

        private static void HandleRequest(object state)
        {
            var ctx = (HttpListenerContext)state;
            var req = ctx.Request;
            var res = ctx.Response;

            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, Accept");

            if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

            // Health / discovery
            if (req.HttpMethod == "GET" && (req.Url.LocalPath == "/" || req.Url.LocalPath == "/health"))
            {
                WriteJson(res, $"{{\"status\":\"running\",\"server\":\"GameStudioMCP\",\"version\":\"1.0.0\",\"port\":{Port},\"mcp\":\"http://localhost:{Port}/mcp\"}}");
                return;
            }

            // MCP JSON-RPC endpoint
            if (req.HttpMethod == "POST" && req.Url.LocalPath == "/mcp")
            {
                string body = new StreamReader(req.InputStream, req.ContentEncoding).ReadToEnd();
                string response = MCPProtocol.Handle(body);
                WriteJson(res, response);
                return;
            }

            res.StatusCode = 404;
            WriteJson(res, "{\"error\":\"Not found\"}");
        }

        private static void WriteJson(HttpListenerResponse res, string json)
        {
            res.ContentType = "application/json";
            byte[] buf = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = buf.Length;
            res.OutputStream.Write(buf, 0, buf.Length);
            res.Close();
        }

        public static void NotifyToolCalled(string toolName)
        {
            EditorApplication.delayCall += () => OnToolCalled?.Invoke(toolName);
        }

        /// <summary>Returns the correct config block for a given IDE.</summary>
        public static string GetIDEConfig(string ide)
        {
            int port = Port;
            switch (ide.ToLower())
            {
                case "windsurf":
                case "cursor":
                case "claude-desktop":
                    return $"{{\"mcpServers\":{{\"gameStudioMCP\":{{\"url\":\"http://localhost:{port}/mcp\"}}}}}}";
                case "vscode":
                    return $"{{\"servers\":{{\"gameStudioMCP\":{{\"type\":\"http\",\"url\":\"http://localhost:{port}/mcp\"}}}}}}";
                default:
                    return $"{{\"mcpServers\":{{\"gameStudioMCP\":{{\"url\":\"http://localhost:{port}/mcp\"}}}}}}";
            }
        }
    }
}
