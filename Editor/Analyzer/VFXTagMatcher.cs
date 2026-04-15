using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// 标签匹配器
    /// 用标签池反向筛选需求关键词，找出最匹配的标签
    /// </summary>
    public static class VFXTagMatcher
    {
        private const float SimilarityThreshold = 0.7f;
        
        /// <summary>
        /// 用标签池反向筛选需求关键词
        /// 对每个标签，检查需求关键词中是否有相关描述
        /// </summary>
        public static List<string> MatchTags(VFXAnalysisResult analysisResult, List<string> existingTags)
        {
            var matchedTags = new List<string>();
            
            if (existingTags == null || existingTags.Count == 0)
            {
                Debug.LogWarning("[VFX TagMatcher] 标签池为空，无法匹配");
                return matchedTags;
            }
            
            var keywords = analysisResult.GetAllKeywords();
            
            if (keywords == null || keywords.Count == 0)
            {
                Debug.LogWarning("[VFX TagMatcher] AI 未返回任何关键词");
                return matchedTags;
            }
            
            Debug.Log($"[VFX TagMatcher] AI 返回关键词: {string.Join(", ", keywords)}");
            Debug.Log($"[VFX TagMatcher] 标签池: {string.Join(", ", existingTags)}");
            
            string allKeywordsText = string.Join(" ", keywords.Select(k => k.ToLower().Trim()));
            Debug.Log($"[VFX TagMatcher] 关键词文本: {allKeywordsText}");
            
            foreach (var tag in existingTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                
                string lowerTag = tag.ToLower().Trim();
                
                if (allKeywordsText.Contains(lowerTag))
                {
                    Debug.Log($"[VFX TagMatcher] 匹配成功(文本包含): 标签[{tag}] 在关键词文本中找到");
                    if (!matchedTags.Contains(tag))
                    {
                        matchedTags.Add(tag);
                    }
                    continue;
                }
                
                foreach (var keyword in keywords)
                {
                    if (string.IsNullOrEmpty(keyword)) continue;
                    
                    string lowerKeyword = keyword.ToLower().Trim();
                    
                    if (lowerKeyword == lowerTag)
                    {
                        Debug.Log($"[VFX TagMatcher] 匹配成功(完全匹配): 标签[{tag}] = 关键词[{keyword}]");
                        if (!matchedTags.Contains(tag))
                        {
                            matchedTags.Add(tag);
                        }
                        break;
                    }
                    
                    if (lowerKeyword.Contains(lowerTag) || lowerTag.Contains(lowerKeyword))
                    {
                        Debug.Log($"[VFX TagMatcher] 匹配成功(包含匹配): 标签[{tag}] <-> 关键词[{keyword}]");
                        if (!matchedTags.Contains(tag))
                        {
                            matchedTags.Add(tag);
                        }
                        break;
                    }
                    
                    float similarity = CalculateSimilarity(lowerKeyword, lowerTag);
                    if (similarity >= SimilarityThreshold)
                    {
                        Debug.Log($"[VFX TagMatcher] 匹配成功(相似度{similarity:F2}): 标签[{tag}] <-> 关键词[{keyword}]");
                        if (!matchedTags.Contains(tag))
                        {
                            matchedTags.Add(tag);
                        }
                        break;
                    }
                }
            }
            
            Debug.Log($"[VFX TagMatcher] 最终匹配结果: {matchedTags.Count} 个标签 - {string.Join(", ", matchedTags)}");
            
            return matchedTags;
        }
        
        private static float CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            {
                return 0f;
            }
            
            int distance = LevenshteinDistance(source, target);
            int maxLength = Math.Max(source.Length, target.Length);
            
            return 1f - (float)distance / maxLength;
        }
        
        private static int LevenshteinDistance(string source, string target)
        {
            int n = source.Length;
            int m = target.Length;
            
            if (n == 0) return m;
            if (m == 0) return n;
            
            int[,] d = new int[n + 1, m + 1];
            
            for (int i = 0; i <= n; i++)
            {
                d[i, 0] = i;
            }
            
            for (int j = 0; j <= m; j++)
            {
                d[0, j] = j;
            }
            
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
                    
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            
            return d[n, m];
        }
        
        public static Dictionary<string, MatchType> GetMatchDetails(VFXAnalysisResult analysisResult, List<string> existingTags)
        {
            var details = new Dictionary<string, MatchType>();
            
            if (existingTags == null || existingTags.Count == 0)
            {
                return details;
            }
            
            var keywords = analysisResult.GetAllKeywords();
            
            foreach (var tag in existingTags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                
                string lowerTag = tag.ToLower().Trim();
                MatchType bestMatch = MatchType.None;
                
                foreach (var keyword in keywords)
                {
                    if (string.IsNullOrEmpty(keyword)) continue;
                    
                    string lowerKeyword = keyword.ToLower().Trim();
                    
                    if (lowerKeyword == lowerTag)
                    {
                        bestMatch = MatchType.Exact;
                        break;
                    }
                    
                    if (lowerKeyword.Contains(lowerTag) || lowerTag.Contains(lowerKeyword))
                    {
                        if (bestMatch < MatchType.Contains)
                        {
                            bestMatch = MatchType.Contains;
                        }
                        continue;
                    }
                    
                    float similarity = CalculateSimilarity(lowerKeyword, lowerTag);
                    if (similarity >= SimilarityThreshold && bestMatch < MatchType.Similar)
                    {
                        bestMatch = MatchType.Similar;
                    }
                }
                
                if (bestMatch != MatchType.None)
                {
                    details[tag] = bestMatch;
                }
            }
            
            return details;
        }
        
        public enum MatchType
        {
            None,
            Similar,
            Contains,
            Exact
        }
    }
}
