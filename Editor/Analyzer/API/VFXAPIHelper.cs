using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// API 请求辅助类
    /// 处理 HTTP 请求和响应
    /// </summary>
    public static class VFXAPIHelper
    {
        private const int DefaultTimeout = 30000;
        
        public static void SendPostRequest(string url, string jsonBody, Dictionary<string, string> headers, Action<bool, string> callback, int timeoutMs = DefaultTimeout)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    using (var client = new System.Net.WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        
                        if (headers != null)
                        {
                            foreach (var header in headers)
                            {
                                client.Headers.Add(header.Key, header.Value);
                            }
                        }
                        
                        var response = client.UploadString(url, "POST", jsonBody);
                        MainThreadDispatcher.Enqueue(() => callback(true, response));
                    }
                }
                catch (System.Net.WebException webEx)
                {
                    string errorMessage = GetWebErrorMessage(webEx);
                    Debug.LogError($"[VFX Analyzer] API 请求失败: {errorMessage}");
                    MainThreadDispatcher.Enqueue(() => callback(false, errorMessage));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VFX Analyzer] 请求异常: {ex.Message}");
                    MainThreadDispatcher.Enqueue(() => callback(false, ex.Message));
                }
            });
            
            thread.IsBackground = true;
            thread.Start();
        }
        
        private static string GetWebErrorMessage(System.Net.WebException webEx)
        {
            if (webEx.Response != null)
            {
                try
                {
                    using (var reader = new System.IO.StreamReader(webEx.Response.GetResponseStream()))
                    {
                        string responseText = reader.ReadToEnd();
                        
                        var errorResponse = JsonUtility.FromJson<APIErrorResponse>(responseText);
                        if (errorResponse?.error?.message != null)
                        {
                            return errorResponse.error.message;
                        }
                        
                        return responseText;
                    }
                }
                catch
                {
                }
            }
            
            return webEx.Message;
        }
        
        [Serializable]
        private class APIErrorResponse
        {
            public APIError error;
        }
        
        [Serializable]
        private class APIError
        {
            public string message;
            public string type;
        }
    }
    
    /// <summary>
    /// 主线程调度器
    /// 用于从后台线程回调到 Unity 主线程
    /// </summary>
    public static class MainThreadDispatcher
    {
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        
        public static void Initialize()
        {
            if (!_initialized)
            {
                UnityEditor.EditorApplication.update += Update;
                _initialized = true;
            }
        }
        
        public static void Enqueue(Action action)
        {
            lock (_lock)
            {
                _actionQueue.Enqueue(action);
            }
        }
        
        private static void Update()
        {
            lock (_lock)
            {
                while (_actionQueue.Count > 0)
                {
                    var action = _actionQueue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[VFX Analyzer] 主线程执行错误: {e.Message}");
                    }
                }
            }
        }
    }
}
