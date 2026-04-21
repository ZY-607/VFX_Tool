using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VFXTools.Editor.Analyzer;

namespace VFXTools.Editor
{
    public class VFXFilterWindow : EditorWindow
    {
        private const string ToolVersion = "v0.22.5";
        private const string LibraryCachePath = "Assets/VFX Tools/Editor/VFXLibraryCache.asset";
        private const string FilterCachePath = "Assets/VFX Tools/Editor/VFXFilterCache.asset";
        private const string ScanPathPrefsKey = "VFXFilter_ScanPath";
        private const string ExcludeHtSuffixPrefsKey = "VFXFilter_ExcludeHtSuffix";
        
        private const float ItemHeight = 90f;
        private const float ItemSpacing = 5f;
        private const float TotalItemHeight = ItemHeight + ItemSpacing;
        private const int ItemsPerPage = 500;
        
        private List<VFXFilterData.FilterItemData> _filterLibrary = new List<VFXFilterData.FilterItemData>();
        private VFXFilterData _filterCache;
        
        private Vector2 _scrollPosLibrary;
        private string _scanPath = "Assets/VFX Tools";
        private List<string> _activeFilters = new List<string>();
        private bool _excludeHtSuffix = false;
        
        private List<VFXFilterData.FilterItemData> _filteredListCache;
        private int _lastFilterHash = -1;
        
        private List<VFXFilterData.FilterItemData> _excludeHtFilterCache;
        private int _lastExcludeHtHash = -1;
        
        private GameObject _previewInstance;
        private List<ParticleSystem> _previewParticleSystems = new List<ParticleSystem>();
        private bool _isPreviewing = false;
        private double _lastUpdateTime;
        private VFXFilterData.FilterItemData _currentPreviewItem;
        private float _previewTime = 0f;
        private const float PreviewLoopDuration = 3.0f;
        
        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _tagLabelStyle;
        private GUIStyle _activeTagButtonStyle;
        private GUIStyle _inactiveTagButtonStyle;
        private GUIStyle _actionButtonStyle;
        private GUIStyle _scanButtonStyle;
        private GUIStyle _analyzerButtonStyle;
        private GUIStyle _favoriteButtonStyle;
        
        private Dictionary<Color, Texture2D> _colorTextureCache = new Dictionary<Color, Texture2D>();
        
        private Rect _lastScrollAreaRect;
        private bool _stylesInitialized = false;
        
        private bool _showFavoriteLibrary = true;
        private List<VFXFavoriteLibrary.ItemData> _favoriteListCache;
        private Vector2 _scrollPosFavorite;
        private List<VFXFavoriteLibrary.ItemData> _filteredFavoriteCache;
        private int _lastFavoriteFilterHash = -1;
        private Dictionary<string, GameObject> _favoritePrefabCache = new Dictionary<string, GameObject>();
        private List<string> _favoritePrefabCacheOrder = new List<string>();
        private const int MaxFavoritePrefabCacheSize = 100;
        
        private HashSet<string> _selectedItems = new HashSet<string>();
        
        private int _scanCurrentPage = 1;
        private int _favoriteCurrentPage = 1;

        private static readonly Dictionary<string, string[]> TagKeywordAliases = new Dictionary<string, string[]>
        {
            ["技能特效"] = new[] { "技能", "skill", "skills", "ability", "abilities", "spell", "magic", "combat" },
            ["UI特效"] = new[] { "ui", "hud", "interface", "screen", "panel", "button", "icon", "widget" },
            ["场景特效"] = new[] { "场景", "scene", "scenes", "environment", "env", "level", "map", "terrain", "world" },
            ["常态特效"] = new[] { "常态", "idle", "ambient", "aura", "persistent", "loop_idle", "standby" },
            ["通用特效"] = new[] { "通用", "common", "generic", "shared", "public", "universal", "general" },
            ["拖尾"] = new[] { "拖尾", "trail", "tracer", "ribbon" },
            ["弹道"] = new[] { "弹道", "projectile", "bullet", "missile", "shot", "beam", "ray" },
            ["受击"] = new[] { "受击", "hit", "impact", "hurt", "strike" },
            ["消散"] = new[] { "消散", "fade", "dissolve", "destroy", "despawn", "die" },
            ["护盾"] = new[] { "护盾", "shield", "barrier" },
            ["预警"] = new[] { "预警", "warning", "telegraph", "indicator" },
            ["攻击"] = new[] { "攻击", "attack", "atk", "slash", "burst", "shoot" },
            ["治疗"] = new[] { "治疗", "heal", "cure", "recover" },
            ["BUFF"] = new[] { "buff", "boost", "aura" },
            ["DEBUFF"] = new[] { "debuff", "curse", "weaken", "poison" },
            ["界面提示"] = new[] { "界面提示", "notice", "prompt", "tooltip", "tip" },
            ["点击反馈"] = new[] { "点击反馈", "click", "tap", "press", "hover", "select" },
            ["装饰点缀"] = new[] { "装饰", "点缀", "decoration", "deco", "ornament", "embellish" },
            ["单体"] = new[] { "单体", "single", "target" },
            ["群体"] = new[] { "群体", "group", "multi", "aoe", "area" },
            ["直线"] = new[] { "直线", "line", "beam", "laser" },
            ["圆形"] = new[] { "圆形", "circle", "radial", "sphere" },
            ["环形"] = new[] { "环形", "ring", "halo", "donut" },
            ["扇形"] = new[] { "扇形", "fan", "cone", "sector" },
            ["屏幕效果"] = new[] { "屏幕", "screenfx", "fullscreen", "overlay" },
            ["氛围效果"] = new[] { "氛围", "ambient", "atmosphere", "mood" },
            ["过场效果"] = new[] { "过场", "transition", "cutscene", "intro", "outro" },
            ["火"] = new[] { "火", "fire", "flame", "burn" },
            ["水"] = new[] { "水", "water", "splash", "wave" },
            ["风"] = new[] { "风", "wind", "air", "gust" },
            ["土"] = new[] { "土", "earth", "rock", "stone", "dust" },
            ["电"] = new[] { "电", "lightning", "electric", "thunder" },
            ["冰"] = new[] { "冰", "ice", "frost", "snow" },
            ["毒"] = new[] { "毒", "poison", "venom", "toxic" },
            ["黑暗系"] = new[] { "黑暗", "dark", "shadow", "void" },
            ["科技"] = new[] { "科技", "tech", "scifi", "sci-fi", "cyber", "energy" },
            ["文字"] = new[] { "文字", "text", "word" },
            ["数字"] = new[] { "数字", "number", "damage" },
            ["烟花"] = new[] { "烟花", "firework", "celebrate", "celebration" },
            ["星空"] = new[] { "星空", "star", "stars", "galaxy", "nebula" },
            ["法阵"] = new[] { "法阵", "magiccircle", "magic_circle", "sigil", "glyph" },
            ["瞬发"] = new[] { "瞬发", "instant", "burst", "oneshot", "one_shot" },
            ["持续"] = new[] { "持续", "sustain", "persistent", "duration", "channel" },
            ["收尾"] = new[] { "收尾", "end", "finish", "ending", "outro" }
        };

        [MenuItem("Tools/VFX/VFX Filter")]
        public static void ShowWindow()
        {
            var window = GetWindow<VFXFilterWindow>("VFX Filter");
            window.minSize = new Vector2(500, 400);
        }

        private void OnEnable()
        {
            _stylesInitialized = false;
            LoadScanPath();
            LoadExcludeHtSuffixSetting();
            LoadFilterCache();
            InvalidateFilterCache();
            LoadFavoriteLibrary();
            
            EditorApplication.update += OnEditorUpdate;
            
            VFXTagPoolManager.OnTagRenamed += OnTagRenamed;
            VFXTagPoolManager.OnTagRemoved += OnTagRemoved;
            VFXPreviewCoordinator.OnPreviewStarted += OnExternalPreviewStarted;
            VFXAnalyzerWindow.OnAnalysisResultApplied += OnAnalysisResultApplied;
            VFXFavoriteManager.OnLibraryChanged += OnFavoriteLibraryChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            ClearPreview();
            
            VFXPreviewUtils.CleanupAll();
            
            VFXTagPoolManager.OnTagRenamed -= OnTagRenamed;
            VFXTagPoolManager.OnTagRemoved -= OnTagRemoved;
            VFXPreviewCoordinator.OnPreviewStarted -= OnExternalPreviewStarted;
            VFXAnalyzerWindow.OnAnalysisResultApplied -= OnAnalysisResultApplied;
            VFXFavoriteManager.OnLibraryChanged -= OnFavoriteLibraryChanged;
            
            _colorTextureCache.Clear();
            _favoritePrefabCache.Clear();
            _favoritePrefabCacheOrder.Clear();
        }
        
        private void OnExternalPreviewStarted(object sender)
        {
            if (sender != this && _isPreviewing)
            {
                ClearPreview();
            }
        }
        
        private void OnTagRenamed(string oldName, string newName)
        {
            if (_filterCache != null)
            {
                _filterCache.RenameTagInAllItems(oldName, newName);
                EditorUtility.SetDirty(_filterCache);
                AssetDatabase.SaveAssets();
                LoadFilterCache();
                InvalidateFilterCache();
                Repaint();
                Debug.Log($"[VFX Filter] 标签已同步重命名: {oldName} -> {newName}");
            }
        }
        
        private void OnTagRemoved(string tagName)
        {
            if (_filterCache != null)
            {
                _filterCache.RemoveTagFromAllItems(tagName);
                EditorUtility.SetDirty(_filterCache);
                AssetDatabase.SaveAssets();
                LoadFilterCache();
                InvalidateFilterCache();
                Repaint();
                Debug.Log($"[VFX Filter] 标签已同步删除: {tagName}");
            }
        }
        
        private void OnTagsModified()
        {
            LoadFilterCache();
            InvalidateFilterCache();
            Repaint();
        }

        private void LoadFavoriteLibrary()
        {
            VFXFavoriteManager.LoadLibrary();
            _favoriteListCache = VFXFavoriteManager.GetItems();
        }

        private void OnFavoriteLibraryChanged()
        {
            LoadFavoriteLibrary();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            UpdateMainPreview();
            UpdateThumbnailPreview();
        }

        private void UpdateMainPreview()
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

        private void UpdateThumbnailPreview()
        {
            if (VFXPreviewUtils.UpdateAllPreviews())
            {
                Repaint();
            }
        }

        private void LoadScanPath()
        {
            if (EditorPrefs.HasKey(ScanPathPrefsKey))
            {
                _scanPath = EditorPrefs.GetString(ScanPathPrefsKey);
            }
        }

        private void SaveScanPath()
        {
            EditorPrefs.SetString(ScanPathPrefsKey, _scanPath);
        }

        private void LoadExcludeHtSuffixSetting()
        {
            if (EditorPrefs.HasKey(ExcludeHtSuffixPrefsKey))
            {
                _excludeHtSuffix = EditorPrefs.GetBool(ExcludeHtSuffixPrefsKey);
            }
        }

        private void SaveExcludeHtSuffixSetting()
        {
            EditorPrefs.SetBool(ExcludeHtSuffixPrefsKey, _excludeHtSuffix);
        }

        private void LoadFilterCache()
        {
            _filterCache = AssetDatabase.LoadAssetAtPath<VFXFilterData>(FilterCachePath);
            if (_filterCache != null && _filterCache.items != null)
            {
                _filterLibrary = new List<VFXFilterData.FilterItemData>(_filterCache.items);
                _filterLibrary.RemoveAll(x => x.prefab == null);
            }
        }

        private void SaveFilterCache()
        {
            if (_filterCache == null)
            {
                _filterCache = ScriptableObject.CreateInstance<VFXFilterData>();
                string directory = Path.GetDirectoryName(FilterCachePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                AssetDatabase.CreateAsset(_filterCache, FilterCachePath);
            }

            _filterCache.items = new List<VFXFilterData.FilterItemData>(_filterLibrary);
            EditorUtility.SetDirty(_filterCache);
            AssetDatabase.SaveAssets();
        }

        private void OnAnalysisResultApplied(List<string> matchedTags)
        {
            _activeFilters = new List<string>(matchedTags);
            InvalidateFilterCache();
            Repaint();
            Debug.Log($"[VFX Filter] 已应用分析器筛选: {string.Join(", ", matchedTags)}");
        }

        private void OnGUI()
        {
            EnsureStylesInitialized();

            EditorGUILayout.BeginVertical();
            
            DrawHeader();
            
            DrawLibraryArea();

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
                margin = new RectOffset(10, 10, 10, 10),
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            
            _cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(5, 5, 5, 5),
                fixedHeight = ItemHeight
            };
            
            _tagLabelStyle = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(6, 6, 2, 2),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            };
            
            _activeTagButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 4, 4),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                border = new RectOffset(1, 1, 1, 1),
                fixedHeight = 26
            };
            _activeTagButtonStyle.normal.background = GetCachedTexture(new Color(0.12f, 0.12f, 0.12f, 1f));
            _activeTagButtonStyle.normal.textColor = Color.white;
            
            _inactiveTagButtonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 4, 4),
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                border = new RectOffset(1, 1, 1, 1),
                fixedHeight = 26
            };
            _inactiveTagButtonStyle.normal.background = GetCachedTexture(new Color(0.2f, 0.2f, 0.2f, 1f));
            _inactiveTagButtonStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            
            _actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            
            Color scanButtonColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            _scanButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(15, 15, 4, 4)
            };
            _scanButtonStyle.normal.background = GetCachedTexture(scanButtonColor);
            _scanButtonStyle.normal.textColor = Color.white;
            
            _analyzerButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            _analyzerButtonStyle.normal.background = GetCachedTexture(scanButtonColor);
            _analyzerButtonStyle.normal.textColor = Color.white;
            
            Color favoriteButtonColor = new Color(0.8f, 0.5f, 0.2f, 1f);
            _favoriteButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            _favoriteButtonStyle.normal.background = GetCachedTexture(favoriteButtonColor);
            _favoriteButtonStyle.normal.textColor = Color.white;
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

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"VFX Filter (特效筛选器) {ToolVersion}", _headerStyle);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("需求分析器", _analyzerButtonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            {
                VFXAnalyzerWindow.ShowWindowFromCaller("Filter");
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLibraryArea()
        {
            EditorGUILayout.BeginVertical();
            
            DrawViewModeToggle();
            
            if (_showFavoriteLibrary)
            {
                DrawFavoriteLibraryView();
            }
            else
            {
                DrawScanLibraryView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawViewModeToggle()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUIStyle activeFavoriteStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            activeFavoriteStyle.normal.background = GetCachedTexture(new Color(0.8f, 0.5f, 0.2f, 1f));
            activeFavoriteStyle.normal.textColor = Color.white;
            
            GUIStyle inactiveFavoriteStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(10, 10, 4, 4)
            };
            inactiveFavoriteStyle.normal.background = GetCachedTexture(new Color(0.37f, 0.37f, 0.35f, 1f));
            inactiveFavoriteStyle.normal.textColor = Color.white;
            
            GUIStyle activeScanStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            activeScanStyle.normal.background = GetCachedTexture(new Color(0.2f, 0.5f, 0.8f, 1f));
            activeScanStyle.normal.textColor = Color.white;
            
            GUIStyle inactiveScanStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(10, 10, 4, 4)
            };
            inactiveScanStyle.normal.background = GetCachedTexture(new Color(0.35f, 0.37f, 0.37f, 1f));
            inactiveScanStyle.normal.textColor = Color.white;
            
            if (GUILayout.Button("精选特效库", _showFavoriteLibrary ? activeFavoriteStyle : inactiveFavoriteStyle, GUILayout.Width(90), GUILayout.Height(28)))
            {
                if (!_showFavoriteLibrary)
                {
                    _showFavoriteLibrary = true;
                    LoadFavoriteLibrary();
                }
            }
            
            if (GUILayout.Button("扫描库", !_showFavoriteLibrary ? activeScanStyle : inactiveScanStyle, GUILayout.Width(70), GUILayout.Height(28)))
            {
                if (_showFavoriteLibrary)
                {
                    _showFavoriteLibrary = false;
                }
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawPaginationControls(ref int currentPage, int totalPages, int totalCount)
        {
            if (totalPages <= 1) return;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            
            GUI.enabled = currentPage > 1;
            if (GUILayout.Button("◀ 首页", GUILayout.Width(60), GUILayout.Height(22)))
            {
                currentPage = 1;
            }
            
            GUI.enabled = currentPage > 1;
            if (GUILayout.Button("◀ 上一页", GUILayout.Width(70), GUILayout.Height(22)))
            {
                currentPage--;
            }
            
            GUI.enabled = true;
            
            GUILayout.Space(10);
            
            int jumpPage = currentPage;
            string pageStr = GUILayout.TextField(jumpPage.ToString(), GUILayout.Width(40), GUILayout.Height(22));
            if (int.TryParse(pageStr, out int newPage))
            {
                jumpPage = Mathf.Clamp(newPage, 1, totalPages);
            }
            
            if (GUILayout.Button("跳转", GUILayout.Width(40), GUILayout.Height(22)))
            {
                currentPage = jumpPage;
            }
            
            GUILayout.Space(10);
            
            GUI.enabled = currentPage < totalPages;
            if (GUILayout.Button("下一页 ▶", GUILayout.Width(70), GUILayout.Height(22)))
            {
                currentPage++;
            }
            
            GUI.enabled = currentPage < totalPages;
            if (GUILayout.Button("末页 ▶", GUILayout.Width(60), GUILayout.Height(22)))
            {
                currentPage = totalPages;
            }
            
            GUI.enabled = true;
            
            GUILayout.FlexibleSpace();
            
            GUILayout.Label($"每页 {ItemsPerPage} 项", EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFavoriteLibraryView()
        {
            GUILayout.Label("精选特效库 ⭐", _headerStyle);
            DrawFilterBar();

            var filteredFavorites = GetFilteredFavoriteList();
            int totalCount = filteredFavorites.Count;
            int totalPages = Mathf.CeilToInt((float)totalCount / ItemsPerPage);
            if (totalPages < 1) totalPages = 1;
            
            if (_favoriteCurrentPage > totalPages) _favoriteCurrentPage = totalPages;
            if (_favoriteCurrentPage < 1) _favoriteCurrentPage = 1;
            
            if (totalCount == 0)
            {
                _scrollPosFavorite = EditorGUILayout.BeginScrollView(_scrollPosFavorite);
                GUILayout.Label("未找到符合筛选条件的精选特效。\n可清除筛选或联系开发者补充精选库。", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(100));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"共 {totalCount} 个精选特效 | 第 {_favoriteCurrentPage}/{totalPages} 页", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                DrawPaginationControls(ref _favoriteCurrentPage, totalPages, totalCount);
                
                int startIndex = (_favoriteCurrentPage - 1) * ItemsPerPage;
                int endIndex = Mathf.Min(startIndex + ItemsPerPage, totalCount);
                int pageItemCount = endIndex - startIndex;
                
                float viewHeight = position.height - 280f;
                if (viewHeight < 100f) viewHeight = 100f;
                
                _scrollPosFavorite = EditorGUILayout.BeginScrollView(_scrollPosFavorite, GUILayout.Height(viewHeight));
                
                float totalContentHeight = pageItemCount * TotalItemHeight;
                GUILayout.Space(totalContentHeight);
                
                int firstVisibleIndex = Mathf.FloorToInt(_scrollPosFavorite.y / TotalItemHeight);
                int lastVisibleIndex = Mathf.CeilToInt((_scrollPosFavorite.y + viewHeight) / TotalItemHeight);
                
                firstVisibleIndex = Mathf.Max(0, firstVisibleIndex - 1);
                lastVisibleIndex = Mathf.Min(pageItemCount - 1, lastVisibleIndex + 1);
                
                float topPadding = firstVisibleIndex * TotalItemHeight;
                
                GUILayout.Space(-totalContentHeight);
                GUILayout.Space(topPadding);
                
                for (int i = firstVisibleIndex; i <= lastVisibleIndex && i < pageItemCount; i++)
                {
                    int actualIndex = startIndex + i;
                    DrawFavoriteItem(filteredFavorites[actualIndex]);
                }
                
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawFavoriteItem(VFXFavoriteLibrary.ItemData item)
        {
            EditorGUILayout.BeginHorizontal(_cardStyle, GUILayout.Height(ItemHeight));
            
            Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            var prefab = GetFavoritePrefabCached(item.path);
            if (prefab != null)
            {
                VFXPreviewUtils.DrawDynamicThumbnail(previewRect, prefab, item.path, forceDynamic: true);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.3f, 0.3f, 0.3f, 1f));
                GUI.Label(previewRect, "失效", new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(item.name, $"路径: {item.path}"), EditorStyles.boldLabel);
            GUILayout.Label($"类型: {item.type} | 循环: {(item.loop ? "是" : "否")} | 时长: {item.duration:F1}s | 标签数: {item.tags?.Count ?? 0}", EditorStyles.miniLabel);
            
            if (item.tags != null && item.tags.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                int tagCount = Mathf.Min(item.tags.Count, 5);
                for (int i = 0; i < tagCount; i++)
                {
                    string tagName = item.tags[i];
                    Color tagColor = VFXTagPoolManager.GetTagColor(tagName);
                    
                    _tagLabelStyle.normal.background = GetCachedTexture(tagColor);
                    _tagLabelStyle.normal.textColor = VFXEditorUtils.GetContrastColor(tagColor);
                    
                    GUILayout.Box(tagName, _tagLabelStyle, GUILayout.Height(18));
                }
                if (item.tags.Count > 5)
                {
                    GUILayout.Label($"...", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("定位", "在项目视图中定位此预制体"), _actionButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
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
            
            if (GUILayout.Button(new GUIContent("预览", "在SceneView中预览此特效"), _actionButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                StartPreviewFromFavorite(item);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("添加到场景", "将此预制体实例化到当前场景"), _actionButtonStyle, GUILayout.Width(80), GUILayout.Height(22)))
            {
                AddToHierarchyFromFavorite(item);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void StartPreviewFromFavorite(VFXFavoriteLibrary.ItemData item)
        {
            var prefab = GetFavoritePrefabCached(item.path);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("提示", "预制体已失效或被移动", "确定");
                return;
            }

            var filterItem = new VFXFilterData.FilterItemData
            {
                path = item.path,
                name = item.name,
                prefab = prefab,
                type = item.type,
                tags = item.tags,
                loop = item.loop,
                duration = item.duration
            };
            StartPreview(filterItem);
        }

        private void AddToHierarchyFromFavorite(VFXFavoriteLibrary.ItemData item)
        {
            var prefab = GetFavoritePrefabCached(item.path);
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
            
            Debug.Log($"[VFX Filter] 已添加 '{item.name}' 到 Hierarchy");
        }

        private void AddToComposerFromFavorite(VFXFavoriteLibrary.ItemData item)
        {
            var prefab = GetFavoritePrefabCached(item.path);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("提示", "预制体已失效或被移动", "确定");
                return;
            }

            VFXComposerWindow.AddVFXFromFilter(prefab, item.path, item.name);
            Debug.Log($"[VFX Filter] 已添加到组合器: {item.name}");
        }

        private void DrawScanLibraryView()
        {
            GUILayout.Label("Library Browser (完整特效库)", _headerStyle);

            DrawScanToolbar();
            DrawFilterBar();

            var filteredList = GetFilteredListCached();
            int totalCount = filteredList.Count;
            int totalPages = Mathf.CeilToInt((float)totalCount / ItemsPerPage);
            if (totalPages < 1) totalPages = 1;
            
            if (_scanCurrentPage > totalPages) _scanCurrentPage = totalPages;
            if (_scanCurrentPage < 1) _scanCurrentPage = 1;

            if (totalCount == 0)
            {
                _scrollPosLibrary = EditorGUILayout.BeginScrollView(_scrollPosLibrary);
                GUILayout.Label("未找到特效资源。请点击 '扫描' 或清空筛选器。\nNo assets found. Click '扫描' or clear filters.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"共 {totalCount} 个特效 | 第 {_scanCurrentPage}/{totalPages} 页", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                
                int selectedCount = _selectedItems.Count;
                if (selectedCount > 0)
                {
                    if (GUILayout.Button(new GUIContent($"收藏选中项 ({selectedCount})", "将选中的特效添加到精选库"), _favoriteButtonStyle, GUILayout.Width(110), GUILayout.Height(22)))
                    {
                        AddSelectedToFavorites(filteredList);
                    }
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button(new GUIContent("收藏选中项 (0)", "请先选中要收藏的特效"), _actionButtonStyle, GUILayout.Width(110), GUILayout.Height(22));
                    GUI.enabled = true;
                }
                
                EditorGUILayout.EndHorizontal();
                
                DrawPaginationControls(ref _scanCurrentPage, totalPages, totalCount);
                
                int startIndex = (_scanCurrentPage - 1) * ItemsPerPage;
                int endIndex = Mathf.Min(startIndex + ItemsPerPage, totalCount);
                int pageItemCount = endIndex - startIndex;
                
                float viewHeight = position.height - 280f;
                if (viewHeight < 100f) viewHeight = 100f;
                
                _scrollPosLibrary = EditorGUILayout.BeginScrollView(_scrollPosLibrary, GUILayout.Height(viewHeight));
                
                float totalContentHeight = pageItemCount * TotalItemHeight;
                GUILayout.Space(totalContentHeight);
                
                int firstVisibleIndex = Mathf.FloorToInt(_scrollPosLibrary.y / TotalItemHeight);
                int lastVisibleIndex = Mathf.CeilToInt((_scrollPosLibrary.y + viewHeight) / TotalItemHeight);
                
                firstVisibleIndex = Mathf.Max(0, firstVisibleIndex - 1);
                lastVisibleIndex = Mathf.Min(pageItemCount - 1, lastVisibleIndex + 1);
                
                float topPadding = firstVisibleIndex * TotalItemHeight;
                
                GUILayout.Space(-totalContentHeight);
                GUILayout.Space(topPadding);
                
                for (int i = firstVisibleIndex; i <= lastVisibleIndex && i < pageItemCount; i++)
                {
                    int actualIndex = startIndex + i;
                    DrawFilterItem(filteredList[actualIndex]);
                }
                
                EditorGUILayout.EndScrollView();
            }
            
        }

        private List<VFXFavoriteLibrary.ItemData> GetFilteredFavoriteList()
        {
            if (_favoriteListCache == null)
            {
                return new List<VFXFavoriteLibrary.ItemData>();
            }

            int currentHash = ComputeFavoriteFilterHash();
            if (_filteredFavoriteCache != null && _lastFavoriteFilterHash == currentHash)
            {
                return _filteredFavoriteCache;
            }

            if (_activeFilters == null || _activeFilters.Count == 0)
            {
                _filteredFavoriteCache = _favoriteListCache;
            }
            else
            {
                var activeFilterSet = new HashSet<string>(_activeFilters);
                var result = new List<VFXFavoriteLibrary.ItemData>();
                for (int i = 0; i < _favoriteListCache.Count; i++)
                {
                    var item = _favoriteListCache[i];
                    if (item?.tags == null) continue;
                    for (int j = 0; j < item.tags.Count; j++)
                    {
                        if (activeFilterSet.Contains(item.tags[j]))
                        {
                            result.Add(item);
                            break;
                        }
                    }
                }
                _filteredFavoriteCache = result;
            }

            _lastFavoriteFilterHash = currentHash;
            return _filteredFavoriteCache;
        }

        private int ComputeFavoriteFilterHash()
        {
            int hash = 17;
            foreach (var filter in _activeFilters)
            {
                hash = hash * 31 + filter.GetHashCode();
            }
            hash = hash * 31 + (_favoriteListCache?.Count ?? 0).GetHashCode();
            return hash;
        }

        private GameObject GetFavoritePrefabCached(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_favoritePrefabCache.TryGetValue(path, out var cached))
            {
                if (cached != null)
                {
                    _favoritePrefabCacheOrder.Remove(path);
                    _favoritePrefabCacheOrder.Add(path);
                    return cached;
                }
                _favoritePrefabCache.Remove(path);
                _favoritePrefabCacheOrder.Remove(path);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                while (_favoritePrefabCacheOrder.Count >= MaxFavoritePrefabCacheSize)
                {
                    string oldestKey = _favoritePrefabCacheOrder[0];
                    _favoritePrefabCacheOrder.RemoveAt(0);
                    _favoritePrefabCache.Remove(oldestKey);
                }
                _favoritePrefabCache[path] = prefab;
                _favoritePrefabCacheOrder.Add(path);
            }
            return prefab;
        }

        private void ShowFavoriteTags(VFXFavoriteLibrary.ItemData item)
        {
            if (item == null)
            {
                EditorUtility.DisplayDialog("标签", "未找到精选项信息。", "确定");
                return;
            }

            if (item.tags == null || item.tags.Count == 0)
            {
                EditorUtility.DisplayDialog("标签", $"「{item.name}」暂无标签。", "确定");
                return;
            }

            string tagText = string.Join("、", item.tags);
            EditorUtility.DisplayDialog("标签", $"「{item.name}」标签：\n{tagText}", "确定");
        }

        private void DrawScanToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(new GUIContent("扫描路径:", "指定要扫描的文件夹"), EditorStyles.boldLabel, GUILayout.Width(70));
            _scanPath = EditorGUILayout.TextField(_scanPath, GUILayout.Height(24));
            
            if (GUILayout.Button(new GUIContent("...", "浏览文件夹"), GUILayout.Width(30), GUILayout.Height(24)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择扫描文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    string dataPath = Application.dataPath.Replace("\\", "/");
                    string projectPath = Path.GetFullPath(dataPath + "/..").Replace("\\", "/");
                    selectedPath = selectedPath.Replace("\\", "/");
                    
                    if (selectedPath.StartsWith(projectPath + "/") || selectedPath == projectPath)
                    {
                        string relativePath = selectedPath;
                        if (selectedPath.StartsWith(projectPath + "/"))
                        {
                            relativePath = selectedPath.Substring(projectPath.Length + 1);
                        }
                        
                        if (string.IsNullOrEmpty(relativePath) || relativePath == "Assets")
                        {
                            _scanPath = "Assets";
                        }
                        else
                        {
                            _scanPath = relativePath;
                        }
                        SaveScanPath();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("无效路径", "请选择项目内的文件夹。", "确定");
                    }
                }
            }
            
            if (GUILayout.Button(new GUIContent("扫描", "扫描指定目录下的完整特效预制体"), _scanButtonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                SaveScanPath();
                ScanLibrary();
            }
            
            GUILayout.Space(10);
            
            bool newExcludeHt = GUILayout.Toggle(_excludeHtSuffix, new GUIContent("去_ht", "过滤名称以 _ht 或 _hutong 结尾的特效"), GUILayout.Width(55));
            if (newExcludeHt != _excludeHtSuffix)
            {
                _excludeHtSuffix = newExcludeHt;
                SaveExcludeHtSuffixSetting();
                InvalidateFilterCache();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilterBar()
        {
            var categories = VFXTagPoolManager.GetCategories();
            if (categories.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("筛选:", EditorStyles.boldLabel, GUILayout.Width(50));
            
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("清除筛选", GUILayout.Width(80), GUILayout.Height(24)))
            {
                _activeFilters.Clear();
                InvalidateFilterCache();
            }

            EditorGUILayout.EndHorizontal();

            float availableWidth = EditorGUIUtility.currentViewWidth - 140;
            float buttonWidth = 80f;
            float buttonSpacing = 5f;
            float categoryLabelWidth = 90f;
            float categorySpacing = 10f;

            foreach (var category in categories)
            {
                if (category.tags == null || category.tags.Count == 0) continue;

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal(GUILayout.Height(26));
                GUILayout.Space(categoryLabelWidth + categorySpacing);
                Rect labelRect = GUILayoutUtility.GetLastRect();
                labelRect.width = categoryLabelWidth;
                labelRect.height = 26;
                EditorGUI.LabelField(labelRect, $"【{category.name}】", new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft });
                
                float currentLineWidth = 0f;

                foreach (var tag in category.tags)
                {
                    bool isActive = _activeFilters.Contains(tag.name);
                    
                    if (currentLineWidth + buttonWidth > availableWidth && currentLineWidth > 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal(GUILayout.Height(26));
                        GUILayout.Space(categoryLabelWidth + categorySpacing);
                        currentLineWidth = 0f;
                    }

                    GUIStyle buttonStyle = isActive ? _activeTagButtonStyle : _inactiveTagButtonStyle;

                    if (GUILayout.Button(tag.name, buttonStyle, GUILayout.Height(26), GUILayout.Width(buttonWidth)))
                    {
                        if (isActive)
                        {
                            _activeFilters.Remove(tag.name);
                        }
                        else
                        {
                            _activeFilters.Add(tag.name);
                        }
                        InvalidateFilterCache();
                    }

                    currentLineWidth += buttonWidth + buttonSpacing;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }
        
        private void InvalidateFilterCache()
        {
            _lastFilterHash = -1;
            _filteredListCache = null;
            _lastExcludeHtHash = -1;
            _excludeHtFilterCache = null;
            _lastFavoriteFilterHash = -1;
            _filteredFavoriteCache = null;
            _scanCurrentPage = 1;
            _favoriteCurrentPage = 1;
        }
        
        private int ComputeFilterHash()
        {
            int hash = 17;
            foreach (var filter in _activeFilters)
            {
                hash = hash * 31 + filter.GetHashCode();
            }
            hash = hash * 31 + _filterLibrary.Count.GetHashCode();
            hash = hash * 31 + _excludeHtSuffix.GetHashCode();
            return hash;
        }

        private int ComputeExcludeHtHash(List<VFXFilterData.FilterItemData> list)
        {
            int hash = 17;
            hash = hash * 31 + (list?.Count ?? 0).GetHashCode();
            hash = hash * 31 + _excludeHtSuffix.GetHashCode();
            return hash;
        }

        private List<VFXFilterData.FilterItemData> GetFilteredListCached()
        {
            int currentHash = ComputeFilterHash();
            
            if (_filteredListCache != null && _lastFilterHash == currentHash)
            {
                return ApplyExcludeHtSuffix(_filteredListCache);
            }
            
            _filteredListCache = ComputeFilteredList();
            _lastFilterHash = currentHash;
            
            return ApplyExcludeHtSuffix(_filteredListCache);
        }
        
        private List<VFXFilterData.FilterItemData> ApplyExcludeHtSuffix(List<VFXFilterData.FilterItemData> filteredList)
        {
            if (!_excludeHtSuffix || filteredList == null || filteredList.Count == 0)
            {
                return filteredList;
            }

            int currentHash = ComputeExcludeHtHash(filteredList);
            if (_excludeHtFilterCache != null && _lastExcludeHtHash == currentHash)
            {
                return _excludeHtFilterCache;
            }
            
            var result = new List<VFXFilterData.FilterItemData>();
            for (int i = 0; i < filteredList.Count; i++)
            {
                var item = filteredList[i];
                if (item != null && !item.name.EndsWith("_ht") && !item.name.EndsWith("_hutong"))
                {
                    result.Add(item);
                }
            }
            
            int removedCount = filteredList.Count - result.Count;
            if (removedCount > 0)
            {
                Debug.Log($"[VFX Filter] 已过滤 {removedCount} 个 _ht/_hutong 后缀特效");
            }

            _excludeHtFilterCache = result;
            _lastExcludeHtHash = currentHash;
            
            return result;
        }
        
        private List<VFXFilterData.FilterItemData> ComputeFilteredList()
        {
            if (_activeFilters.Count == 0)
            {
                return _filterLibrary;
            }
            
            var activeFilterSet = new HashSet<string>(_activeFilters);
            var result = new List<VFXFilterData.FilterItemData>();
            
            for (int i = 0; i < _filterLibrary.Count; i++)
            {
                var item = _filterLibrary[i];
                if (item.tags != null)
                {
                    for (int j = 0; j < item.tags.Count; j++)
                    {
                        if (activeFilterSet.Contains(item.tags[j]))
                        {
                            result.Add(item);
                            break;
                        }
                    }
                }
            }
            
            return result;
        }

        private void DrawFilterItem(VFXFilterData.FilterItemData item)
        {
            EditorGUILayout.BeginHorizontal(_cardStyle, GUILayout.Height(ItemHeight));
            
            Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            VFXPreviewUtils.DrawDynamicThumbnail(previewRect, item.prefab, item.path, forceDynamic: true);

            EditorGUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(item.name, $"路径: {item.path}"), EditorStyles.boldLabel);
            GUILayout.Label($"类型: {item.type} | 标签数: {item.tags?.Count ?? 0}", EditorStyles.miniLabel);
            
            if (item.tags != null && item.tags.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                int tagCount = Mathf.Min(item.tags.Count, 5);
                for (int i = 0; i < tagCount; i++)
                {
                    string tagName = item.tags[i];
                    Color tagColor = VFXTagPoolManager.GetTagColor(tagName);
                    
                    _tagLabelStyle.normal.background = GetCachedTexture(tagColor);
                    _tagLabelStyle.normal.textColor = VFXEditorUtils.GetContrastColor(tagColor);
                    
                    GUILayout.Box(tagName, _tagLabelStyle, GUILayout.Height(18));
                }
                if (item.tags.Count > 5)
                {
                    GUILayout.Label($"...", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("定位", "在项目视图中定位此预制体"), _actionButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                EditorGUIUtility.PingObject(item.prefab);
            }
            
            if (GUILayout.Button(new GUIContent("预览", "在SceneView中预览此特效"), _actionButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
            {
                StartPreview(item);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            
            if (GUILayout.Button(new GUIContent("添加到场景", "将此预制体实例化到当前场景"), _actionButtonStyle, GUILayout.Width(80), GUILayout.Height(22)))
            {
                AddToHierarchy(item);
            }
            
            if (GUILayout.Button(new GUIContent("标签", "编辑此特效的标签"), _actionButtonStyle, GUILayout.Width(80), GUILayout.Height(22)))
            {
                VFXTagEditorWindow.ShowWindow(item, _filterCache, OnTagsModified);
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            
            bool isFav = VFXFavoriteManager.IsInFavorite(item.path);
            bool isSelected = _selectedItems.Contains(item.path);
            
            if (isFav)
            {
                GUI.enabled = false;
                GUILayout.Button(new GUIContent("已收藏", "此特效已在精选库中"), _actionButtonStyle, GUILayout.Width(60), GUILayout.Height(22));
                GUI.enabled = true;
            }
            else
            {
                GUIStyle selectStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(10, 10, 4, 4)
                };
                
                if (isSelected)
                {
                    selectStyle.normal.background = GetCachedTexture(new Color(0.3f, 0.6f, 0.3f, 1f));
                    selectStyle.normal.textColor = Color.white;
                    if (GUILayout.Button(new GUIContent("✓", "点击取消选中"), selectStyle, GUILayout.Width(60), GUILayout.Height(22)))
                    {
                        _selectedItems.Remove(item.path);
                    }
                }
                else
                {
                    selectStyle.normal.background = GetCachedTexture(new Color(0.3f, 0.3f, 0.3f, 1f));
                    selectStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                    if (GUILayout.Button(new GUIContent("○", "点击选中"), selectStyle, GUILayout.Width(60), GUILayout.Height(22)))
                    {
                        _selectedItems.Add(item.path);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }


        private void StartPreview(VFXFilterData.FilterItemData item)
        {
            ClearPreview();

            if (SceneView.lastActiveSceneView == null)
            {
                EditorUtility.DisplayDialog("提示", "请先打开 SceneView 窗口", "确定");
                return;
            }
            
            if (item.prefab == null) return;

            _previewInstance = Instantiate(item.prefab);
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
            _currentPreviewItem = item;
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
            _currentPreviewItem = null;
            _previewTime = 0f;
        }

        private void AddToHierarchy(VFXFilterData.FilterItemData item)
        {
            if (item.prefab == null) return;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab);
            
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                Camera sceneCam = SceneView.lastActiveSceneView.camera;
                instance.transform.position = sceneCam.transform.position + sceneCam.transform.forward * 5f;
            }
            
            Selection.activeGameObject = instance;
            Undo.RegisterCreatedObjectUndo(instance, $"Add {item.name} to Hierarchy");
            
            Debug.Log($"[VFX Filter] 已添加 '{item.name}' 到 Hierarchy");
        }

        private void AddFilteredToFavorites(List<VFXFilterData.FilterItemData> filteredList)
        {
            if (filteredList == null || filteredList.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "当前筛选结果为空，无法添加到精选库。", "确定");
                return;
            }

            var toAdd = new List<VFXFilterData.FilterItemData>();
            int duplicateCount = 0;

            foreach (var item in filteredList)
            {
                if (item == null || string.IsNullOrEmpty(item.path) || item.prefab == null) continue;

                if (VFXFavoriteManager.IsInFavorite(item.path))
                {
                    duplicateCount++;
                    continue;
                }

                toAdd.Add(item);
            }

            if (toAdd.Count == 0)
            {
                EditorUtility.DisplayDialog("批量收藏", $"当前筛选结果共 {filteredList.Count} 项，全部已在精选库中。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("批量收藏",
                $"当前筛选结果共 {filteredList.Count} 项。\n新增 {toAdd.Count} 项到精选库，{duplicateCount} 项已存在。\n确认添加？",
                "确认", "取消"))
            {
                return;
            }

            VFXFavoriteManager.AddItems(toAdd);
            Repaint();
        }

        private void AddSelectedToFavorites(List<VFXFilterData.FilterItemData> filteredList)
        {
            if (_selectedItems.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先选中要收藏的特效。", "确定");
                return;
            }

            var toAdd = new List<VFXFilterData.FilterItemData>();
            int duplicateCount = 0;
            int notInFilterCount = 0;

            foreach (var item in filteredList)
            {
                if (item == null || string.IsNullOrEmpty(item.path) || item.prefab == null) continue;

                if (_selectedItems.Contains(item.path))
                {
                    if (VFXFavoriteManager.IsInFavorite(item.path))
                    {
                        duplicateCount++;
                    }
                    else
                    {
                        toAdd.Add(item);
                    }
                }
            }

            notInFilterCount = _selectedItems.Count - toAdd.Count - duplicateCount;

            if (toAdd.Count == 0)
            {
                string msg = $"已选中 {_selectedItems.Count} 项";
                if (duplicateCount > 0) msg += $"\n{duplicateCount} 项已在精选库中";
                if (notInFilterCount > 0) msg += $"\n{notInFilterCount} 项不在当前筛选结果中";
                EditorUtility.DisplayDialog("收藏选中项", msg, "确定");
                return;
            }

            string confirmMsg = $"已选中 {_selectedItems.Count} 项\n新增 {toAdd.Count} 项到精选库";
            if (duplicateCount > 0) confirmMsg += $"\n{duplicateCount} 项已在精选库中（跳过）";
            if (notInFilterCount > 0) confirmMsg += $"\n{notInFilterCount} 项不在当前筛选结果中（跳过）";
            confirmMsg += "\n\n确认添加？";

            if (!EditorUtility.DisplayDialog("收藏选中项", confirmMsg, "确认", "取消"))
            {
                return;
            }

            VFXFavoriteManager.AddItems(toAdd);
            _selectedItems.Clear();
            Repaint();
        }

        private void ScanLibrary()
        {
            _filterLibrary.Clear();
            InvalidateFilterCache();

            string[] paths = _scanPath.Split(new char[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = paths[i].Trim();
            }
            
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("无效路径", "请至少指定一个扫描路径。", "确定");
                return;
            }
            
            int validCount = 0;
            int scannedPrefabs = 0;

            foreach (string scanPath in paths)
            {
                if (!AssetDatabase.IsValidFolder(scanPath))
                {
                    Debug.LogWarning($"[VFX Filter] 无效文件夹路径: {scanPath}");
                    continue;
                }
                
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { scanPath });
                scannedPrefabs += guids.Length;

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab == null) continue;

                    string type;
                    List<string> autoTags;
                    bool isLooping;
                    float duration;
                    
                    if (IsValidCompleteVFXPrefab(prefab, out type, out autoTags, out isLooping, out duration))
                    {
                        var finalTags = BuildMatchedTags(path, prefab.name, type, isLooping, autoTags);

                        var newItem = new VFXFilterData.FilterItemData
                        {
                            path = path,
                            name = prefab.name,
                            prefab = prefab,
                            type = type,
                            tags = finalTags,
                            loop = isLooping,
                            duration = duration
                        };
                        
                        _filterLibrary.Add(newItem);
                        validCount++;
                    }
                }
            }
            
            SaveFilterCache();

            Debug.Log($"[VFX Filter] 扫描完成。扫描了 {scannedPrefabs} 个预制体，找到 {validCount} 个完整特效预制体。");
            EditorUtility.DisplayDialog("扫描完成", $"扫描完成！\n\n扫描了 {scannedPrefabs} 个预制体\n找到 {validCount} 个完整特效预制体\n路径数: {paths.Length}", "确定");
        }

        private List<string> BuildMatchedTags(string prefabPath, string prefabName, string type, bool isLooping, List<string> detectedTags)
        {
            var existingTags = VFXTagPoolManager.GetAllTags()
                .Select(t => t.name)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            if (existingTags.Count == 0)
            {
                return new List<string>();
            }

            var existingTagSet = new HashSet<string>(existingTags);
            var matchedTags = new HashSet<string>();
            var searchTokens = BuildSearchTokens(prefabPath, prefabName, type);
            string searchableText = string.Join(" ", searchTokens);

            if (detectedTags != null)
            {
                foreach (var tag in detectedTags)
                {
                    if (!string.IsNullOrWhiteSpace(tag) && existingTagSet.Contains(tag))
                    {
                        matchedTags.Add(tag);
                    }
                }
            }

            foreach (var tag in existingTags)
            {
                string normalizedTag = NormalizeForMatching(tag);
                if (ContainsKeyword(searchableText, searchTokens, normalizedTag))
                {
                    matchedTags.Add(tag);
                }

                foreach (var alias in GetKeywordAliases(tag))
                {
                    if (ContainsKeyword(searchableText, searchTokens, alias))
                    {
                        matchedTags.Add(tag);
                        break;
                    }
                }
            }

            if (isLooping && existingTagSet.Contains("循环"))
            {
                matchedTags.Add("循环");
            }
            else if (!isLooping && existingTagSet.Contains("不循环"))
            {
                matchedTags.Add("不循环");
            }

            if (type == "Particle" && existingTagSet.Contains("粒子"))
            {
                matchedTags.Add("粒子");
            }

            return existingTags.Where(matchedTags.Contains).ToList();
        }

        private static HashSet<string> BuildSearchTokens(string prefabPath, string prefabName, string type)
        {
            var tokens = new HashSet<string>();

            AddTokens(tokens, prefabPath);
            AddTokens(tokens, prefabName);
            AddTokens(tokens, type);

            foreach (var pathPart in prefabPath.Split('/'))
            {
                AddTokens(tokens, pathPart);
            }

            return tokens;
        }

        private static void AddTokens(HashSet<string> tokens, string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return;

            string normalizedSource = NormalizeForMatching(source);
            if (!string.IsNullOrEmpty(normalizedSource))
            {
                tokens.Add(normalizedSource);
            }

            var splitParts = source
                .Split(new[] { '/', '\\', '_', '-', '.', ' ', '(', ')', '[', ']', '{', '}' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in splitParts)
            {
                string normalizedPart = NormalizeForMatching(part);
                if (!string.IsNullOrEmpty(normalizedPart))
                {
                    tokens.Add(normalizedPart);
                }
            }
        }

        private static string NormalizeForMatching(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static IEnumerable<string> GetKeywordAliases(string tag)
        {
            if (TagKeywordAliases.TryGetValue(tag, out var aliases))
            {
                return aliases.Select(NormalizeForMatching);
            }

            return System.Array.Empty<string>();
        }

        private static bool ContainsKeyword(string searchableText, HashSet<string> searchTokens, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            string normalizedKeyword = NormalizeForMatching(keyword);
            if (searchTokens.Contains(normalizedKeyword))
            {
                return true;
            }

            return searchableText.Contains(normalizedKeyword);
        }

        private bool IsValidCompleteVFXPrefab(GameObject prefab, out string type, out List<string> tags, out bool isLooping, out float duration)
        {
            type = "Unknown";
            tags = new List<string>();
            isLooping = false;
            duration = 0f;
            
            bool hasValidVFX = false;
            List<string> typesFound = new List<string>();
            
            ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (particleSystems.Length > 0)
            {
                hasValidVFX = true;
                typesFound.Add("Particle");
                
                bool anyLooping = false;
                float maxDuration = 0f;
                
                foreach (var ps in particleSystems)
                {
                    if (ps.main.loop) anyLooping = true;
                    if (ps.main.duration > maxDuration) maxDuration = ps.main.duration;
                }
                
                isLooping = anyLooping;
                duration = maxDuration;
            }

            TrailRenderer[] trailRenderers = prefab.GetComponentsInChildren<TrailRenderer>(true);
            if (trailRenderers.Length > 0)
            {
                hasValidVFX = true;
                typesFound.Add("Trail");
                isLooping = true;
            }

            LineRenderer[] lineRenderers = prefab.GetComponentsInChildren<LineRenderer>(true);
            if (lineRenderers.Length > 0)
            {
                hasValidVFX = true;
                typesFound.Add("Line");
                isLooping = true;
            }

            if (typesFound.Count > 1) type = "Mixed";
            else if (typesFound.Count == 1) type = typesFound[0];

            return hasValidVFX;
        }
    }
}
