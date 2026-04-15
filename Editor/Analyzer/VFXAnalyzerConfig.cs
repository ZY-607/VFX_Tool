using UnityEngine;
using UnityEditor;
using System;

namespace VFXTools.Editor.Analyzer
{
    [Serializable]
    public class VFXAnalyzerConfig
    {
        public enum APIService
        {
            OpenAI,
            Claude,
            Gemini,
            VolcEngine
        }

        public APIService selectedAPI = APIService.Gemini;
        
        public string openAIKey = "";
        public string claudeKey = "";
        public string geminiKey = "";
        public string volcEngineKey = "";
        public bool volcEngineUseCustomModelId = false;
        public string volcEngineCustomModelId = "";
        
        private const string BuiltInGeminiKey = "AIzaSyBbcOcmfBWUiOzTUG4ahcvltvc1UCfHSx0";
        
        public int maxImageSizeMB = 4;
        public int requestTimeoutSeconds = 30;
        
        private const string PrefsKeyPrefix = "VFXAnalyzer_";
        private const string OpenAIKeyPrefs = PrefsKeyPrefix + "OpenAIKey";
        private const string ClaudeKeyPrefs = PrefsKeyPrefix + "ClaudeKey";
        private const string GeminiKeyPrefs = PrefsKeyPrefix + "GeminiKey";
        private const string VolcEngineKeyPrefs = PrefsKeyPrefix + "VolcEngineKey";
        private const string LegacyVolcEngineModelIdPrefs = PrefsKeyPrefix + "VolcEngineModelId";
        private const string VolcEngineUseCustomModelIdPrefs = PrefsKeyPrefix + "VolcEngineUseCustomModelId";
        private const string VolcEngineCustomModelIdPrefs = PrefsKeyPrefix + "VolcEngineCustomModelId";
        private const string SelectedAPIPrefs = PrefsKeyPrefix + "SelectedAPI";

        private VFXAnalyzerProjectDefaultsData projectDefaults = new VFXAnalyzerProjectDefaultsData();
        
        public void Save()
        {
            EditorPrefs.SetString(OpenAIKeyPrefs, EncryptKey(openAIKey));
            EditorPrefs.SetString(ClaudeKeyPrefs, EncryptKey(claudeKey));
            EditorPrefs.SetString(GeminiKeyPrefs, EncryptKey(geminiKey));
            EditorPrefs.SetString(VolcEngineKeyPrefs, EncryptKey(volcEngineKey));
            EditorPrefs.SetBool(VolcEngineUseCustomModelIdPrefs, volcEngineUseCustomModelId);
            EditorPrefs.SetString(VolcEngineCustomModelIdPrefs, volcEngineCustomModelId ?? "");
            EditorPrefs.SetString(LegacyVolcEngineModelIdPrefs, volcEngineUseCustomModelId ? volcEngineCustomModelId ?? "" : "");
            EditorPrefs.SetInt(SelectedAPIPrefs, (int)selectedAPI);
        }
        
        public void Load()
        {
            projectDefaults = VFXAnalyzerProjectDefaultsStorage.Load();

            openAIKey = LoadEncryptedKey(OpenAIKeyPrefs, projectDefaults.openAIKey);
            claudeKey = LoadEncryptedKey(ClaudeKeyPrefs, projectDefaults.claudeKey);
            geminiKey = LoadEncryptedKey(GeminiKeyPrefs, projectDefaults.geminiKey);
            volcEngineKey = LoadEncryptedKey(VolcEngineKeyPrefs, projectDefaults.volcEngineKey);

            var endpointSelection = VFXAnalyzerEndpointResolver.ResolveVolcEngineEndpoint(
                projectDefaults.volcEngineModelId,
                EditorPrefs.HasKey(VolcEngineUseCustomModelIdPrefs),
                EditorPrefs.GetBool(VolcEngineUseCustomModelIdPrefs, false),
                LoadPlainValue(VolcEngineCustomModelIdPrefs, ""),
                EditorPrefs.HasKey(LegacyVolcEngineModelIdPrefs),
                LoadPlainValue(LegacyVolcEngineModelIdPrefs, ""));

            volcEngineUseCustomModelId = endpointSelection.useCustomModelId;
            volcEngineCustomModelId = endpointSelection.customModelId;

            int defaultApiIndex = IsValidAPIService(projectDefaults.selectedAPI)
                ? projectDefaults.selectedAPI
                : (int)APIService.Gemini;

            int savedApiIndex = EditorPrefs.HasKey(SelectedAPIPrefs)
                ? EditorPrefs.GetInt(SelectedAPIPrefs, defaultApiIndex)
                : defaultApiIndex;
            selectedAPI = IsValidAPIService(savedApiIndex)
                ? (APIService)savedApiIndex
                : APIService.Gemini;

            if (endpointSelection.migratedLegacyValue)
            {
                Save();
            }
        }
        
        public string GetCurrentAPIKey()
        {
            switch (selectedAPI)
            {
                case APIService.OpenAI:
                    return openAIKey;
                case APIService.Claude:
                    return claudeKey;
                case APIService.Gemini:
                    return !string.IsNullOrEmpty(geminiKey) ? geminiKey : BuiltInGeminiKey;
                case APIService.VolcEngine:
                    return volcEngineKey;
                default:
                    return "";
            }
        }

        public string GetCurrentVolcEngineModelId()
        {
            return volcEngineUseCustomModelId
                ? volcEngineCustomModelId
                : GetDefaultVolcEngineModelId();
        }

        public string GetDefaultVolcEngineModelId()
        {
            return projectDefaults?.volcEngineModelId ?? "";
        }

        public string GetVolcEngineCustomModelId()
        {
            return volcEngineCustomModelId;
        }

        public bool IsUsingCustomVolcEngineModelId()
        {
            return volcEngineUseCustomModelId;
        }
        
        public bool HasValidAPIKey()
        {
            switch (selectedAPI)
            {
                case APIService.Gemini:
                    return true;
                case APIService.VolcEngine:
                    return !string.IsNullOrEmpty(GetCurrentAPIKey()) && !string.IsNullOrEmpty(GetCurrentVolcEngineModelId());
                default:
                    return !string.IsNullOrEmpty(GetCurrentAPIKey());
            }
        }
        
        public void SetAPIKey(APIService service, string key)
        {
            switch (service)
            {
                case APIService.OpenAI:
                    openAIKey = key;
                    break;
                case APIService.Claude:
                    claudeKey = key;
                    break;
                case APIService.Gemini:
                    geminiKey = key;
                    break;
                case APIService.VolcEngine:
                    volcEngineKey = key;
                    break;
            }
            Save();
        }

        public void SetVolcEngineModelId(string modelId)
        {
            SetVolcEngineCustomModelId(modelId);
            SetVolcEngineUseCustomModelId(!string.IsNullOrWhiteSpace(modelId));
        }

        public void SetVolcEngineCustomModelId(string modelId)
        {
            volcEngineCustomModelId = string.IsNullOrWhiteSpace(modelId) ? "" : modelId.Trim();
            Save();
        }

        public void SetVolcEngineUseCustomModelId(bool useCustomModelId)
        {
            volcEngineUseCustomModelId = useCustomModelId;
            Save();
        }

        private string LoadEncryptedKey(string prefsKey, string fallbackEncryptedValue)
        {
            if (EditorPrefs.HasKey(prefsKey))
            {
                return DecryptKey(EditorPrefs.GetString(prefsKey, ""));
            }

            return DecryptKey(fallbackEncryptedValue);
        }

        private string LoadPlainValue(string prefsKey, string fallbackValue)
        {
            if (EditorPrefs.HasKey(prefsKey))
            {
                return EditorPrefs.GetString(prefsKey, "");
            }

            return fallbackValue ?? "";
        }

        private bool IsValidAPIService(int apiIndex)
        {
            return Enum.IsDefined(typeof(APIService), apiIndex);
        }
        
        private string EncryptKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(key);
            return Convert.ToBase64String(bytes);
        }
        
        private string DecryptKey(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            try
            {
                byte[] bytes = Convert.FromBase64String(encrypted);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }
    }
}
