using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// 特效需求分析器核心类
    /// 整合图片处理、API 调用、标签匹配等功能
    /// </summary>
    public class VFXRequirementAnalyzer
    {
        private VFXAnalyzerConfig config;
        private IVisionAPI currentAPI;
        private VFXAnalysisResult lastResult;
        private bool isAnalyzing = false;
        
        public event Action<VFXAnalysisResult> OnAnalysisComplete;
        public event Action<string> OnError;
        public event Action OnAnalysisStart;
        
        public bool IsAnalyzing => isAnalyzing;
        public VFXAnalysisResult LastResult => lastResult;
        public VFXAnalyzerConfig Config => config;
        
        public VFXRequirementAnalyzer()
        {
            config = new VFXAnalyzerConfig();
            config.Load();
            lastResult = new VFXAnalysisResult();
            
            MainThreadDispatcher.Initialize();
        }
        
        public void SetAPIService(VFXAnalyzerConfig.APIService service)
        {
            config.selectedAPI = service;
            config.Save();
            currentAPI = CreateAPIInstance(service);
        }
        
        public void SetAPIKey(VFXAnalyzerConfig.APIService service, string apiKey)
        {
            config.SetAPIKey(service, apiKey);
        }

        public void SetVolcEngineModelId(string modelId)
        {
            config.SetVolcEngineModelId(modelId);
        }

        public void SetVolcEngineCustomModelId(string modelId)
        {
            config.SetVolcEngineCustomModelId(modelId);
        }

        public void SetVolcEngineUseCustomModelId(bool useCustomModelId)
        {
            config.SetVolcEngineUseCustomModelId(useCustomModelId);
        }
        
        public string GetCurrentAPIKey()
        {
            return config.GetCurrentAPIKey();
        }

        public string GetCurrentVolcEngineModelId()
        {
            return config.GetCurrentVolcEngineModelId();
        }

        public string GetDefaultVolcEngineModelId()
        {
            return config.GetDefaultVolcEngineModelId();
        }

        public string GetVolcEngineCustomModelId()
        {
            return config.GetVolcEngineCustomModelId();
        }

        public bool IsUsingCustomVolcEngineModelId()
        {
            return config.IsUsingCustomVolcEngineModelId();
        }
        
        public bool HasValidAPIKey()
        {
            return config.HasValidAPIKey();
        }
        
        public void AnalyzeImage(string imagePath, List<string> existingTags)
        {
            if (isAnalyzing)
            {
                OnError?.Invoke("正在分析中，请等待...");
                return;
            }
            
            if (!HasValidAPIKey())
            {
                OnError?.Invoke("请先设置 API Key");
                return;
            }
            
            if (!File.Exists(imagePath))
            {
                OnError?.Invoke($"图片文件不存在: {imagePath}");
                return;
            }
            
            byte[] imageData = LoadAndCompressImage(imagePath);
            if (imageData == null)
            {
                OnError?.Invoke("无法加载图片文件");
                return;
            }
            
            AnalyzeImage(imageData, existingTags);
        }
        
        public void AnalyzeImage(byte[] imageData, List<string> existingTags)
        {
            if (isAnalyzing)
            {
                OnError?.Invoke("正在分析中，请等待...");
                return;
            }
            
            if (!HasValidAPIKey())
            {
                OnError?.Invoke("请先设置 API Key");
                return;
            }
            
            isAnalyzing = true;
            OnAnalysisStart?.Invoke();
            
            PrepareCurrentAPI();
            
            string[] tagsArray = existingTags?.ToArray() ?? new string[0];
            
            currentAPI.AnalyzeImage(imageData, tagsArray, (result) =>
            {
                isAnalyzing = false;
                
                Debug.Log($"[VFX Analyzer] AI 返回状态: isSuccess={result.isSuccess}, keywords数量={result.keywords?.Count ?? 0}");
                if (result.keywords != null && result.keywords.Count > 0)
                {
                    Debug.Log($"[VFX Analyzer] AI 返回关键词: {string.Join(", ", result.keywords)}");
                }
                if (!string.IsNullOrEmpty(result.rawResponse))
                {
                    Debug.Log($"[VFX Analyzer] AI 原始响应: {result.rawResponse}");
                }
                if (!result.isSuccess)
                {
                    Debug.LogError($"[VFX Analyzer] AI 分析失败: {result.errorMessage}");
                }
                
                if (result.isSuccess)
                {
                    result.matchedTags = VFXTagMatcher.MatchTags(result, existingTags);
                }
                
                lastResult = result;
                OnAnalysisComplete?.Invoke(result);
            });
        }

        public void TestConnection(Action<VFXAnalysisResult> onComplete)
        {
            if (isAnalyzing)
            {
                onComplete?.Invoke(new VFXAnalysisResult
                {
                    isSuccess = false,
                    errorMessage = "正在分析中，请等待当前请求完成后再测试连接"
                });
                return;
            }

            if (!HasValidAPIKey())
            {
                onComplete?.Invoke(new VFXAnalysisResult
                {
                    isSuccess = false,
                    errorMessage = config.selectedAPI == VFXAnalyzerConfig.APIService.VolcEngine
                        ? "请先设置火山引擎 API Key，并确认默认或自定义 Endpoint ID 可用"
                        : "请先设置 API Key"
                });
                return;
            }

            isAnalyzing = true;
            OnAnalysisStart?.Invoke();
            PrepareCurrentAPI();

            currentAPI.AnalyzeImage(CreateConnectionTestImage(), new string[0], (result) =>
            {
                isAnalyzing = false;
                onComplete?.Invoke(result);
            });
        }
        
        private IVisionAPI CreateAPIInstance(VFXAnalyzerConfig.APIService service)
        {
            switch (service)
            {
                case VFXAnalyzerConfig.APIService.OpenAI:
                    return new OpenAIVisionAPI();
                case VFXAnalyzerConfig.APIService.Claude:
                    return new ClaudeVisionAPI();
                case VFXAnalyzerConfig.APIService.Gemini:
                    return new GeminiVisionAPI();
                case VFXAnalyzerConfig.APIService.VolcEngine:
                    return new VolcEngineVisionAPI();
                default:
                    return new OpenAIVisionAPI();
            }
        }

        private void PrepareCurrentAPI()
        {
            currentAPI = CreateAPIInstance(config.selectedAPI);
            currentAPI.SetAPIKey(config.GetCurrentAPIKey());
            if (currentAPI is VolcEngineVisionAPI volcEngineAPI)
            {
                volcEngineAPI.SetModelId(config.GetCurrentVolcEngineModelId());
            }
        }

        private byte[] CreateConnectionTestImage()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            byte[] imageData = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return imageData;
        }
        
        private byte[] LoadAndCompressImage(string imagePath)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(imagePath);
                
                if (fileData.Length <= config.maxImageSizeMB * 1024 * 1024)
                {
                    return fileData;
                }
                
                Debug.LogWarning($"[VFX Analyzer] 图片过大 ({fileData.Length / 1024 / 1024}MB)，正在压缩...");
                
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    float scale = Mathf.Sqrt((float)(config.maxImageSizeMB * 1024 * 1024) / fileData.Length);
                    int newWidth = Mathf.FloorToInt(texture.width * scale);
                    int newHeight = Mathf.FloorToInt(texture.height * scale);
                    
                    Texture2D scaledTexture = ScaleTexture(texture, newWidth, newHeight);
                    byte[] compressedData = scaledTexture.EncodeToPNG();
                    
                    UnityEngine.Object.DestroyImmediate(texture);
                    UnityEngine.Object.DestroyImmediate(scaledTexture);
                    
                    Debug.Log($"[VFX Analyzer] 图片压缩完成: {compressedData.Length / 1024}KB");
                    return compressedData;
                }
                
                UnityEngine.Object.DestroyImmediate(texture);
                return fileData;
            }
            catch (Exception e)
            {
                Debug.LogError($"[VFX Analyzer] 加载图片失败: {e.Message}");
                return null;
            }
        }
        
        private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    float u = (float)x / targetWidth;
                    float v = (float)y / targetHeight;
                    
                    int sourceX = Mathf.FloorToInt(u * source.width);
                    int sourceY = Mathf.FloorToInt(v * source.height);
                    
                    result.SetPixel(x, y, source.GetPixel(sourceX, sourceY));
                }
            }
            
            result.Apply();
            return result;
        }
        
        public void ClearResult()
        {
            lastResult = new VFXAnalysisResult();
        }
        
        public void SaveConfig()
        {
            config.Save();
        }
        
        public void LoadConfig()
        {
            config.Load();
        }
    }
}
