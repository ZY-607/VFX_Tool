using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// OpenAI GPT-4 Vision API 实现
    /// </summary>
    public class OpenAIVisionAPI : IVisionAPI
    {
        private string apiKey;
        private const string APIEndpoint = "https://api.openai.com/v1/chat/completions";
        
        public string GetAPIName() => "OpenAI GPT-4 Vision";
        
        public void SetAPIKey(string key)
        {
            apiKey = key;
        }
        
        public bool ValidateKey()
        {
            return !string.IsNullOrEmpty(apiKey) && apiKey.StartsWith("sk-");
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
            
            var requestBody = new OpenAIRequest
            {
                model = "gpt-4o",
                messages = new List<Message>
                {
                    new Message
                    {
                        role = "user",
                        content = new List<Content>
                        {
                            new Content
                            {
                                type = "text",
                                text = prompt
                            },
                            new Content
                            {
                                type = "image_url",
                                image_url = new ImageUrl
                                {
                                    url = $"data:image/png;base64,{base64Image}"
                                }
                            }
                        }
                    }
                },
                max_tokens = 1000
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
            var openAIResponse = JsonUtility.FromJson<OpenAIResponse>(response);
            
            if (openAIResponse?.choices != null && openAIResponse.choices.Count > 0)
            {
                string content = openAIResponse.choices[0].message.content;
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
                { "Authorization", $"Bearer {key}" }
            };
            
            VFXAPIHelper.SendPostRequest(url, jsonBody, headers, callback);
        }
        
        #region JSON Data Classes
        
        [Serializable]
        private class OpenAIRequest
        {
            public string model;
            public List<Message> messages;
            public int max_tokens;
        }
        
        [Serializable]
        private class Message
        {
            public string role;
            public List<Content> content;
        }
        
        [Serializable]
        private class Content
        {
            public string type;
            public string text;
            public ImageUrl image_url;
        }
        
        [Serializable]
        private class ImageUrl
        {
            public string url;
        }
        
        [Serializable]
        private class OpenAIResponse
        {
            public List<Choice> choices;
        }
        
        [Serializable]
        private class Choice
        {
            public MessageResponse message;
        }
        
        [Serializable]
        private class MessageResponse
        {
            public string content;
        }
        
        [Serializable]
        private class AnalysisData
        {
            public List<string> keywords;
        }
        
        #endregion
    }
}
