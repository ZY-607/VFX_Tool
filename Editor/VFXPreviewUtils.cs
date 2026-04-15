using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VFXTools.Editor
{
    /// <summary>
    /// VFX 预览工具类，处理动态缩略图渲染
    /// </summary>
    public static class VFXPreviewUtils
    {
        // ==================== 动态缩略图开关配置 ====================
        // 当特效数量过多（如2000+）时，动态缩略图会导致性能问题
        // 设置为 false 可禁用动态预览，改用静态缩略图
        // 代码保留，方便将来需要时快速重新启用
        private const bool EnableDynamicThumbnail = false;
        private const float VisiblePreviewGracePeriod = 0.2f;
        private const float PreviewCacheRetention = 1.2f;
        private const int MaxDynamicPreviewCacheSize = 24;
        
        // ==================== 去重功能配置 ====================
        private const int HashSize = 32;
        private const float SimilarityThreshold = 0.85f;
        
        // 预览缓存数据结构
        private class PreviewData
        {
            public GameObject instance;
            public List<ParticleSystem> particleSystems;
            public PreviewRenderUtility pru;
            public float time;
            public double lastVisibleTime;
        }

        // 缓存字典：Key 为唯一标识符（如路径）
        private static Dictionary<string, PreviewData> _previewCache = new Dictionary<string, PreviewData>();
        
        // 缩略图配置
        private const float LoopDuration = 1.5f; // 循环周期
        private const float CameraDistanceMultiplier = 2.5f; // 相机距离系数 (减小以拉近镜头)
        private const float NearClip = 0.1f;
        
        // 最后更新时间
        private static double _lastUpdateTime;
        
        // 静态缩略图缓存
        private static Dictionary<string, Texture2D> _staticThumbnailCache = new Dictionary<string, Texture2D>();
        private const int MaxStaticThumbnailCacheSize = 100;
        private static List<string> _staticThumbnailCacheOrder = new List<string>();

        /// <summary>
        /// 绘制动态缩略图
        /// </summary>
        /// <param name="rect">绘制区域</param>
        /// <param name="prefab">预制体引用</param>
        /// <param name="id">唯一标识符（通常是路径）</param>
        /// <param name="childPath">如果是组合特效中的子特效，传入其相对路径以仅预览该子节点</param>
        public static void DrawDynamicThumbnail(Rect rect, GameObject prefab, string id, string childPath = null, bool forceDynamic = false)
        {
            if (prefab == null) return;
            
            // 仅在 Repaint 事件中渲染，节省性能
            if (Event.current.type != EventType.Repaint) return;

            // 如果动态缩略图被禁用，使用静态缩略图
            if (!ShouldUseDynamicThumbnail(forceDynamic))
            {
                DrawStaticThumbnail(rect, prefab, id);
                return;
            }

            string previewKey = string.IsNullOrEmpty(childPath) ? $"{id}|__ROOT__" : $"{id}|{childPath}";

            if (!_previewCache.ContainsKey(previewKey))
            {
                InitPreview(prefab, previewKey, childPath);
            }

            if (_previewCache.TryGetValue(previewKey, out var data) && data.pru != null && data.instance != null)
            {
                data.lastVisibleTime = EditorApplication.timeSinceStartup;
                data.pru.BeginPreview(rect, GUIStyle.none);
                
                // 计算包围盒并设置相机
                Bounds bounds = CalculateBounds(data.instance);
                SetupCamera(data.pru.camera, bounds);
                
                // 渲染
                data.pru.camera.Render();
                Texture previewTexture = data.pru.EndPreview();
                
                GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                // Fallback 到静态图标
                Texture2D icon = AssetPreview.GetAssetPreview(prefab);
                if (icon == null) icon = AssetPreview.GetMiniThumbnail(prefab);
                if (icon != null)
                {
                    GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                }
            }
        }
        
        /// <summary>
        /// 绘制静态缩略图（当动态缩略图禁用时使用）
        /// </summary>
        private static void DrawStaticThumbnail(Rect rect, GameObject prefab, string id)
        {
            if (_staticThumbnailCache.TryGetValue(id, out var cachedIcon) && cachedIcon != null)
            {
                _staticThumbnailCacheOrder.Remove(id);
                _staticThumbnailCacheOrder.Add(id);
                GUI.DrawTexture(rect, cachedIcon, ScaleMode.ScaleToFit);
                return;
            }
            
            Texture2D icon = AssetPreview.GetAssetPreview(prefab);
            if (icon == null) icon = AssetPreview.GetMiniThumbnail(prefab);
            
            if (icon != null)
            {
                TrimStaticThumbnailCache();
                _staticThumbnailCache[id] = icon;
                _staticThumbnailCacheOrder.Add(id);
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
            }
        }

        private static void TrimStaticThumbnailCache()
        {
            while (_staticThumbnailCacheOrder.Count >= MaxStaticThumbnailCacheSize)
            {
                if (_staticThumbnailCacheOrder.Count == 0) break;
                string oldestKey = _staticThumbnailCacheOrder[0];
                _staticThumbnailCacheOrder.RemoveAt(0);
                _staticThumbnailCache.Remove(oldestKey);
            }
        }

        /// <summary>
        /// 更新所有预览实例的动画状态
        /// </summary>
        public static bool UpdateAllPreviews()
        {
            if (_previewCache.Count == 0) return false;

            double currentTime = EditorApplication.timeSinceStartup;
            TrimPreviewCache(currentTime);
            if (_previewCache.Count == 0)
            {
                _lastUpdateTime = currentTime;
                return false;
            }

            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            // 限制最大 deltaTime，防止卡顿后飞跃
            if (deltaTime > 0.1f) deltaTime = 0.1f;

            bool needsRepaint = false;
            List<string> keysToRemove = null;

            foreach (var kvp in _previewCache)
            {
                var id = kvp.Key;
                var data = kvp.Value;

                // 安全检查
                if (data.instance == null)
                {
                    if (keysToRemove == null) keysToRemove = new List<string>();
                    keysToRemove.Add(id);
                    continue;
                }

                if (currentTime - data.lastVisibleTime > VisiblePreviewGracePeriod)
                {
                    continue;
                }

                data.time += deltaTime;

                // 循环播放逻辑
                if (data.time > LoopDuration)
                {
                    data.time = 0f;
                    foreach (var ps in data.particleSystems)
                    {
                        if (ps != null)
                        {
                            ps.Simulate(0, true, true);
                            ps.Play(true);
                        }
                    }
                    needsRepaint = true;
                }
                else
                {
                    // 正常模拟
                    foreach (var ps in data.particleSystems)
                    {
                        if (ps != null)
                        {
                            ps.Simulate(deltaTime, true, false);
                            needsRepaint = true;
                        }
                    }
                }
            }

            // 清理无效项
            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                {
                    CleanupPreview(key);
                }
            }

            return needsRepaint;
        }

        /// <summary>
        /// 清理所有预览缓存
        /// </summary>
        public static void CleanupAll()
        {
            foreach (var key in _previewCache.Keys)
            {
                var data = _previewCache[key];
                if (data.pru != null) data.pru.Cleanup();
                if (data.instance != null) Object.DestroyImmediate(data.instance);
            }
            _previewCache.Clear();
            
            _staticThumbnailCache.Clear();
            _staticThumbnailCacheOrder.Clear();
        }

        // --- 私有辅助方法 ---

        private static void InitPreview(GameObject prefab, string cacheKey, string childPath = null)
        {
            var pru = new PreviewRenderUtility();
            pru.camera.cameraType = CameraType.Preview;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f); // 深灰底色
            
            // 灯光设置
            pru.lights[0].intensity = 1.0f;
            pru.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);

            GameObject rootInstance = pru.InstantiatePrefabInScene(prefab);
            rootInstance.transform.position = Vector3.zero;
            // 保持预制体原本的旋转，不重置

            GameObject targetInstance = rootInstance;

            // 如果指定了 childPath，只提取目标子节点，销毁其他部分
            if (!string.IsNullOrEmpty(childPath))
            {
                Transform targetTransform = rootInstance.transform.Find(childPath);
                if (targetTransform != null)
                {
                    // 保存原始世界旋转
                    Quaternion originalWorldRotation = targetTransform.rotation;
                    
                    // 使用Instantiate创建独立副本，避免Prefab实例子对象无法SetParent的问题
                    targetInstance = Object.Instantiate(targetTransform.gameObject);
                    targetInstance.transform.position = Vector3.zero;
                    targetInstance.transform.rotation = originalWorldRotation;  // 保持原始世界旋转
                    targetInstance.SetActive(true);

                    // 必须将新实例加入到 PreviewRenderUtility 的场景中
                    pru.AddSingleGO(targetInstance);

                    // 销毁原本的根节点及其剩余部分
                    Object.DestroyImmediate(rootInstance);
                }
            }

            // 收集粒子系统
            var systems = new List<ParticleSystem>();
            targetInstance.GetComponentsInChildren<ParticleSystem>(true, systems);

            // 初始播放
            foreach (var ps in systems)
            {
                ps.Simulate(0, true, true);
                ps.Play(true);
            }

            _previewCache[cacheKey] = new PreviewData
            {
                instance = targetInstance,
                particleSystems = systems,
                pru = pru,
                time = 0f,
                lastVisibleTime = EditorApplication.timeSinceStartup
            };
        }

        private static void CleanupPreview(string id)
        {
            if (_previewCache.TryGetValue(id, out var data))
            {
                if (data.pru != null) data.pru.Cleanup();
                if (data.instance != null) Object.DestroyImmediate(data.instance);
                _previewCache.Remove(id);
            }
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length > 0)
            {
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            else
            {
                // Fallback size
                bounds.size = Vector3.one;
            }
            
            return bounds;
        }

        private static void SetupCamera(Camera cam, Bounds bounds)
        {
            float maxBound = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxBound == 0) maxBound = 1f;

            float distance = maxBound * CameraDistanceMultiplier;

            // 斜45度俯视 (Isometric-like view)
            // 位置：中心点向后、向上、向右偏移
            Vector3 offset = new Vector3(1f, 1f, -1f).normalized * distance;

            cam.transform.position = bounds.center + offset;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = NearClip;
            cam.farClipPlane = distance * 5f;
        }

        private static void SetupCameraFixedOrigin(Camera cam, Bounds bounds)
        {
            float maxBound = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxBound == 0) maxBound = 1f;

            float distance = maxBound * CameraDistanceMultiplier;

            // 斜45度俯视，但始终对准原点（特效初始中心）
            Vector3 offset = new Vector3(1f, 1f, -1f).normalized * distance;

            cam.transform.position = offset;
            cam.transform.LookAt(Vector3.zero);
            cam.nearClipPlane = NearClip;
            cam.farClipPlane = distance * 5f;
        }

        /// <summary>
        /// 绘制动态缩略图（固定原点视角）
        /// 相机始终对准原点，避免粒子扩散导致视角漂移
        /// </summary>
        public static void DrawDynamicThumbnailFixedOrigin(Rect rect, GameObject prefab, string id, string childPath = null, bool forceDynamic = false)
        {
            if (prefab == null) return;

            // 仅在 Repaint 事件中渲染，节省性能
            if (Event.current.type != EventType.Repaint) return;

            // 如果动态缩略图被禁用，使用静态缩略图
            if (!ShouldUseDynamicThumbnail(forceDynamic))
            {
                DrawStaticThumbnail(rect, prefab, id);
                return;
            }

            string previewKey = string.IsNullOrEmpty(childPath) ? $"{id}|__ROOT__" : $"{id}|{childPath}";

            if (!_previewCache.ContainsKey(previewKey))
            {
                InitPreview(prefab, previewKey, childPath);
            }

            if (_previewCache.TryGetValue(previewKey, out var data) && data.pru != null && data.instance != null)
            {
                data.lastVisibleTime = EditorApplication.timeSinceStartup;
                data.pru.BeginPreview(rect, GUIStyle.none);

                // 计算包围盒并设置相机（固定原点）
                Bounds bounds = CalculateBounds(data.instance);
                SetupCameraFixedOrigin(data.pru.camera, bounds);

                // 渲染
                data.pru.camera.Render();
                Texture previewTexture = data.pru.EndPreview();

                GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                // Fallback 到静态图标
                Texture2D icon = AssetPreview.GetAssetPreview(prefab);
                if (icon == null) icon = AssetPreview.GetMiniThumbnail(prefab);
                if (icon != null)
                {
                    GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                }
            }
        }

        private static bool ShouldUseDynamicThumbnail(bool forceDynamic)
        {
            return forceDynamic || EnableDynamicThumbnail;
        }

        private static void TrimPreviewCache(double currentTime)
        {
            if (_previewCache.Count == 0) return;

            List<string> keysToRemove = null;

            foreach (var kvp in _previewCache)
            {
                if (currentTime - kvp.Value.lastVisibleTime > PreviewCacheRetention)
                {
                    if (keysToRemove == null) keysToRemove = new List<string>();
                    keysToRemove.Add(kvp.Key);
                }
            }

            int retainedCount = _previewCache.Count - (keysToRemove?.Count ?? 0);
            if (retainedCount > MaxDynamicPreviewCacheSize)
            {
                var overflowKeys = _previewCache
                    .OrderByDescending(pair => pair.Value.lastVisibleTime)
                    .Skip(MaxDynamicPreviewCacheSize)
                    .Select(pair => pair.Key);

                if (keysToRemove == null) keysToRemove = new List<string>();
                keysToRemove.AddRange(overflowKeys);
            }

            if (keysToRemove == null) return;

            foreach (var key in keysToRemove.Distinct())
            {
                CleanupPreview(key);
            }
        }

        // ==================== 去重功能方法 ====================

        /// <summary>
        /// 计算预制体的预览图哈希值（用于去重）
        /// </summary>
        public static string ComputePreviewHash(GameObject prefab)
        {
            if (prefab == null) return string.Empty;

            Texture2D previewTexture = RenderPreviewTexture(prefab);
            if (previewTexture == null) return string.Empty;

            string hash = ComputeImageHash(previewTexture);
            Object.DestroyImmediate(previewTexture);
            return hash;
        }

        /// <summary>
        /// 渲染预制体的预览纹理
        /// </summary>
        private static Texture2D RenderPreviewTexture(GameObject prefab)
        {
            var pru = new PreviewRenderUtility();
            pru.camera.cameraType = CameraType.Preview;
            pru.camera.clearFlags = CameraClearFlags.SolidColor;
            pru.camera.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

            pru.lights[0].intensity = 1.0f;
            pru.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);

            GameObject instance = pru.InstantiatePrefabInScene(prefab);
            instance.transform.position = Vector3.zero;

            var systems = new List<ParticleSystem>();
            instance.GetComponentsInChildren<ParticleSystem>(true, systems);

            foreach (var ps in systems)
            {
                ps.Simulate(0, true, true);
                ps.Play(true);
            }

            Bounds bounds = CalculateBounds(instance);
            SetupCamera(pru.camera, bounds);

            Rect rect = new Rect(0, 0, HashSize, HashSize);
            pru.BeginPreview(rect, GUIStyle.none);
            pru.camera.Render();
            Texture renderTexture = pru.EndPreview();

            Texture2D result = new Texture2D(HashSize, HashSize, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture as RenderTexture;
            result.ReadPixels(new Rect(0, 0, HashSize, HashSize), 0, 0);
            result.Apply();
            RenderTexture.active = null;

            pru.Cleanup();
            Object.DestroyImmediate(instance);

            return result;
        }

        /// <summary>
        /// 计算图像哈希值（包含颜色信息）
        /// </summary>
        private static string ComputeImageHash(Texture2D texture)
        {
            if (texture == null) return string.Empty;

            Color[] pixels = texture.GetPixels();
            if (pixels == null || pixels.Length == 0) return string.Empty;

            var hashBuilder = new System.Text.StringBuilder();

            float totalR = 0f, totalG = 0f, totalB = 0f;
            foreach (var pixel in pixels)
            {
                totalR += pixel.r;
                totalG += pixel.g;
                totalB += pixel.b;
            }
            float avgR = totalR / pixels.Length;
            float avgG = totalG / pixels.Length;
            float avgB = totalB / pixels.Length;

            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                int rBit = pixel.r > avgR ? 1 : 0;
                int gBit = pixel.g > avgG ? 1 : 0;
                int bBit = pixel.b > avgB ? 1 : 0;
                hashBuilder.Append($"{rBit}{gBit}{bBit}");
            }

            return hashBuilder.ToString();
        }

        /// <summary>
        /// 比较两个哈希值的相似度
        /// </summary>
        public static float ComputeSimilarity(string hash1, string hash2)
        {
            if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2)) return 0f;
            if (hash1.Length != hash2.Length) return 0f;

            int matchCount = 0;
            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] == hash2[i])
                {
                    matchCount++;
                }
            }

            return (float)matchCount / hash1.Length;
        }

        /// <summary>
        /// 判断两个哈希值是否相似（超过阈值）
        /// </summary>
        public static bool IsSimilar(string hash1, string hash2, float threshold = SimilarityThreshold)
        {
            return ComputeSimilarity(hash1, hash2) >= threshold;
        }
    }
}
