using System;
using System.Collections.Generic;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// 特效需求分析结果
    /// 存储 AI 分析返回的关键词数据
    /// </summary>
    [Serializable]
    public class VFXAnalysisResult
    {
        public List<string> keywords = new List<string>();
        public List<string> matchedTags = new List<string>();
        public string rawResponse = "";
        public bool isSuccess = false;
        public string errorMessage = "";
        
        public string GetSummary()
        {
            if (keywords != null && keywords.Count > 0)
            {
                return $"提取关键词: {string.Join("、", keywords)}";
            }
            return "未提取到关键词";
        }
        
        public List<string> GetAllKeywords()
        {
            return keywords ?? new List<string>();
        }
        
        public void Clear()
        {
            keywords.Clear();
            matchedTags.Clear();
            rawResponse = "";
            isSuccess = false;
            errorMessage = "";
        }
    }
}
