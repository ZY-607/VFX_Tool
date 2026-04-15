using System;
using System.IO;
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
        public const string SettingsPath = "Assets/VFX Tools/Editor/Analyzer/VFXAnalyzerProjectDefaults.json";

        public static VFXAnalyzerProjectDefaultsData Load()
        {
            string absolutePath = GetAbsolutePath();
            if (!File.Exists(absolutePath))
            {
                return new VFXAnalyzerProjectDefaultsData();
            }

            try
            {
                string json = File.ReadAllText(absolutePath);
                var data = JsonUtility.FromJson<VFXAnalyzerProjectDefaultsData>(json);
                return data ?? new VFXAnalyzerProjectDefaultsData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VFX Analyzer] 读取项目默认配置失败，将回退到空配置: {e.Message}");
                return new VFXAnalyzerProjectDefaultsData();
            }
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

        private static string GetAbsolutePath()
        {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.Combine(projectRoot, SettingsPath);
        }
    }
}
