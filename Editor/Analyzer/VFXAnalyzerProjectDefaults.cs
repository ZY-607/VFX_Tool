using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    [Serializable]
    public class VFXAnalyzerProjectDefaultsData
    {
        public string openAIKey = "";
        public string claudeKey = "";
        public string geminiKey = "";
        public string volcEngineKey = "";
        public string volcEngineModelId = "";
        public int selectedAPI = (int)VFXAnalyzerConfig.APIService.Gemini;
    }

    public static class VFXAnalyzerProjectDefaultsStorage
    {
        private const string SettingsFileName = "VFXAnalyzerProjectDefaults.json";
        private static string _settingsPath;

        private static string SettingsPath
        {
            get
            {
                if (string.IsNullOrEmpty(_settingsPath))
                {
                    _settingsPath = FindSettingsPath();
                }
                return _settingsPath;
            }
        }

        public static VFXAnalyzerProjectDefaultsData Load()
        {
            try
            {
                string assetPath = SettingsPath;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (asset != null)
                {
                    var data = JsonUtility.FromJson<VFXAnalyzerProjectDefaultsData>(asset.text);
                    Debug.Log($"[VFX Analyzer] 成功加载项目默认配置: {assetPath}");
                    return data ?? new VFXAnalyzerProjectDefaultsData();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VFX Analyzer] 读取项目默认配置失败: {e.Message}");
            }

            Debug.Log("[VFX Analyzer] 未找到项目默认配置，使用空配置");
            return new VFXAnalyzerProjectDefaultsData();
        }

        public static void Save(VFXAnalyzerProjectDefaultsData data)
        {
            string absolutePath = GetAbsolutePath();
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(data ?? new VFXAnalyzerProjectDefaultsData(), true);
            File.WriteAllText(absolutePath, json);
            AssetDatabase.Refresh();
        }

        private static string FindSettingsPath()
        {
            // 方案1：通过 Unity Package Manager API 获取包路径（最可靠）
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);

                if (packageInfo != null)
                {
                    string settingsPath = $"{packageInfo.assetPath}/Editor/Analyzer/{SettingsFileName}";
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(settingsPath);
                    if (asset != null)
                    {
                        Debug.Log($"[VFX Analyzer] 通过 PackageInfo 定位配置: {settingsPath}");
                        return settingsPath;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VFX Analyzer] PackageInfo 定位失败: {e.Message}");
            }

            // 方案2：尝试已知的 Package 路径
            string[] possiblePaths = new string[]
            {
                "Packages/com.nd.vfxtools/Editor/Analyzer/VFXAnalyzerProjectDefaults.json",
                "Assets/VFX Tools/Editor/Analyzer/VFXAnalyzerProjectDefaults.json",
                "Assets/com.nd.vfxtools/Editor/Analyzer/VFXAnalyzerProjectDefaults.json"
            };

            foreach (var path in possiblePaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null)
                {
                    Debug.Log($"[VFX Analyzer] 通过预设路径定位配置: {path}");
                    return path;
                }
            }

            // 方案3：默认 Package 路径
            string defaultPath = $"Packages/com.nd.vfxtools/Editor/Analyzer/{SettingsFileName}";
            Debug.Log($"[VFX Analyzer] 使用默认配置路径: {defaultPath}");
            return defaultPath;
        }

        private static string GetAbsolutePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relativePath = SettingsPath;

            // 如果是 Package 路径，转换为 Library 缓存路径（用于保存用户自定义配置）
            if (relativePath.StartsWith("Packages/"))
            {
                // Package 内的文件是只读的，用户配置保存到项目根目录
                return Path.Combine(projectRoot, "UserSettings", "VFXAnalyzerProjectDefaults.json");
            }

            return Path.Combine(projectRoot, relativePath);
        }
    }
}
