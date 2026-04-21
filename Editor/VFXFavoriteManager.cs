using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VFXTools.Editor
{
    public static class VFXFavoriteManager
    {
        private const string FavoriteLibraryFileName = "VFXFavoriteLibrary.json";
        private const string Version = "1.0.0";

        private static string _libraryPath;
        private static VFXFavoriteLibrary _cachedLibrary;
        private static bool _isLoaded = false;
        private static bool _tagEventSubscribed = false;

        public static System.Action OnLibraryChanged;

        private static string LibraryPath
        {
            get
            {
                if (string.IsNullOrEmpty(_libraryPath))
                {
                    _libraryPath = FindLibraryPath();
                }
                return _libraryPath;
            }
        }

        private static string FindLibraryPath()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
                
                if (packageInfo != null)
                {
                    string libPath = $"{packageInfo.assetPath}/Editor/{FavoriteLibraryFileName}";
                    return libPath;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[精选库] PackageInfo 定位失败: {e.Message}");
            }
            
            string[] possiblePaths = new string[]
            {
                "Packages/com.nd.vfxtools/Editor/VFXFavoriteLibrary.json",
                "Assets/VFX Tools/Editor/VFXFavoriteLibrary.json",
                "Assets/com.nd.vfxtools/Editor/VFXFavoriteLibrary.json"
            };
            
            foreach (var path in possiblePaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null)
                {
                    return path;
                }
            }
            
            string asmdefPath = FindAsmdefPath();
            if (!string.IsNullOrEmpty(asmdefPath))
            {
                string directory = Path.GetDirectoryName(asmdefPath);
                string libPath = Path.Combine(directory, FavoriteLibraryFileName).Replace("\\", "/");
                return libPath;
            }
            
            string defaultPath = $"Packages/com.nd.vfxtools/Editor/{FavoriteLibraryFileName}";
            return defaultPath;
        }

        private static string FindAsmdefPath()
        {
            string[] guids = AssetDatabase.FindAssets("ND.VFXTools.Editor t:asmdef");
            if (guids != null && guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }
            return null;
        }

        public static VFXFavoriteLibrary GetLibrary()
        {
            EnsureTagEventSubscribed();
            if (!_isLoaded)
            {
                LoadLibrary();
            }
            return _cachedLibrary;
        }

        public static void LoadLibrary()
        {
            EnsureTagEventSubscribed();
            _isLoaded = true;
            string libPath = LibraryPath;
            
            Debug.Log($"[精选库] 查找路径: {libPath}");
            
            string json = null;
            
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(libPath);
            if (textAsset != null)
            {
                json = textAsset.text;
                Debug.Log($"[精选库] 通过 AssetDatabase 加载成功");
            }
            else if (File.Exists(libPath))
            {
                json = File.ReadAllText(libPath);
                Debug.Log($"[精选库] 通过 File.ReadAllText 加载成功");
            }
            
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"[精选库] 未找到精选库文件: {libPath}，创建空精选库");
                _cachedLibrary = new VFXFavoriteLibrary();
                return;
            }

            try
            {
                _cachedLibrary = JsonUtility.FromJson<VFXFavoriteLibrary>(json);
                
                if (_cachedLibrary == null)
                {
                    _cachedLibrary = new VFXFavoriteLibrary();
                }
                
                Debug.Log($"[精选库] 已从 {libPath} 加载：{_cachedLibrary.items.Count} 个精选特效");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[精选库] 加载失败: {e.Message}");
                _cachedLibrary = new VFXFavoriteLibrary();
            }
        }

        private static void EnsureTagEventSubscribed()
        {
            if (_tagEventSubscribed) return;
            VFXTagPoolManager.OnTagRenamed += HandleTagRenamed;
            VFXTagPoolManager.OnTagRemoved += HandleTagRemoved;
            _tagEventSubscribed = true;
        }

        private static void HandleTagRenamed(string oldName, string newName)
        {
            int changedCount = RenameTagInAllItems(oldName, newName);
            if (changedCount > 0)
            {
                SaveLibrary();
                Debug.Log($"[精选库] 标签重命名同步完成: {oldName} -> {newName}，影响 {changedCount} 个精选项");
            }
        }

        private static void HandleTagRemoved(string tagName)
        {
            int changedCount = RemoveTagFromAllItems(tagName);
            if (changedCount > 0)
            {
                SaveLibrary();
                Debug.Log($"[精选库] 标签删除同步完成: {tagName}，影响 {changedCount} 个精选项");
            }
        }

        public static void SaveLibrary()
        {
            if (_cachedLibrary == null)
            {
                _cachedLibrary = new VFXFavoriteLibrary();
            }

            try
            {
                string libPath = LibraryPath;
                string directory = Path.GetDirectoryName(libPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(_cachedLibrary, true);
                File.WriteAllText(libPath, json);
                
                AssetDatabase.Refresh();
                Debug.Log($"[精选库] 已保存到 {libPath}：{_cachedLibrary.items.Count} 个精选特效");
                
                OnLibraryChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[精选库] 保存失败: {e.Message}");
            }
        }

        public static List<VFXFavoriteLibrary.ItemData> GetItems()
        {
            return GetLibrary()?.items ?? new List<VFXFavoriteLibrary.ItemData>();
        }

        public static int GetItemCount()
        {
            return GetLibrary()?.items?.Count ?? 0;
        }

        public static bool IsInFavorite(string path)
        {
            var library = GetLibrary();
            if (library?.items == null) return false;
            return library.items.Any(item => item.path == path);
        }

        public static void AddItem(VFXFilterData.FilterItemData filterItem)
        {
            if (filterItem == null || string.IsNullOrEmpty(filterItem.path)) return;
            
            var library = GetLibrary();
            if (library.items.Any(item => item.path == filterItem.path))
            {
                Debug.Log($"[精选库] 特效已存在: {filterItem.name}");
                return;
            }

            var newItem = new VFXFavoriteLibrary.ItemData(
                filterItem.path,
                filterItem.name,
                filterItem.type,
                filterItem.loop,
                filterItem.duration,
                filterItem.tags
            );

            library.items.Add(newItem);
            SaveLibrary();
            
            Debug.Log($"[精选库] 已添加: {filterItem.name}");
        }

        public static void AddItems(List<VFXFilterData.FilterItemData> filterItems)
        {
            if (filterItems == null || filterItems.Count == 0) return;
            
            var library = GetLibrary();
            int addedCount = 0;

            foreach (var filterItem in filterItems)
            {
                if (filterItem == null || string.IsNullOrEmpty(filterItem.path)) continue;
                
                if (library.items.Any(item => item.path == filterItem.path)) continue;

                var newItem = new VFXFavoriteLibrary.ItemData(
                    filterItem.path,
                    filterItem.name,
                    filterItem.type,
                    filterItem.loop,
                    filterItem.duration,
                    filterItem.tags
                );

                library.items.Add(newItem);
                addedCount++;
            }

            if (addedCount > 0)
            {
                SaveLibrary();
                Debug.Log($"[精选库] 已添加 {addedCount} 个特效");
            }
        }

        public static void RemoveItem(string path)
        {
            var library = GetLibrary();
            int removed = library.items.RemoveAll(item => item.path == path);
            
            if (removed > 0)
            {
                SaveLibrary();
                Debug.Log($"[精选库] 已移除: {path}");
            }
        }

        public static void RemoveItems(List<string> paths)
        {
            if (paths == null || paths.Count == 0) return;
            
            var library = GetLibrary();
            int removed = library.items.RemoveAll(item => paths.Contains(item.path));
            
            if (removed > 0)
            {
                SaveLibrary();
                Debug.Log($"[精选库] 已移除 {removed} 个特效");
            }
        }

        public static void ClearAll()
        {
            _cachedLibrary = new VFXFavoriteLibrary();
            SaveLibrary();
            Debug.Log($"[精选库] 已清空所有精选特效");
        }

        public static int CleanupInvalidItems()
        {
            var library = GetLibrary();
            if (library?.items == null) return 0;

            int removedCount = 0;
            var invalidPaths = new List<string>();

            foreach (var item in library.items)
            {
                if (string.IsNullOrEmpty(item.path)) 
                {
                    invalidPaths.Add(item.path);
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.path);
                if (prefab == null)
                {
                    invalidPaths.Add(item.path);
                }
            }

            foreach (var path in invalidPaths)
            {
                library.items.RemoveAll(item => item.path == path);
                removedCount++;
            }

            if (removedCount > 0)
            {
                SaveLibrary();
                Debug.Log($"[精选库] 已清理 {removedCount} 个失效项");
            }

            return removedCount;
        }

        public static void ImportFromExternalJson(string jsonPath)
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                var externalLibrary = JsonUtility.FromJson<VFXFavoriteLibrary>(json);
                
                if (externalLibrary == null || externalLibrary.items == null)
                {
                    Debug.LogError("[精选库] 外部JSON格式不正确");
                    return;
                }

                _cachedLibrary = externalLibrary;
                SaveLibrary();
                
                Debug.Log($"[精选库] 已从外部导入 {externalLibrary.items.Count} 个精选特效");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[精选库] 导入失败: {e.Message}");
            }
        }

        public static void ExportToJson(string exportPath)
        {
            try
            {
                var library = GetLibrary();
                string json = JsonUtility.ToJson(library, true);
                File.WriteAllText(exportPath, json);
                
                Debug.Log($"[精选库] 已导出到 {exportPath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[精选库] 导出失败: {e.Message}");
            }
        }

        public static GameObject GetPrefabByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static VFXFavoriteLibrary.ItemData GetItemByPath(string path)
        {
            var library = GetLibrary();
            return library?.items?.FirstOrDefault(item => item.path == path);
        }

        public static List<VFXFavoriteLibrary.ItemData> FilterByTags(List<string> tags)
        {
            var library = GetLibrary();
            if (library?.items == null || tags == null || tags.Count == 0)
            {
                return library?.items ?? new List<VFXFavoriteLibrary.ItemData>();
            }

            var tagSet = new HashSet<string>(tags);
            return library.items.Where(item => 
                item.tags != null && item.tags.Any(tag => tagSet.Contains(tag))
            ).ToList();
        }

        public static int RenameTagInAllItems(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName) || oldName == newName)
            {
                return 0;
            }

            var library = GetLibrary();
            if (library?.items == null) return 0;

            int changedItems = 0;
            foreach (var item in library.items)
            {
                if (item?.tags == null || item.tags.Count == 0) continue;

                bool changed = false;
                for (int i = 0; i < item.tags.Count; i++)
                {
                    if (item.tags[i] == oldName)
                    {
                        item.tags[i] = newName;
                        changed = true;
                    }
                }

                if (changed)
                {
                    // 避免重命名后重复标签
                    item.tags = item.tags.Distinct().ToList();
                    changedItems++;
                }
            }

            return changedItems;
        }

        public static int RemoveTagFromAllItems(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return 0;
            }

            var library = GetLibrary();
            if (library?.items == null) return 0;

            int changedItems = 0;
            foreach (var item in library.items)
            {
                if (item?.tags == null || item.tags.Count == 0) continue;

                int beforeCount = item.tags.Count;
                item.tags.RemoveAll(tag => tag == tagName);
                if (item.tags.Count != beforeCount)
                {
                    changedItems++;
                }
            }

            return changedItems;
        }

        private const string FilterCachePath = "Assets/VFX Tools/Editor/VFXFilterCache.asset";

        public static void SyncTagsToFilterCache(string itemPath, List<string> tags)
        {
            var filterCache = AssetDatabase.LoadAssetAtPath<VFXFilterData>(FilterCachePath);
            if (filterCache?.items == null) return;

            var filterItem = filterCache.items.FirstOrDefault(i => i.path == itemPath);
            if (filterItem == null) return;

            filterItem.tags = new List<string>(tags);
            EditorUtility.SetDirty(filterCache);
            AssetDatabase.SaveAssets();
            Debug.Log($"[精选库] 已同步标签到扫描库: {itemPath}");
        }
    }
}
