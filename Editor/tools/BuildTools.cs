using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameStudioMCP
{
    public static class BuildTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("trigger_build",
                ToolDef("trigger_build", "Trigger a Unity build for a target platform",
                    Param("platform", "string", "android | ios | webgl | windows | macos"),
                    Param("output_path", "string", "Optional: output folder path"),
                    Param("development", "string", "Optional: true for development build")),
                TriggerBuild);

            MCPToolRegistry.Register("get_build_settings",
                ToolDef("get_build_settings", "Return current build settings: target platform, scenes, company name, bundle ID"),
                GetBuildSettings);

            MCPToolRegistry.Register("set_bundle_id",
                ToolDef("set_bundle_id", "Set the application bundle identifier",
                    Param("bundle_id", "string", "e.g. com.studio.gamename"),
                    Param("platform", "string", "android | ios | all")),
                SetBundleId);

            MCPToolRegistry.Register("manage_packages",
                ToolDef("manage_packages", "List or install Unity packages via Package Manager",
                    Param("action", "string", "list | install"),
                    Param("package", "string", "Optional: package ID to install e.g. com.unity.textmeshpro")),
                ManagePackages);
        }

        private static string TriggerBuild(string args)
        {
            string platform    = ParseArg(args, "platform")    ?? "android";
            string outputPath  = ParseArg(args, "output_path") ?? $"Builds/{platform}";
            string development = ParseArg(args, "development") ?? "false";

            BuildTarget target = platform.ToLower() switch
            {
                "android" => BuildTarget.Android,
                "ios"     => BuildTarget.iOS,
                "webgl"   => BuildTarget.WebGL,
                "windows" => BuildTarget.StandaloneWindows64,
                "macos"   => BuildTarget.StandaloneOSX,
                _         => BuildTarget.Android
            };

            bool isDev = development.ToLower() == "true";

            var options = new BuildPlayerOptions
            {
                scenes      = GetBuildScenes(),
                locationPathName = outputPath,
                target      = target,
                options     = isDev ? BuildOptions.Development : BuildOptions.None
            };

            EditorApplication.delayCall += () =>
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log($"[GameStudioMCP] Build {report.summary.result}: {report.summary.totalSize} bytes, {report.summary.totalErrors} errors");
            };

            return $"{{\"action\":\"build_triggered\",\"platform\":\"{platform}\",\"output\":\"{outputPath}\",\"development\":{isDev.ToString().ToLower()},\"message\":\"Build started — monitor Unity console for progress\"}}";
        }

        private static string GetBuildSettings(string args)
        {
            string[] scenes = GetBuildScenes();
            string sceneList = string.Join(",", System.Array.ConvertAll(scenes, s => $"\"{s}\""));

            return $@"{{
  ""product_name"":""{PlayerSettings.productName}"",
  ""company_name"":""{PlayerSettings.companyName}"",
  ""bundle_id_android"":""{PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)}"",
  ""bundle_id_ios"":""{PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)}"",
  ""version"":""{PlayerSettings.bundleVersion}"",
  ""active_platform"":""{EditorUserBuildSettings.activeBuildTarget}"",
  ""scenes"":[{sceneList}],
  ""scripting_backend"":""{PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)}""
}}";
        }

        private static string SetBundleId(string args)
        {
            string bundleId = ParseArg(args, "bundle_id");
            string platform = ParseArg(args, "platform") ?? "all";

            if (string.IsNullOrEmpty(bundleId)) return "{\"error\":\"bundle_id is required\"}";

            EditorApplication.delayCall += () =>
            {
                if (platform == "all" || platform == "android")
                    PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, bundleId);
                if (platform == "all" || platform == "ios")
                    PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, bundleId);
                AssetDatabase.SaveAssets();
            };

            return $"{{\"bundle_id\":\"{bundleId}\",\"platform\":\"{platform}\",\"message\":\"Bundle ID updated\"}}";
        }

        private static string ManagePackages(string args)
        {
            string action  = ParseArg(args, "action")  ?? "list";
            string package = ParseArg(args, "package");

            if (action == "list")
            {
                var listReq = UnityEditor.PackageManager.Client.List(true);
                return "{\"action\":\"list\",\"message\":\"Package list requested — check Package Manager window\",\"status\":\"async\"}";
            }

            if (action == "install" && !string.IsNullOrEmpty(package))
            {
                EditorApplication.delayCall += () =>
                {
                    var addReq = UnityEditor.PackageManager.Client.Add(package);
                    Debug.Log($"[GameStudioMCP] Installing package: {package}");
                };
                return $"{{\"action\":\"install\",\"package\":\"{package}\",\"message\":\"Package installation started\"}}";
            }

            return "{\"error\":\"Invalid action or missing package name\"}";
        }

        private static string[] GetBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var paths = new System.Collections.Generic.List<string>();
            foreach (var s in scenes)
                if (s.enabled) paths.Add(s.path);
            return paths.ToArray();
        }

        private static string ParseArg(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ToolDef(string name, string desc, params string[] props)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{{string.Join(",", props)}}}}}}}";

        private static string ToolDef(string name, string desc)
            => $"{{\"name\":\"{name}\",\"description\":\"{desc}\",\"inputSchema\":{{\"type\":\"object\",\"properties\":{{}}}}}}";

        private static string Param(string name, string type, string desc)
            => $"\"{name}\":{{\"type\":\"{type}\",\"description\":\"{desc}\"}}";
    }
}
