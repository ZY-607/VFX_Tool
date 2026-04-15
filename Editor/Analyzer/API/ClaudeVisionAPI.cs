using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// Anthropic Claude Vision API 实现
    /// </summary>
    public class ClaudeVisionAPI : IVisionAPI
    {
        private string apiKey;
        private const string APIEndpoint = "https://api.anthropic.com/v1/messages";
        private const string APIVersion = "2024-01-01";
        
        public string GetAPIName() => "Claude Vision";
        
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
            
            var requestBody = new ClaudeRequest
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 1000,
                messages = new List<ClaudeMessage>
                {
                    new ClaudeMessage
                    {
                        role = "user",
                        content = new List<ClaudeContent>
                        {
                            new ClaudeContent
                            {
                                type = "image",
                                source = new ClaudeSource
                                {
                                    type = "base64",
                                    media_type = "image/png",
                                    data = base64Image
                                }
                            },
                            new ClaudeContent
                            {
                                type = "text",
                                text = prompt
                            }
                        }
                    }
                }
            };
            
            string jsonBody = JsonUtility.ToJson(requestBody);
            
            SendRequest(APIEndpoint, jsonBody, apiKey, (success, response) =>
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
        
        private void ParseResponse(string response, VFXAnalysisResult result)
        {
            var claudeResponse = JsonUtility.FromJson<ClaudeResponse>(response);
            
            if (claudeResponse?.content != null && claudeResponse.content.Count > 0)
            {
                string content = claudeResponse.content[0].text;
                result.rawResponse = content;
                
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
                
                var analysisData = JsonUtility.FromJson<AnalysisData>(content);
                
                if (analysisData != null)
                {
                    result.keywords = analysisData.keywords ?? new List<string>();
                    result.isSuccess = true;
                }
                else
                {
                    result.isSuccess = false;
                    result.errorMessage = "无法解析 JSON 响应";
                }
            }
            else
            {
                result.isSuccess = false;
                result.errorMessage = "API 返回空响应";
            }
        }
        
        private void SendRequest(string url, string jsonBody, string key, Action<bool, string> callback)
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "x-api-key", key },
                { "anthropic-version", APIVersion }
            };
            
            VFXAPIHelper.SendPostRequest(url, jsonBody, headers, callback);
        }
        
        #region JSON Data Classes
        
        [Serializable]
        private class ClaudeRequest
        {
            public string model;
            public int max_tokens;
            public List<ClaudeMessage> messages;
        }
        
        [Serializable]
        private class ClaudeMessage
        {
            public string role;
            public List<ClaudeContent> content;
        }
        
        [Serializable]
        private class ClaudeContent
        {
            public string type;
            public string text;
            public ClaudeSource source;
        }
        
        [Serializable]
        private class ClaudeSource
        {
            public string type;
            public string media_type;
            public string data;
        }
        
        [Serializable]
        private class ClaudeResponse
        {
            public List<ClaudeContentResponse> content;
        }
        
        [Serializable]
        private class ClaudeContentResponse
        {
            public string type;
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
