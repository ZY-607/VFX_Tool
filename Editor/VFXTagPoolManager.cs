using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VFXTools.Editor
{
    public static class VFXTagPoolManager
    {
        private const string TagPoolFileName = "VFXTagPool.json";
        private const string Version = "1.0.0";

        private static string _tagPoolPath;
        private static TagPoolData _cachedData;
        private static bool _isLoaded = false;

        public static System.Action<string, string> OnTagRenamed;
        public static System.Action<string> OnTagRemoved;

        private static string TagPoolPath
        {
            get
            {
                if (string.IsNullOrEmpty(_tagPoolPath))
                {
                    _tagPoolPath = FindTagPoolPath();
                }
                return _tagPoolPath;
            }
        }

        private static string FindTagPoolPath()
        {
            // 方案1：通过 Unity Package Manager API 获取包路径（最可靠）
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
                
                if (packageInfo != null)
                {
                    string poolPath = $"{packageInfo.assetPath}/Editor/{TagPoolFileName}";
                    Debug.Log($"[标签池] 通过 PackageInfo 定位: {poolPath}");
                    return poolPath;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[标签池] PackageInfo 定位失败: {e.Message}");
            }
            
            // 方案2：尝试已知的 Package 路径（直接尝试加载）
            string[] possiblePaths = new string[]
            {
                "Packages/com.nd.vfxtools/Editor/VFXTagPool.json",
                "Assets/VFX Tools/Editor/VFXTagPool.json",
                "Assets/com.nd.vfxtools/Editor/VFXTagPool.json"
            };
            
            foreach (var path in possiblePaths)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null)
                {
                    Debug.Log($"[标签池] 通过预设路径定位: {path}");
                    return path;
                }
            }
            
            // 方案3：通过 asmdef 路径定位（备用）
            string asmdefPath = FindAsmdefPath();
            if (!string.IsNullOrEmpty(asmdefPath))
            {
                string directory = Path.GetDirectoryName(asmdefPath);
                string poolPath = Path.Combine(directory, TagPoolFileName).Replace("\\", "/");
                Debug.Log($"[标签池] 通过 asmdef 定位: {poolPath}");
                return poolPath;
            }
            
            // 方案4：默认 Package 路径
            string defaultPath = $"Packages/com.nd.vfxtools/Editor/{TagPoolFileName}";
            Debug.Log($"[标签池] 使用默认路径: {defaultPath}");
            return defaultPath;
        }

        private static string FindAsmdefPath()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("ND.VFXTools.Editor t:asmdef");
            if (guids != null && guids.Length > 0)
            {
                return UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return null;
        }

        [System.Serializable]
        public class TagPoolData
        {
            public string version;
            public List<TagPoolCategory> categories = new List<TagPoolCategory>();
        }

        [System.Serializable]
        public class TagPoolCategory
        {
            public string name;
            public List<TagPoolTag> tags = new List<TagPoolTag>();

            public TagPoolCategory(string categoryName)
            {
                name = categoryName;
            }
        }

        [System.Serializable]
        public class TagPoolTag
        {
            public string name;
            public string colorHex;

            public TagPoolTag(string tagName, Color color)
            {
                name = tagName;
                colorHex = ColorToHex(color);
            }

            public Color GetColor()
            {
                return HexToColor(colorHex);
            }
        }

        public static TagPoolData GetTagPool()
        {
            if (!_isLoaded)
            {
                LoadTagPool();
            }
            return _cachedData;
        }

        public static void LoadTagPool()
        {
            _isLoaded = true;
            string poolPath = TagPoolPath;
            
            Debug.Log($"[标签池] 查找路径: {poolPath}");
            
            string json = null;
            
            // 优先尝试通过 AssetDatabase 加载（支持 Package Manager 安装的包）
            var textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(poolPath);
            if (textAsset != null)
            {
                json = textAsset.text;
                Debug.Log($"[标签池] 通过 AssetDatabase 加载成功");
            }
            // 备用方案：直接读取文件（支持本地开发环境）
            else if (File.Exists(poolPath))
            {
                json = File.ReadAllText(poolPath);
                Debug.Log($"[标签池] 通过 File.ReadAllText 加载成功");
            }
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"[标签池] 未找到预设标签池文件: {poolPath}，创建空标签池");
                _cachedData = new TagPoolData { version = Version };
                return;
            }

            try
            {
                _cachedData = JsonUtility.FromJson<TagPoolData>(json);
                
                if (_cachedData == null)
                {
                    _cachedData = new TagPoolData { version = Version };
                }
                
                Debug.Log($"[标签池] 已从 {poolPath} 加载：{_cachedData.categories.Count} 个分类，{GetTotalTagCount()} 个标签");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[标签池] 加载失败: {e.Message}");
                _cachedData = new TagPoolData { version = Version };
            }
        }

        public static void SaveTagPool()
        {
            if (_cachedData == null)
            {
                _cachedData = new TagPoolData { version = Version };
            }

            try
            {
                string poolPath = TagPoolPath;
                string directory = Path.GetDirectoryName(poolPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _cachedData.version = Version;
                string json = JsonUtility.ToJson(_cachedData, true);
                File.WriteAllText(poolPath, json);
                
                UnityEditor.AssetDatabase.Refresh();
                Debug.Log($"[标签池] 已保存到 {poolPath}：{_cachedData.categories.Count} 个分类，{GetTotalTagCount()} 个标签");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[标签池] 保存失败: {e.Message}");
            }
        }

        public static void ImportFromExternalJson(string jsonPath)
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                var externalData = JsonUtility.FromJson<ExternalTagPoolData>(json);
                
                if (externalData == null || externalData.categories == null)
                {
                    Debug.LogError("[标签池] 外部JSON格式不正确");
                    return;
                }

                _cachedData = new TagPoolData { version = Version };

                foreach (var extCategory in externalData.categories)
                {
                    if (string.IsNullOrEmpty(extCategory.name)) continue;

                    var category = new TagPoolCategory(extCategory.name);
                    
                    if (extCategory.tags != null)
                    {
                        foreach (var tagName in extCategory.tags)
                        {
                            if (string.IsNullOrEmpty(tagName)) continue;
                            
                            Color randomColor = GenerateRandomColor();
                            category.tags.Add(new TagPoolTag(tagName, randomColor));
                        }
                    }

                    _cachedData.categories.Add(category);
                }

                SaveTagPool();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[标签池] 导入失败: {e.Message}");
            }
        }

        [System.Serializable]
        private class ExternalTagPoolData
        {
            public string version;
            public List<ExternalCategory> categories;
        }

        [System.Serializable]
        private class ExternalCategory
        {
            public string name;
            public List<string> tags;
        }

        public static List<TagPoolCategory> GetCategories()
        {
            return GetTagPool()?.categories ?? new List<TagPoolCategory>();
        }

        public static List<TagPoolTag> GetAllTags()
        {
            var allTags = new List<TagPoolTag>();
            var data = GetTagPool();
            if (data?.categories != null)
            {
                foreach (var category in data.categories)
                {
                    if (category.tags != null)
                    {
                        allTags.AddRange(category.tags);
                    }
                }
            }
            return allTags;
        }

        public static TagPoolTag GetTag(string tagName)
        {
            var data = GetTagPool();
            if (data?.categories != null)
            {
                foreach (var category in data.categories)
                {
                    if (category.tags != null)
                    {
                        var tag = category.tags.FirstOrDefault(t => t.name == tagName);
                        if (tag != null) return tag;
                    }
                }
            }
            return null;
        }

        public static Color GetTagColor(string tagName)
        {
            var tag = GetTag(tagName);
            return tag?.GetColor() ?? Color.gray;
        }

        public static string GetTagCategory(string tagName)
        {
            var data = GetTagPool();
            if (data?.categories != null)
            {
                foreach (var category in data.categories)
                {
                    if (category.tags != null && category.tags.Any(t => t.name == tagName))
                    {
                        return category.name;
                    }
                }
            }
            return "";
        }

        public static List<TagPoolTag> GetTagsByCategory(string categoryName)
        {
            var data = GetTagPool();
            if (data?.categories != null)
            {
                var category = data.categories.FirstOrDefault(c => c.name == categoryName);
                return category?.tags ?? new List<TagPoolTag>();
            }
            return new List<TagPoolTag>();
        }

        public static int GetTotalTagCount()
        {
            int count = 0;
            var data = GetTagPool();
            if (data?.categories != null)
            {
                foreach (var category in data.categories)
                {
                    count += category.tags?.Count ?? 0;
                }
            }
            return count;
        }

        public static void AddCategory(string categoryName)
        {
            var data = GetTagPool();
            if (data.categories.Any(c => c.name == categoryName)) return;
            
            data.categories.Add(new TagPoolCategory(categoryName));
        }

        public static void RemoveCategory(string categoryName)
        {
            var data = GetTagPool();
            var category = data.categories.FirstOrDefault(c => c.name == categoryName);
            if (category?.tags != null)
            {
                foreach (var tag in category.tags)
                {
                    OnTagRemoved?.Invoke(tag.name);
                }
            }
            data.categories.RemoveAll(c => c.name == categoryName);
        }

        public static void RenameCategory(string oldName, string newName)
        {
            var data = GetTagPool();
            var category = data.categories.FirstOrDefault(c => c.name == oldName);
            if (category != null)
            {
                category.name = newName;
            }
        }

        public static void AddTag(string tagName, Color color, string categoryName)
        {
            var data = GetTagPool();
            
            var category = data.categories.FirstOrDefault(c => c.name == categoryName);
            if (category == null)
            {
                category = new TagPoolCategory(categoryName);
                data.categories.Add(category);
            }

            if (!category.tags.Any(t => t.name == tagName))
            {
                category.tags.Add(new TagPoolTag(tagName, color));
            }
        }

        public static void UpdateTag(string oldName, string newName, Color newColor, string newCategory)
        {
            var data = GetTagPool();
            foreach (var category in data.categories)
            {
                category.tags?.RemoveAll(t => t.name == oldName);
            }
            
            if (oldName != newName)
            {
                OnTagRenamed?.Invoke(oldName, newName);
            }
            
            AddTag(newName, newColor, newCategory);
        }

        public static void RemoveTag(string tagName)
        {
            var data = GetTagPool();
            foreach (var category in data.categories)
            {
                category.tags?.RemoveAll(t => t.name == tagName);
            }
            OnTagRemoved?.Invoke(tagName);
        }

        public static void ClearAll()
        {
            _cachedData = new TagPoolData { version = Version };
        }

        private static Color GenerateRandomColor()
        {
            return new Color(
                Random.Range(0.2f, 0.8f),
                Random.Range(0.2f, 0.8f),
                Random.Range(0.2f, 0.8f)
            );
        }

        private static string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 255);
            int g = Mathf.RoundToInt(color.g * 255);
            int b = Mathf.RoundToInt(color.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.gray;
            
            hex = hex.Replace("#", "");
            if (hex.Length != 6) return Color.gray;

            try
            {
                int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f);
            }
            catch
            {
                return Color.gray;
            }
        }
    }
}
