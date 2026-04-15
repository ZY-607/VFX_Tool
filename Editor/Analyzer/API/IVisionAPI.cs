using System;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// 视觉 AI API 接口
    /// 定义所有支持的 Vision API 的统一接口
    /// </summary>
    public interface IVisionAPI
    {
        string GetAPIName();
        void SetAPIKey(string apiKey);
        bool ValidateKey();
        void AnalyzeImage(byte[] imageData, string[] existingTags, Action<VFXAnalysisResult> onComplete);
    }
}
