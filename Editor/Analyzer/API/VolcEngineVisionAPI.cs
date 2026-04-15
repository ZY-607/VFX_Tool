using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    public class VolcEngineVisionAPI : IVisionAPI
    {
        private string apiKey;
        private string modelId = "";
        private const string APIEndpoint = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";
        
        public string GetAPIName() => "火山引擎豆包视觉";
        
        public void SetAPIKey(string key)
        {
            apiKey = key;
        }
        
        public void SetModelId(string modelId)
        {
            this.modelId = string.IsNullOrWhiteSpace(modelId) ? "" : modelId.Trim();
        }
        
        public bool ValidateKey()
        {
            return !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(modelId);
        }
        
        public void AnalyzeImage(byte[] imageData, string[] existingTags, Action<VFXAnalysisResult> onComplete)
        {
            var result = new VFXAnalysisResult();
            
            if (!ValidateKey())
            {
                result.isSuccess = false;
                result.errorMessage = "API Key 或 Endpoint ID 无效，请检查设置";
                onComplete?.Invoke(result);
                return;
            }
            
            string base64Image = Convert.ToBase64String(imageData);
            string prompt = BuildPrompt(existingTags);
            string jsonBody = BuildRequestBody(base64Image, prompt);
            
            Debug.Log($"[VFX Analyzer] 火山引擎请求: model={modelId}");
            
            SendRequest(jsonBody, (success, response) =>
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
                    result.errorMessage = FormatErrorMessage(response);
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
            sb.Append("\"model\":\"").Append(modelId).Append("\",");
            sb.Append("\"messages\":[{");
            sb.Append("\"role\":\"user\",");
            sb.Append("\"content\":[");
            sb.Append("{\"type\":\"text\",\"text\":\"").Append(escapedPrompt).Append("\"},");
            sb.Append("{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/png;base64,").Append(base64Image).Append("\"}}");
            sb.Append("]");
            sb.Append("}],");
            sb.Append("\"max_tokens\":4096");
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
            var volcEngineResponse = JsonUtility.FromJson<VolcEngineResponse>(response);
            
            if (volcEngineResponse?.choices != null && volcEngineResponse.choices.Count > 0)
            {
                var choice = volcEngineResponse.choices[0];
                if (choice?.message?.content != null)
                {
                    string content = choice.message.content;
                    result.rawResponse = content;
                    
                    Debug.Log($"[VFX Analyzer] 火山引擎原始响应内容:\n{content}");
                    
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
        
        private void SendRequest(string jsonBody, Action<bool, string> callback)
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Authorization", $"Bearer {apiKey}" }
            };
            
            VFXAPIHelper.SendPostRequest(APIEndpoint, jsonBody, headers, callback);
        }

        private string FormatErrorMessage(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return "火山引擎请求失败，请检查 API Key 和 Endpoint ID 配置";
            }

            if (response.IndexOf("does not exist or you do not have access", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "当前火山 Endpoint ID 不存在，或你的 API Key 没有访问权限。若默认模式失败，请切换为自定义 Endpoint ID 并填写你自己账号下可用的 Endpoint。\n\n原始错误: " + response;
            }

            return response;
        }
        
        #region JSON Data Classes
        
        [Serializable]
        private class VolcEngineResponse
        {
            public List<VolcEngineChoice> choices;
        }
        
        [Serializable]
        private class VolcEngineChoice
        {
            public VolcEngineMessage message;
        }
        
        [Serializable]
        private class VolcEngineMessage
        {
            public string content;
            public string role;
        }
        
        [Serializable]
        private class AnalysisData
        {
            public List<string> keywords;
        }
        
        #endregion
    }
}
