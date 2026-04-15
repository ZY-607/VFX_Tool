using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// Google Gemini Vision API 实现
    /// </summary>
    public class GeminiVisionAPI : IVisionAPI
    {
        private string apiKey;
        private const string APIEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent";
        
        public string GetAPIName() => "Google Gemini Vision";
        
        public void SetAPIKey(string key)
        {
            apiKey = key;
        }
        
        public bool ValidateKey()
        {
            return !string.IsNullOrEmpty(apiKey);
        }
        
        public void AnalyzeImage(byte[] imageData, string[] existingTags, Action<VFXAnalysisResult> onComplete)
        {
            var result = new VFXAnalysisResult();
            
            if (!ValidateKey())
            {
                result.isSuccess = false;
                result.errorMessage = "API Key 无效，请检查设置";
                onComplete?.Invoke(result);
                return;
            }
            
            string base64Image = Convert.ToBase64String(imageData);
            string prompt = BuildPrompt(existingTags);
            string jsonBody = BuildRequestBody(base64Image, prompt);
            string url = $"{APIEndpoint}?key={apiKey}";
            
            SendRequest(url, jsonBody, (success, response) =>
            {
                if (success)
                {
                    try
                    {
                        ParseResponse(response, result);
                    }
                    catch (Exception e)
                    {
                        result.isSuccess = false;
                        result.errorMessage = $"解析响应失败: {e.Message}";
                        result.rawResponse = response;
                    }
                }
                else
                {
                    result.isSuccess = false;
                    result.errorMessage = response;
                }
                onComplete?.Invoke(result);
            });
        }
        
        private string BuildPrompt(string[] existingTags)
        {
            string tagsList = existingTags != null && existingTags.Length > 0 
                ? string.Join("、", existingTags) 
                : "无";
                
            return $@"分析这张特效需求图片，提取所有与特效制作相关的关键词。

现有标签池：[{tagsList}]

请全面提取需求中的关键信息，包括但不限于：
- 角色/技能归属（如：穿山兽、主角、敌人等）
- 技能类型（如：充能技能、近战攻击、远程攻击等）
- 攻击方式（如：抓挠、斩击、射击等）
- 判定区域（如：扇形、圆形、直线等）
- 伤害范围（如：单体、范围、AOE等）
- 特效类型（如：火焰、冰霜、雷电、爆炸、烟雾等）
- 视觉风格（如：写实、卡通、像素、水墨等）
- 动态属性（如：循环、一次性、快速爆发、缓慢持续等）
- 光效需求（如：施法光效、受击光效、弹道光效等）
- 其他特效相关关键词

以严格的 JSON 格式返回（不要包含任何其他文字）：
{{
    ""keywords"": [""关键词1"", ""关键词2"", ""关键词3""]
}}

注意：
1. 提取所有与特效制作相关的关键词，不要遗漏
2. 优先使用现有标签池中已有的词汇
3. 如果标签池中没有合适的词，可以提取需求中的原始词汇
4. 所有内容请用中文返回";
        }
        
        private string BuildRequestBody(string base64Image, string prompt)
        {
            string escapedPrompt = EscapeJsonString(prompt);
            
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"contents\":[{");
            sb.Append("\"parts\":[");
            sb.Append("{\"inline_data\":{\"mime_type\":\"image/png\",\"data\":\"").Append(base64Image).Append("\"}},");
            sb.Append("{\"text\":\"").Append(escapedPrompt).Append("\"}");
            sb.Append("]");
            sb.Append("}],");
            sb.Append("\"generationConfig\":{\"temperature\":0.7,\"maxOutputTokens\":4096}");
            sb.Append("}");
            
            return sb.ToString();
        }
        
        private string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
        
        private void ParseResponse(string response, VFXAnalysisResult result)
        {
            var geminiResponse = JsonUtility.FromJson<GeminiResponse>(response);
            
            if (geminiResponse?.candidates != null && geminiResponse.candidates.Count > 0)
            {
                var candidate = geminiResponse.candidates[0];
                if (candidate?.content?.parts != null && candidate.content.parts.Count > 0)
                {
                    string content = candidate.content.parts[0].text;
                    result.rawResponse = content;
                    
                    Debug.Log($"[VFX Analyzer] Gemini 原始响应内容:\n{content}");
                    
                    content = content.Trim();
                    if (content.StartsWith("```json"))
                    {
                        content = content.Substring(7);
                    }
                    if (content.StartsWith("```"))
                    {
                        content = content.Substring(3);
                    }
                    if (content.EndsWith("```"))
                    {
                        content = content.Substring(0, content.Length - 3);
                    }
                    content = content.Trim();
                    
                    Debug.Log($"[VFX Analyzer] 清理后的 JSON:\n{content}");
                    
                    try
                    {
                        var analysisData = JsonUtility.FromJson<AnalysisData>(content);
                        
                        if (analysisData != null && analysisData.keywords != null)
                        {
                            result.keywords = analysisData.keywords;
                            result.isSuccess = true;
                            Debug.Log($"[VFX Analyzer] 解析成功，关键词数量: {result.keywords.Count}");
                        }
                        else
                        {
                            result.isSuccess = false;
                            result.errorMessage = "无法解析 JSON 响应 - analysisData 为空";
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[VFX Analyzer] 首次 JSON 解析失败，尝试修复: {e.Message}");
                        
                        string fixedJson = TryFixTruncatedJson(content);
                        Debug.Log($"[VFX Analyzer] 修复后的 JSON:\n{fixedJson}");
                        
                        try
                        {
                            var analysisData = JsonUtility.FromJson<AnalysisData>(fixedJson);
                            
                            if (analysisData != null && analysisData.keywords != null)
                            {
                                result.keywords = analysisData.keywords;
                                result.isSuccess = true;
                                Debug.Log($"[VFX Analyzer] 修复后解析成功，关键词数量: {result.keywords.Count}");
                            }
                            else
                            {
                                result.isSuccess = false;
                                result.errorMessage = "无法解析 JSON 响应 - analysisData 为空";
                            }
                        }
                        catch (Exception e2)
                        {
                            result.isSuccess = false;
                            result.errorMessage = $"JSON 解析异常: {e2.Message}";
                            Debug.LogError($"[VFX Analyzer] JSON 修复后仍解析失败: {e2.Message}\n原始内容:\n{content}\n修复后:\n{fixedJson}");
                        }
                    }
                }
                else
                {
                    result.isSuccess = false;
                    result.errorMessage = "API 返回空内容";
                }
            }
            else
            {
                result.isSuccess = false;
                result.errorMessage = "API 返回空响应";
            }
        }
        
        private string TryFixTruncatedJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "{}";
            
            json = json.Trim();
            
            if (!json.StartsWith("{"))
            {
                json = "{" + json;
            }
            
            int lastValidQuote = json.LastIndexOf('"');
            int lastValidComma = json.LastIndexOf(',');
            int lastValidBracket = json.LastIndexOf(']');
            int lastValidBrace = json.LastIndexOf('}');
            
            if (lastValidBracket == -1 && lastValidBrace == -1)
            {
                if (lastValidComma > lastValidQuote)
                {
                    json = json.Substring(0, lastValidComma);
                }
                else if (lastValidQuote > 0)
                {
                    int prevQuote = json.LastIndexOf('"', lastValidQuote - 1);
                    if (prevQuote > 0)
                    {
                        json = json.Substring(0, lastValidQuote + 1);
                    }
                }
                
                json = json.TrimEnd(',');
                json += "]}";
            }
            else if (lastValidBrace == -1 && lastValidBracket > 0)
            {
                json = json.Substring(0, lastValidBracket + 1) + "}";
            }
            
            return json;
        }
        
        private void SendRequest(string url, string jsonBody, Action<bool, string> callback)
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };
            
            VFXAPIHelper.SendPostRequest(url, jsonBody, headers, callback);
        }
        
        #region JSON Data Classes
        
        [Serializable]
        private class GeminiResponse
        {
            public List<GeminiCandidate> candidates;
        }
        
        [Serializable]
        private class GeminiCandidate
        {
            public GeminiContentResponse content;
        }
        
        [Serializable]
        private class GeminiContentResponse
        {
            public List<GeminiPartResponse> parts;
        }
        
        [Serializable]
        private class GeminiPartResponse
        {
            public string text;
        }
        
        [Serializable]
        private class AnalysisData
        {
            public List<string> keywords;
        }
        
        #endregion
    }
}
