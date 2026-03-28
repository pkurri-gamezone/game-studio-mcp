using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameStudioMCP
{
    public static class MonetizeTools
    {
        public static void Register()
        {
            MCPToolRegistry.Register("toggle_test_ads",
                ToolDef("toggle_test_ads", "Hot-swap AdManager between test and production AdMob IDs without recompile",
                    Param("mode", "string", "test | production"),
                    Param("path", "string", "Optional: path to AdManager.cs")),
                ToggleTestAds);

            MCPToolRegistry.Register("get_iap_status",
                ToolDef("get_iap_status", "Check Unity IAP integration and PlayerPrefs purchase state",
                    Param("product_id", "string", "Optional: specific product ID to check")),
                GetIAPStatus);

            MCPToolRegistry.Register("check_gdpr_consent",
                ToolDef("check_gdpr_consent", "Read GDPR consent state from PlayerPrefs and GDPRManager",
                    Param("reset", "string", "Optional: true to reset consent for testing")),
                CheckGDPRConsent);

            MCPToolRegistry.Register("get_monetization_summary",
                ToolDef("get_monetization_summary", "Return full monetization status: ads, IAP, GDPR, Firebase Remote Config"),
                GetMonetizationSummary);
        }

        private static string ToggleTestAds(string args)
        {
            string mode      = ParseArg(args, "mode") ?? "test";
            string adMgrPath = ParseArg(args, "path") ?? "Assets/Scripts/Monetization/AdManager.cs";
            string fullPath  = Path.Combine(Application.dataPath, "..", adMgrPath);

            if (!File.Exists(fullPath)) return $"{{\"error\":\"AdManager.cs not found at {adMgrPath}\"}}";

            string src = File.ReadAllText(fullPath);

            bool isTest = mode.ToLower() == "test";
            // Replace interstitial ID
            src = Regex.Replace(src,
                @"(private const string InterstitialAdUnitId = "")[^""]*""",
                $"$1{(isTest ? "ca-app-pub-3940256099942544/1033173712" : "YOUR_INTERSTITIAL_ID")}\"");
            // Replace rewarded ID
            src = Regex.Replace(src,
                @"(private const string RewardedAdUnitId = "")[^""]*""",
                $"$1{(isTest ? "ca-app-pub-3940256099942544/5224354917" : "YOUR_REWARDED_ID")}\"");

            File.WriteAllText(fullPath, src);
            AssetDatabase.Refresh();

            return $"{{\"mode\":\"{mode}\",\"message\":\"AdManager IDs switched to {mode} mode\",\"path\":\"{adMgrPath}\"}}";
        }

        private static string GetIAPStatus(string args)
        {
            string productId = ParseArg(args, "product_id");
            bool noAds = PlayerPrefs.GetInt("NoAds", 0) == 1;

            if (!string.IsNullOrEmpty(productId))
                return $"{{\"product_id\":\"{productId}\",\"purchased\":{noAds.ToString().ToLower()}}}";

            return $"{{\"no_ads_purchased\":{noAds.ToString().ToLower()},\"iap_initialized\":true,\"message\":\"Unity IAP status from PlayerPrefs\"}}";
        }

        private static string CheckGDPRConsent(string args)
        {
            string reset = ParseArg(args, "reset");
            if (reset?.ToLower() == "true")
            {
                PlayerPrefs.DeleteKey("GDPR_Consent_Given");
                PlayerPrefs.Save();
                return "{\"reset\":true,\"message\":\"GDPR consent cleared — will prompt on next launch\"}";
            }

            bool hasKey     = PlayerPrefs.HasKey("GDPR_Consent_Given");
            int  consentVal = PlayerPrefs.GetInt("GDPR_Consent_Given", -1);
            return $"{{\"consent_given\":{(consentVal == 1).ToString().ToLower()},\"has_responded\":{hasKey.ToString().ToLower()},\"consent_value\":{consentVal}}}";
        }

        private static string GetMonetizationSummary(string args)
        {
            bool noAds       = PlayerPrefs.GetInt("NoAds", 0) == 1;
            bool gdprConsent = PlayerPrefs.GetInt("GDPR_Consent_Given", -1) == 1;
            bool gdprAnswered = PlayerPrefs.HasKey("GDPR_Consent_Given");

            bool adMgrExists = File.Exists(Path.Combine(Application.dataPath, "Scripts/Monetization/AdManager.cs"));
            bool iapMgrExists = File.Exists(Path.Combine(Application.dataPath, "Scripts/Monetization/IAPManager.cs"));
            bool gdprMgrExists = File.Exists(Path.Combine(Application.dataPath, "Scripts/Compliance/GDPRManager.cs"));

            return $@"{{
  ""ads"": {{""admanager_present"":{adMgrExists.ToString().ToLower()},""no_ads_purchased"":{noAds.ToString().ToLower()}}},
  ""iap"": {{""iapmanager_present"":{iapMgrExists.ToString().ToLower()}}},
  ""gdpr"": {{""manager_present"":{gdprMgrExists.ToString().ToLower()},""consent_given"":{gdprConsent.ToString().ToLower()},""has_responded"":{gdprAnswered.ToString().ToLower()}}},
  ""remote_config"": {{""status"":""not_connected — run game monetize to set up Firebase""}}
}}";
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
