using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace VFXTools.Editor
{
    public class VFXFavoriteManagerWindow : EditorWindow
    {
        private const string ToolVersion = "v0.23.4";
        private const string FilterCachePath = "Assets/VFX Tools/Editor/VFXFilterCache.asset";
        
        private const float ItemHeight = 90f;
        private const float ItemSpacing = 5f;
        private const float TotalItemHeight = ItemHeight + ItemSpacing;
        private const int ItemsPerPage = 200;
        
        private Vector2 _scrollPosition;
        private int _currentPage = 1;
        private List<int> _selectedIndices = new List<int>();
        
        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _selectedStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _dangerButtonStyle;
        private GUIStyle _previewButtonStyle;
        private GUIStyle _tagLabelStyle;
        private bool _stylesInitialized = false;
        
        private string _searchFilter = "";
        private List<VFXFavoriteLibrary.ItemData> _filteredItems = new List<VFXFavoriteLibrary.ItemData>();
        
        private Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
        private List<string> _prefabCacheOrder = new List<string>();
        private const int MaxPrefabCacheSize = 100;
        private Dictionary<Color, Texture2D> _colorTextureCache = new Dictionary<Color, Texture2D>();
        
        private GameObject _previewInstance;
        private List<ParticleSystem> _previewParticleSystems = new List<ParticleSystem>();
        private bool _isPreviewing = false;
        private double _lastUpdateTime;
        private float _previewTime = 0f;
        private const float PreviewLoopDuration = 3.0f;

        [MenuItem("Tools/VFX/精选库管理（开发者）")]
        public static void ShowWindow()
        {
            var window = GetWindow<VFXFavoriteManagerWindow>("精选库管理（开发者）");
            window.minSize = new Vector2(700, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _stylesInitialized = false;
            VFXFavoriteManager.LoadLibrary();
            RefreshFilteredList();
            VFXFavoriteManager.OnLibraryChanged += OnLibraryChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            VFXFavoriteManager.OnLibraryChanged -= OnLibraryChanged;
            EditorApplication.update -= OnEditorUpdate;
            ClearPreview();
            _prefabCache.Clear();
            _prefabCacheOrder.Clear();
            _colorTextureCache.Clear();
        }

        private void OnEditorUpdate()
        {
            UpdatePreview();
            UpdateThumbnailPreview();
        }

        private void UpdateThumbnailPreview()
        {
            if (VFXPreviewUtils.UpdateAllPreviews())
            {
                Repaint();
            }
        }

        private void UpdatePreview()
        {
            if (!_isPreviewing || _previewInstance == null) return;

            if (_previewInstance == null)
            {
                ClearPreview();
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            if (deltaTime > 0.1f) deltaTime = 0.1f;

            _previewTime += deltaTime;

            if (_previewTime > PreviewLoopDuration)
            {
                _previewTime = 0f;
                foreach (var ps in _previewParticleSystems)
                {
                    if (ps != null)
                    {
                        ps.Simulate(0, true, true);
                    }
                }
            }

            bool needsRepaint = false;

            foreach (var ps in _previewParticleSystems)
            {
                if (ps != null)
                {
                    ps.Simulate(deltaTime, true, false);
                    needsRepaint = true;
                }
            }

            if (needsRepaint)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnLibraryChanged()
        {
            RefreshFilteredList(resetScroll: false);
            Repaint();
        }

        private void OnTagsModified()
        {
            RefreshFilteredList(resetScroll: false);
            Repaint();
        }

        private void RefreshFilteredList(bool resetScroll = true)
        {
            var items = VFXFavoriteManager.GetItems();
            
            if (string.IsNullOrEmpty(_searchFilter))
            {
                _filteredItems = new List<VFXFavoriteLibrary.ItemData>(items);
            }
            else
            {
                string filterLower = _searchFilter.ToLower();
                _filteredItems = items.FindAll(item => 
                    item.name.ToLower().Contains(filterLower) ||
                    item.path.ToLower().Contains(filterLower) ||
                    (item.tags != null && item.tags.Exists(t => t.ToLower().Contains(filterLower)))
                );
            }
            
            if (resetScroll)
            {
                _currentPage = 1;
                _scrollPosition = Vector2.zero;
            }
        }

        private void OnGUI()
        {
            EnsureStylesInitialized();
            
            EditorGUILayout.BeginVertical();
            
            DrawHeader();
            DrawToolbar();
            DrawItemList();
            DrawFooter();
            
            EditorGUILayout.EndVertical();
        }

        private void EnsureStylesInitialized()
        {
            if (_stylesInitialized) return;
            
            InitStyles();
            _stylesInitialized = true;
        }

        private void InitStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                margin = new RectOffset(10, 10, 10, 10)
            };
            
            _cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5),
                fixedHeight = ItemHeight
            };
            
            _selectedStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5),
                fixedHeight = ItemHeight
            };
            _selectedStyle.normal.background = GetCachedTexture(new Color(0.2f, 0.4f, 0.6f, 0.3f));
            
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4)
            };
            
            _dangerButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4)
            };
            _dangerButtonStyle.normal.background = GetCachedTexture(new Color(0.6f, 0.2f, 0.2f, 1f));
            _dangerButtonStyle.normal.textColor = Color.white;
            
            _previewButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4)
            };
            _previewButtonStyle.normal.background = GetCachedTexture(new Color(0.2f, 0.5f, 0.3f, 1f));
            _previewButtonStyle.normal.textColor = Color.white;
            
            _tagLabelStyle = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(6, 6, 2, 2),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
        }

        private Texture2D GetCachedTexture(Color color)
        {
            if (!_colorTextureCache.TryGetValue(color, out var texture))
            {
                texture = VFXEditorUtils.MakeTexture(2, 2, color);
                _colorTextureCache[color] = texture;
            }
            return texture;
        }

        private GameObject GetPrefabCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_prefabCache.TryGetValue(path, out var cached))
            {
                if (cached != null)
                {
                    _prefabCacheOrder.Remove(path);
                    _prefabCacheOrder.Add(path);
                    return cached;
                }
                _prefabCache.Remove(path);
                _prefabCacheOrder.Remove(path);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                while (_prefabCacheOrder.Count >= MaxPrefabCacheSize)
                {
                    string oldestKey = _prefabCacheOrder[0];
                    _prefabCacheOrder.RemoveAt(0);
                    _prefabCache.Remove(oldestKey);
                }
                _prefabCache[path] = prefab;
                _prefabCacheOrder.Add(path);
            }
            return prefab;
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"精选库管理（开发者）{ToolVersion}", _headerStyle);
            GUILayout.FlexibleSpace();
            
            int totalCount = VFXFavoriteManager.GetItemCount();
            GUILayout.Label($"共 {totalCount} 个精选特效", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", EditorStyles.boldLabel, GUILayout.Width(50));
            string newFilter = EditorGUILayout.TextField(_searchFilter);
            if (newFilter != _searchFilter)
            {
                _searchFilter = newFilter;
                RefreshFilteredList();
                _selectedIndices.Clear();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("刷新", _buttonStyle, GUILayout.Width(60), GUILayout.Height(24)))
            {
                VFXFavoriteManager.LoadLibrary();
                RefreshFilteredList();
                _selectedIndices.Clear();
                _prefabCache.Clear();
            }
            
            if (GUILayout.Button("清理失效项", _buttonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            {
                int removed = VFXFavoriteManager.CleanupInvalidItems();
                if (removed > 0)
                {
                    EditorUtility.DisplayDialog("清理完成", $"已清理 {removed} 个失效项", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("清理完成", "未发现失效项", "确定");
                }
                RefreshFilteredList();
                _selectedIndices.Clear();
            }

            if (GUILayout.Button("从筛选器添加", _buttonStyle, GUILayout.Width(100), GUILayout.Height(24)))
            {
                AddFromFilterCache();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("导入JSON", _buttonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            {
                string path = EditorUtility.OpenFilePanel("导入精选库", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    VFXFavoriteManager.ImportFromExternalJson(path);
                    RefreshFilteredList();
                    _selectedIndices.Clear();
                }
            }
            
            if (GUILayout.Button("导出JSON", _buttonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            {
                string path = EditorUtility.SaveFilePanel("导出精选库", "", "VFXFavoriteLibrary_Export", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    VFXFavoriteManager.ExportToJson(path);
                }
            }
            
            GUILayout.Space(10);
            
            GUI.enabled = _selectedIndices.Count > 0;
            if (GUILayout.Button($"删除选中 ({_selectedIndices.Count})", _dangerButtonStyle, GUILayout.Width(100), GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("确认删除", 
                    $"确定要删除选中的 {_selectedIndices.Count} 个特效吗？", 
                    "删除", "取消"))
                {
                    var pathsToRemove = new List<string>();
                    foreach (var idx in _selectedIndices)
                    {
                        if (idx >= 0 && idx < _filteredItems.Count)
                        {
                            pathsToRemove.Add(_filteredItems[idx].path);
                        }
                    }
                    VFXFavoriteManager.RemoveItems(pathsToRemove);
                    RefreshFilteredList();
                    _selectedIndices.Clear();
                }
            }
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            GUI.enabled = VFXFavoriteManager.GetItemCount() > 0;
            if (GUILayout.Button("清空全部", _dangerButtonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("确认清空", 
                    "确定要清空所有精选特效吗？\n此操作不可撤销！", 
                    "清空", "取消"))
                {
                    VFXFavoriteManager.ClearAll();
                    RefreshFilteredList();
                    _selectedIndices.Clear();
                }
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private void AddFromFilterCache()
        {
            var filterCache = AssetDatabase.LoadAssetAtPath<VFXFilterData>(FilterCachePath);
            if (filterCache == null || filterCache.items == null || filterCache.items.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到筛选器缓存数据，请先在 VFX Filter 中执行扫描。", "确定");
                return;
            }

            int total = filterCache.items.Count;
            int invalidCount = 0;
            int duplicateCount = 0;
            var toAdd = new List<VFXFilterData.FilterItemData>();

            var existingPaths = new HashSet<string>(VFXFavoriteManager.GetItems().ConvertAll(item => item.path));
            foreach (var item in filterCache.items)
            {
                if (item == null || string.IsNullOrEmpty(item.path) || item.prefab == null)
                {
                    invalidCount++;
                    continue;
                }

                if (existingPaths.Contains(item.path))
                {
                    duplicateCount++;
                    continue;
                }

                existingPaths.Add(item.path);
                toAdd.Add(item);
            }

            if (toAdd.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "导入结果",
                    $"筛选器缓存共 {total} 项。\n可新增 0 项。\n重复 {duplicateCount} 项，失效 {invalidCount} 项。",
                    "确定");
                return;
            }

            VFXFavoriteManager.AddItems(toAdd);
            RefreshFilteredList();
            _selectedIndices.Clear();

            EditorUtility.DisplayDialog(
                "导入完成",
                $"筛选器缓存共 {total} 项。\n成功新增 {toAdd.Count} 项。\n重复 {duplicateCount} 项，失效 {invalidCount} 项。",
                "确定");
        }

        private void DrawItemList()
        {
            EditorGUILayout.BeginVertical();
            
            if (_filteredItems.Count == 0)
            {
                GUILayout.Label("精选库为空\n\n请在 VFX Filter 中扫描特效后，点击「收藏」按钮添加到精选库", 
                    EditorStyles.centeredGreyMiniLabel, 
                    GUILayout.Height(position.height - 200));
            }
            else
            {
                int totalCount = _filteredItems.Count;
                int totalPages = Mathf.CeilToInt((float)totalCount / ItemsPerPage);
                if (totalPages < 1) totalPages = 1;
                
                if (_currentPage > totalPages) _currentPage = totalPages;
                if (_currentPage < 1) _currentPage = 1;
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"共 {totalCount} 个特效 | 第 {_currentPage}/{totalPages} 页", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("(虚拟滚动已启用) | 点击行可多选", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                
                DrawPaginationControls(totalPages, totalCount);
                
                int startIndex = (_currentPage - 1) * ItemsPerPage;
                int endIndex = Mathf.Min(startIndex + ItemsPerPage, totalCount);
                int pageItemCount = endIndex - startIndex;
                
                float viewHeight = position.height - 280f;
                if (viewHeight < 100f) viewHeight = 100f;
                
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(viewHeight));
                
                float totalContentHeight = pageItemCount * TotalItemHeight;
                GUILayout.Space(totalContentHeight);
                
                int firstVisibleIndex = Mathf.FloorToInt(_scrollPosition.y / TotalItemHeight);
                int lastVisibleIndex = Mathf.CeilToInt((_scrollPosition.y + viewHeight) / TotalItemHeight);
                
                firstVisibleIndex = Mathf.Max(0, firstVisibleIndex - 1);
                lastVisibleIndex = Mathf.Min(pageItemCount - 1, lastVisibleIndex + 1);
                
                float topPadding = firstVisibleIndex * TotalItemHeight;
                
                GUILayout.Space(-totalContentHeight);
                GUILayout.Space(topPadding);
                
                for (int i = firstVisibleIndex; i <= lastVisibleIndex && i < pageItemCount; i++)
                {
                    int actualIndex = startIndex + i;
                    DrawItem(actualIndex, _filteredItems[actualIndex]);
                }
                
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawPaginationControls(int totalPages, int totalCount)
        {
            if (totalPages <= 1) return;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUI.enabled = _currentPage > 1;
            if (GUILayout.Button("◀ 首页", _buttonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                _currentPage = 1;
                _scrollPosition = Vector2.zero;
            }
            
            GUI.enabled = _currentPage > 1;
            if (GUILayout.Button("◀ 上一页", _buttonStyle, GUILayout.Width(70), GUILayout.Height(22)))
            {
                _currentPage--;
                _scrollPosition = Vector2.zero;
            }
            
            GUI.enabled = true;
            
            GUILayout.Space(10);
            
            int jumpPage = _currentPage;
            string pageStr = GUILayout.TextField(jumpPage.ToString(), GUILayout.Width(40), GUILayout.Height(22));
            if (int.TryParse(pageStr, out int newPage))
            {
                jumpPage = Mathf.Clamp(newPage, 1, totalPages);
            }
            
            if (GUILayout.Button("跳转", _buttonStyle, GUILayout.Width(40), GUILayout.Height(22)))
            {
                _currentPage = jumpPage;
                _scrollPosition = Vector2.zero;
            }
            
            GUILayout.Space(10);
            
            GUI.enabled = _currentPage < totalPages;
            if (GUILayout.Button("下一页 ▶", _buttonStyle, GUILayout.Width(70), GUILayout.Height(22)))
            {
                _currentPage++;
                _scrollPosition = Vector2.zero;
            }
            
            GUI.enabled = _currentPage < totalPages;
            if (GUILayout.Button("末页 ▶", _buttonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                _currentPage = totalPages;
                _scrollPosition = Vector2.zero;
            }
            
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            GUILayout.Label($"每页 {ItemsPerPage} 项", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawItem(int index, VFXFavoriteLibrary.ItemData item)
        {
            bool isSelected = _selectedIndices.Contains(index);
            GUIStyle style = isSelected ? _selectedStyle : _cardStyle;
            
            EditorGUILayout.BeginHorizontal(style, GUILayout.Height(ItemHeight));
            GUILayout.Space(0);
            Rect itemRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.control || Event.current.command)
                {
                    if (isSelected)
                    {
                        _selectedIndices.Remove(index);
                    }
                    else
                    {
                        _selectedIndices.Add(index);
                    }
                }
                else if (Event.current.shift && _selectedIndices.Count > 0)
                {
                    int lastSelected = _selectedIndices[_selectedIndices.Count - 1];
                    int start = Mathf.Min(lastSelected, index);
                    int end = Mathf.Max(lastSelected, index);
                    for (int i = start; i <= end; i++)
                    {
                        if (!_selectedIndices.Contains(i))
                        {
                            _selectedIndices.Add(i);
                        }
                    }
                }
                else
                {
                    _selectedIndices.Clear();
                    _selectedIndices.Add(index);
                }
                Event.current.Use();
                Repaint();
            }
            
            Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            var prefab = GetPrefabCached(item.path);
            if (prefab != null)
            {
                VFXPreviewUtils.DrawDynamicThumbnail(previewRect, prefab, item.path, forceDynamic: false);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.3f, 0.3f, 0.3f, 1f));
                GUI.Label(previewRect, "失效", new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
            }
            
            EditorGUILayout.BeginVertical();
            
            GUILayout.Label(new GUIContent(item.name, item.path), EditorStyles.boldLabel);
            GUILayout.Label($"类型: {item.type} | 循环: {(item.loop ? "是" : "否")} | 时长: {item.duration:F1}s", EditorStyles.miniLabel);
            
            if (item.tags != null && item.tags.Count > 0)
            {
                float availableWidth = EditorGUIUtility.currentViewWidth - 280f;
                float tagSpacing = 4f;
                float currentLineWidth = 0f;
                float tagHeight = 18f;
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(0);
                
                for (int i = 0; i < item.tags.Count; i++)
                {
                    string tagName = item.tags[i];
                    Color tagColor = VFXTagPoolManager.GetTagColor(tagName);
                    
                    _tagLabelStyle.normal.background = GetCachedTexture(tagColor);
                    _tagLabelStyle.normal.textColor = VFXEditorUtils.GetContrastColor(tagColor);
                    
                    Vector2 tagSize = _tagLabelStyle.CalcSize(new GUIContent(tagName));
                    float tagWidth = tagSize.x + 8f;
                    
                    if (currentLineWidth + tagWidth > availableWidth && currentLineWidth > 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(0);
                        currentLineWidth = 0f;
                    }
                    
                    GUILayout.Box(tagName, _tagLabelStyle, GUILayout.Height(tagHeight), GUILayout.Width(tagWidth));
                    currentLineWidth += tagWidth + tagSpacing;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("定位", "在项目视图中定位此预制体"), _buttonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                if (prefab != null)
                {
                    EditorGUIUtility.PingObject(prefab);
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "预制体已失效或被移动", "确定");
                }
            }
            
            if (GUILayout.Button(new GUIContent("预览", "在SceneView中预览此特效"), _previewButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                StartPreview(item, prefab);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("添加到场景", "将此预制体实例化到当前场景"), _buttonStyle, GUILayout.Width(80), GUILayout.Height(22)))
            {
                AddToHierarchy(item, prefab);
            }
            
            if (GUILayout.Button(new GUIContent("标签", "编辑此精选特效的标签"), _buttonStyle, GUILayout.Width(80), GUILayout.Height(22)))
            {
                VFXTagEditorWindow.ShowWindow(item, OnTagsModified);
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("删除", "从精选库中移除此特效"), _dangerButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除「{item.name}」吗？", "删除", "取消"))
                {
                    VFXFavoriteManager.RemoveItem(item.path);
                    RefreshFilteredList();
                    _selectedIndices.Remove(index);
                }
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }

        private void StartPreview(VFXFavoriteLibrary.ItemData item, GameObject prefab)
        {
            ClearPreview();

            if (SceneView.lastActiveSceneView == null)
            {
                EditorUtility.DisplayDialog("提示", "请先打开 SceneView 窗口", "确定");
                return;
            }
            
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("提示", "预制体已失效或被移动", "确定");
                return;
            }

            _previewInstance = Instantiate(prefab);
            _previewInstance.hideFlags = HideFlags.HideAndDontSave;
            _previewInstance.name = $"[PREVIEW] {item.name}";
            
            _previewInstance.transform.position = Vector3.zero;
            _previewInstance.transform.rotation = Quaternion.identity;

            _previewParticleSystems.Clear();
            _previewInstance.GetComponentsInChildren<ParticleSystem>(true, _previewParticleSystems);

            foreach (var ps in _previewParticleSystems)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                    ps.time = 0f;
                }
            }

            _previewInstance.SetActive(true);

            foreach (var ps in _previewParticleSystems)
            {
                if (ps != null)
                {
                    ps.Simulate(0, true, true);
                }
            }

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            Renderer[] renderers = _previewInstance.GetComponentsInChildren<Renderer>();
            bool hasRenderer = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    bounds.Encapsulate(renderer.bounds);
                    hasRenderer = true;
                }
            }
            
            if (!hasRenderer)
            {
                bounds.size = Vector3.one * 2f;
            }

            float maxBound = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxBound < 0.1f) maxBound = 2f;
            
            float cameraDistance = maxBound * 0.5f;
            Vector3 cameraOffset = new Vector3(1f, 1f, -1f).normalized * cameraDistance;
            
            SceneView.lastActiveSceneView.LookAt(bounds.center, Quaternion.LookRotation(-cameraOffset.normalized), cameraDistance);

            SceneView.lastActiveSceneView.ShowNotification(new GUIContent($"预览中: {item.name}"));
            
            _isPreviewing = true;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            _previewTime = 0f;
            
            VFXPreviewCoordinator.NotifyPreviewStarted(this);
        }

        private void ClearPreview()
        {
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
                _previewInstance = null;
            }
            _previewParticleSystems.Clear();
            _isPreviewing = false;
            _previewTime = 0f;
        }

        private void AddToHierarchy(VFXFavoriteLibrary.ItemData item, GameObject prefab)
        {
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("提示", "预制体已失效或被移动", "确定");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                Camera sceneCam = SceneView.lastActiveSceneView.camera;
                instance.transform.position = sceneCam.transform.position + sceneCam.transform.forward * 5f;
            }
            
            Selection.activeGameObject = instance;
            Undo.RegisterCreatedObjectUndo(instance, $"Add {item.name} to Hierarchy");
            
            Debug.Log($"[精选库管理] 已添加 '{item.name}' 到 Hierarchy");
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("提示: 可使用「从筛选器添加」按钮批量导入 VFX Filter 扫描缓存", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
    }
}
