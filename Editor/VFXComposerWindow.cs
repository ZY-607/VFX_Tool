using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VFXTools.Editor.Analyzer;

namespace VFXTools.Editor
{
    public class VFXComposerWindow : EditorWindow
    {
        private const string ToolVersion = "v0.22.4";
        private List<VFXLibraryData.VFXAssetItemData> _library = new List<VFXLibraryData.VFXAssetItemData>();
        private GameObject _previewStructureRoot; // 预览结构根节点
        private List<GameObject> _composerSlots = new List<GameObject>();
        private List<bool> _slotLocks = new List<bool>(); // 插槽锁定状态
        
        // 预览相关变量
        private GameObject _previewInstance;
        private List<ParticleSystem> _previewParticleSystems = new List<ParticleSystem>(); // 缓存粒子系统以提高性能
        private bool _isPreviewing = false;
        private double _lastUpdateTime; // 用于计算 deltaTime

        // 数据持久化路径
        private const string LibraryCachePath = "Assets/VFX Tools/Editor/VFXLibraryCache.asset";
        
        // 生成控制
        private int _generateCount = 3; // 默认生成数量
        
        private Vector2 _scrollPosLibrary;
        private Vector2 _scrollPosComposer;
        private string _scanPath = "Assets/VFX Tools"; // 默认扫描路径
        private const string ScanPathPrefsKey = "VFXComposer_ScanPath"; // EditorPrefs 键名
        
        // 拖动调整高度相关变量
        private float _composerHeight = 200f;
        private bool _isResizing = false;
        private float _minComposerHeight = 100f;
        private float _resizeHandleHeight = 5f;

        private GUIStyle _headerStyle;
        private GUIStyle _cardStyle;
        
        private List<string> _activeFilters = new List<string>();

        [MenuItem("Tools/VFX/VFX Composer")]
        public static void ShowWindow()
        {
            GetWindow<VFXComposerWindow>("VFX Composer");
        }

        public static void AddVFXFromFilter(GameObject prefab, string path, string name)
        {
            var window = GetWindow<VFXComposerWindow>("VFX Composer");
            if (window == null || prefab == null) return;
            
            window.AddPrefabToFirstEmptySlot(prefab, name);
            window.Repaint();
        }

        private void AddPrefabToFirstEmptySlot(GameObject prefab, string name)
        {
            if (prefab == null) return;
            
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.SetActive(false);
            
            GameObject extractedGo = Instantiate(instance);
            extractedGo.name = string.IsNullOrEmpty(name) ? prefab.name : name;
            extractedGo.transform.position = Vector3.zero;
            extractedGo.SetActive(true);
            
            DestroyImmediate(instance);
            
            for (int i = 0; i < _composerSlots.Count; i++)
            {
                if (_composerSlots[i] == null)
                {
                    _composerSlots[i] = extractedGo;
                    return;
                }
            }
            
            _composerSlots.Add(extractedGo);
            _slotLocks.Add(false);
            _generateCount = _composerSlots.Count;
        }

        private void OnEnable()
        {
            LoadScanPath();
            LoadLibrary();

            if (_composerSlots.Count == 0)
            {
                _composerSlots.Add(null);
                _composerSlots.Add(null);
                _composerSlots.Add(null);
            }
            
            while (_slotLocks.Count < _composerSlots.Count) _slotLocks.Add(false);
            while (_slotLocks.Count > _composerSlots.Count) _slotLocks.RemoveAt(_slotLocks.Count - 1);
            
            _generateCount = _composerSlots.Count;

            EditorApplication.update += OnEditorUpdate;
            
            VFXTagPoolManager.OnTagRenamed += OnTagRenamed;
            VFXTagPoolManager.OnTagRemoved += OnTagRemoved;
            VFXPreviewCoordinator.OnPreviewStarted += OnExternalPreviewStarted;
            VFXAnalyzerWindow.OnAnalysisResultApplied += OnAnalysisResultApplied;
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
            var data = AssetDatabase.LoadAssetAtPath<VFXLibraryData>(LibraryCachePath);
            if (data != null)
            {
                data.RenameTagInAllItems(oldName, newName);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                LoadLibrary();
                Repaint();
                Debug.Log($"[VFX Composer] 标签已同步重命名: {oldName} -> {newName}");
            }
        }
        
        private void OnTagRemoved(string tagName)
        {
            var data = AssetDatabase.LoadAssetAtPath<VFXLibraryData>(LibraryCachePath);
            if (data != null)
            {
                data.RemoveTagFromAllItems(tagName);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                LoadLibrary();
                Repaint();
                Debug.Log($"[VFX Composer] 标签已同步删除: {tagName}");
            }
        }

        private void OnEditorUpdate()
        {
            // 更新动态缩略图
            if (VFXPreviewUtils.UpdateAllPreviews())
            {
                Repaint();
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            // 手动驱动粒子模拟
            if (_isPreviewing && _previewInstance != null)
            {
                // 如果预览对象被意外删除了，清理状态
                if (_previewInstance == null)
                {
                    ClearPreview();
                    return;
                }

                bool needsRepaint = false;

                // 驱动所有粒子系统
                foreach (var ps in _previewParticleSystems)
                {
                    if (ps != null)
                    {
                        // 只有非暂停状态下才模拟，但为了预览我们强制模拟
                        ps.Simulate(deltaTime, true, false);
                        needsRepaint = true;
                    }
                }

                // 如果有变化，强制 SceneView 重绘
                if (needsRepaint)
                {
                    SceneView.RepaintAll();
                }
            }
        }

        private void LoadLibrary()
        {
            var data = AssetDatabase.LoadAssetAtPath<VFXLibraryData>(LibraryCachePath);
            if (data != null)
            {
                _library = new List<VFXLibraryData.VFXAssetItemData>(data.items);
                // 简单验证一下引用是否丢失 (如果删除了Prefab，引用会变成null)
                _library.RemoveAll(x => x.rootPrefab == null);
            }
        }

        private void StartPreview(VFXLibraryData.VFXAssetItemData item)
        {
            ClearPreview(); // 确保旧的已清除

            if (SceneView.lastActiveSceneView == null) return;
            if (item.rootPrefab == null) return;

            // 1. 实例化根 Prefab (不可见，且不保存)
            GameObject rootInstance = (GameObject)PrefabUtility.InstantiatePrefab(item.rootPrefab);
            rootInstance.hideFlags = HideFlags.HideAndDontSave;
            rootInstance.SetActive(false);

            // 2. 找到目标子节点
            Transform targetTransform = string.IsNullOrEmpty(item.childPath) ? rootInstance.transform : rootInstance.transform.Find(item.childPath);

            if (targetTransform != null)
            {
                // 3. 复制目标节点作为预览对象
                _previewInstance = Instantiate(targetTransform.gameObject);
                _previewInstance.hideFlags = HideFlags.HideAndDontSave; // 关键：不污染场景
                _previewInstance.name = $"[PREVIEW] {item.name}";
                
                // 4. 设置位置：基于包围盒计算自适应距离
                Camera sceneCam = SceneView.lastActiveSceneView.camera;
                if (sceneCam != null)
                {
                    Bounds bounds = new Bounds(_previewInstance.transform.position, Vector3.zero);
                    Renderer[] renderers = _previewInstance.GetComponentsInChildren<Renderer>();
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    
                    float maxBound = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (maxBound == 0) maxBound = 1f;
                    
                    float distance = Mathf.Max(8f, maxBound * 4f);
                    
                    _previewInstance.transform.position = sceneCam.transform.position + sceneCam.transform.forward * distance;
                }

                _previewInstance.SetActive(true);

                // 收集所有粒子系统以便每帧驱动
                _previewParticleSystems.Clear();
                _previewInstance.GetComponentsInChildren<ParticleSystem>(true, _previewParticleSystems);

                // 立即播放一次
                foreach(var ps in _previewParticleSystems)
                {
                    ps.Play(true);
                }
                
                // 5. 显示提示
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent($"Previewing: {item.name}"));
            }

            // 6. 销毁临时根节点
            DestroyImmediate(rootInstance);
            
            _isPreviewing = true;
            _lastUpdateTime = EditorApplication.timeSinceStartup; // 重置时间，防止第一帧 delta 过大
            
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

        private void SaveLibrary()
        {
            var data = AssetDatabase.LoadAssetAtPath<VFXLibraryData>(LibraryCachePath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<VFXLibraryData>();
                // 确保存储目录存在
                string directory = Path.GetDirectoryName(LibraryCachePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                
                AssetDatabase.CreateAsset(data, LibraryCachePath);
            }

            data.items = new List<VFXLibraryData.VFXAssetItemData>(_library);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        private void OnTagsModified()
        {
            // 标签修改后刷新显示
            LoadLibrary();
            Repaint();
        }
        
        private void OnAnalysisResultApplied(List<string> matchedTags)
        {
            _activeFilters = new List<string>(matchedTags);
            Repaint();
            
            Debug.Log($"[VFX Composer] 已应用分析器筛选: {string.Join(", ", matchedTags)}");
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.BeginVertical();
            
            DrawComposerArea();

            DrawSplitLine();

            DrawLibraryArea();

            EditorGUILayout.EndVertical();
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel);
                _headerStyle.fontSize = 14;
                _headerStyle.margin = new RectOffset(10, 10, 10, 10);
                _headerStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f); // 稍微提亮标题
            }
            
            if (_cardStyle == null)
            {
                _cardStyle = new GUIStyle(EditorStyles.helpBox);
                _cardStyle.padding = new RectOffset(10, 10, 10, 10);
                _cardStyle.margin = new RectOffset(5, 5, 5, 5);
            }
        }

        private void DrawSplitLine()
        {
            // 获取一个用于绘制分割线的矩形区域
            Rect resizeRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(_resizeHandleHeight));
            
            // 绘制视觉上的分割线 (居中细线)
            Rect visualLineRect = resizeRect;
            visualLineRect.height = 1;
            visualLineRect.y += (_resizeHandleHeight / 2) - 1;
            EditorGUI.DrawRect(visualLineRect, new Color(0.12f, 0.12f, 0.12f, 1)); // 深灰色分割线

            // 增加交互区域的热区，方便鼠标捕捉
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);

            // 处理拖动事件
            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
            }
            
            if (_isResizing)
            {
                _composerHeight += Event.current.delta.y;
                // 限制高度范围
                _composerHeight = Mathf.Clamp(_composerHeight, _minComposerHeight, position.height - 100);
                
                Repaint(); // 强制重绘以更新布局
            }

            if (Event.current.type == EventType.MouseUp)
            {
                _isResizing = false;
            }
        }

        private void DrawComposerArea()
        {
            EditorGUILayout.BeginVertical(GUILayout.Height(_composerHeight));
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"VFX Composer (特效组合器) {ToolVersion}", _headerStyle);
            GUILayout.FlexibleSpace();
            
            var analyzerButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 4, 4)
            };
            Color analyzerButtonColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            analyzerButtonStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, analyzerButtonColor);
            analyzerButtonStyle.normal.textColor = Color.white;
            
            if (GUILayout.Button("需求分析器", analyzerButtonStyle, GUILayout.Width(80), GUILayout.Height(24)))
            {
                VFXAnalyzerWindow.ShowWindowFromCaller("Composer");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Settings:", GUILayout.Width(60));
            _generateCount = EditorGUILayout.IntSlider(new GUIContent("Count (数量)", "Number of slots to generate.\n生成插槽的数量。"), _generateCount, 1, 30);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Random Generate (随机生成)", "Randomly fill all unlocked slots.\n随机填充所有未锁定的插槽。"), GUILayout.Height(30)))
            {
                GenerateRandom();
            }
            if (GUILayout.Button(new GUIContent("Clear All (全部清空)", "Clear all slots and destroy effects.\n清空所有插槽并销毁特效。"), GUILayout.Height(30)))
            {
                for (int i = 0; i < _composerSlots.Count; i++) 
                {
                    if (_composerSlots[i] != null) DestroyImmediate(_composerSlots[i]);
                    _composerSlots[i] = null;
                    _slotLocks[i] = false; // 清空时解锁
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Slots (组合插槽)", EditorStyles.boldLabel);

            _scrollPosComposer = EditorGUILayout.BeginScrollView(_scrollPosComposer);
            EditorGUILayout.BeginVertical(); // 改为垂直列表布局

            // 确保同步
            while (_slotLocks.Count < _composerSlots.Count) _slotLocks.Add(false);

            for (int i = 0; i < _composerSlots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(24));
                
                // 1. Lock Icon
                var lockIconName = _slotLocks[i] ? "InspectorLock" : "InspectorLock"; // Unity 默认锁图标只有一种，靠 Toggle 状态区分背景，或者使用不同的图标
                // 为了更好的视觉效果，使用 Toggle
                _slotLocks[i] = GUILayout.Toggle(_slotLocks[i], GUIContent.none, "IN LockButton", GUILayout.Width(20), GUILayout.Height(20));

                // 2. Slot Object
                _composerSlots[i] = (GameObject)EditorGUILayout.ObjectField(_composerSlots[i], typeof(GameObject), false, GUILayout.Height(20));

                // 3. Info (Optional - 如果有内容，显示名字)
                if (_composerSlots[i] != null)
                {
                     // GUILayout.Label(_composerSlots[i].name, EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();

                // 4. Single Refresh
                if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh", "Randomize this slot only.\n仅随机刷新此插槽。"), GUILayout.Width(28), GUILayout.Height(22)))
                {
                    // 即使锁定了，手动点刷新也应该强制刷新，或者提示？
                    // 逻辑：手动操作优先级高于锁，但为了逻辑统一，如果锁了应该提示解锁
                    if (_slotLocks[i])
                    {
                        if (EditorUtility.DisplayDialog("Slot Locked", "This slot is locked. Unlock to refresh?", "Unlock & Refresh", "Cancel"))
                        {
                            _slotLocks[i] = false;
                            RandomizeSlot(i);
                        }
                    }
                    else
                    {
                        RandomizeSlot(i);
                    }
                }

                // 5. Delete/Remove
                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash", "Remove this slot.\n移除此插槽。"), GUILayout.Width(28), GUILayout.Height(22)))
                {
                    if (_composerSlots[i] != null) DestroyImmediate(_composerSlots[i]);
                    _composerSlots.RemoveAt(i);
                    _slotLocks.RemoveAt(i);
                    i--;
                    _generateCount = _composerSlots.Count;
                }

                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }

        private List<VFXLibraryData.VFXAssetItemData> GetFilteredLibrary()
        {
            var filteredList = _library;
            
            // 标签筛选 (OR 逻辑)
            if (_activeFilters.Count > 0)
            {
                filteredList = filteredList.Where(x => x.tags.Any(t => _activeFilters.Contains(t))).ToList();
            }
            
            return filteredList;
        }

        private void RandomizeSlot(int index)
        {
            var filteredLibrary = GetFilteredLibrary();
            if (filteredLibrary.Count == 0) return;

            // 确保结构存在
            EnsurePreviewStructure();
            Transform container = GetContainer();

            // 如果插槽已有内容，先销毁旧的
            if (_composerSlots[index] != null)
            {
                DestroyImmediate(_composerSlots[index]);
                _composerSlots[index] = null;
            }

            var randomItem = filteredLibrary[Random.Range(0, filteredLibrary.Count)];
            
            // 提取并放入容器
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(randomItem.rootPrefab);
            instance.SetActive(false);
            Transform target = string.IsNullOrEmpty(randomItem.childPath) ? instance.transform : instance.transform.Find(randomItem.childPath);
            if (target != null)
            {
                GameObject extracted = Instantiate(target.gameObject);
                extracted.name = target.name; // 使用原始名称
                
                if (container != null)
                {
                    extracted.transform.SetParent(container, false);
                }
                
                extracted.transform.localPosition = Vector3.zero;
                extracted.SetActive(true);
                _composerSlots[index] = extracted;
            }
            DestroyImmediate(instance);
        }

        private void GenerateRandom()
        {
            var filteredLibrary = GetFilteredLibrary();
            if (filteredLibrary.Count == 0)
            {
                if (_library.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示 (Hint)", "特效库为空，请先点击 'Scan Library'。\nLibrary is empty. Please scan first.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("提示 (Hint)", "筛选后特效库为空，请调整筛选条件。\nFiltered library is empty. Please adjust filters.", "OK");
                }
                return;
            }

            // 1. 同步插槽数量
            while (_composerSlots.Count > _generateCount)
            {
                int lastIndex = _composerSlots.Count - 1;
                if (_composerSlots[lastIndex] != null) DestroyImmediate(_composerSlots[lastIndex]);
                _composerSlots.RemoveAt(lastIndex);
                _slotLocks.RemoveAt(lastIndex);
            }
            while (_composerSlots.Count < _generateCount)
            {
                _composerSlots.Add(null);
                _slotLocks.Add(false);
            }

            // 2. 随机填充/替换逻辑
            for (int i = 0; i < _composerSlots.Count; i++)
            {
                // 如果插槽被锁定，且内容不为空，则跳过
                if (_slotLocks[i] && _composerSlots[i] != null) continue;

                RandomizeSlot(i);
            }
        }

        private void EnsurePreviewStructure()
        {
            // 如果根节点被删除了，或者为空，则重建
            if (_previewStructureRoot == null)
            {
                _previewStructureRoot = new GameObject($"VFX_Gen_{System.DateTime.Now:MMdd_HHmmss}");
                
                // 创建空粒子系统 (无效果，无消耗) 并作为容器
                // 参考用户需求：Control_Particle 需要替代掉 Container 层级作为所有粒子效果的父级
                var psGo = new GameObject("Control_Particle");
                psGo.transform.SetParent(_previewStructureRoot.transform, false);
                var ps = psGo.AddComponent<ParticleSystem>();
                
                // 禁用发射和渲染，确保零消耗
                var emission = ps.emission;
                emission.enabled = false;
                var shape = ps.shape;
                shape.enabled = false;
                var renderer = psGo.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = false;
                
                ps.Stop();
            }
        }

        private Transform GetContainer()
        {
            if (_previewStructureRoot != null)
            {
                // 查找 Control_Particle 作为容器
                return _previewStructureRoot.transform.Find("Control_Particle");
            }
            return null;
        }

        private void DrawLibraryArea()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Library Browser (特效库浏览)", _headerStyle);

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
            
            var scanButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(15, 15, 4, 4)
            };
            Color scanButtonColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            scanButtonStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, scanButtonColor);
            scanButtonStyle.normal.textColor = Color.white;
            
            if (GUILayout.Button(new GUIContent("扫描", "扫描指定目录下的特效预制体"), scanButtonStyle, GUILayout.Width(70), GUILayout.Height(24)))
            {
                SaveScanPath();
                ScanLibrary();
            }
            
            EditorGUILayout.EndHorizontal();

            var categories = VFXTagPoolManager.GetCategories();
            if (categories.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("筛选:", EditorStyles.boldLabel, GUILayout.Width(50));
                
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("清除筛选", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    _activeFilters.Clear();
                }

                EditorGUILayout.EndHorizontal();

                float availableWidth = EditorGUIUtility.currentViewWidth - 140;
                float buttonMinWidth = 50f;
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
                        
                        var tagStyle = new GUIStyle(GUI.skin.button)
                        {
                            padding = new RectOffset(10, 10, 4, 4),
                            alignment = TextAnchor.MiddleCenter,
                            fontSize = 12,
                            fontStyle = FontStyle.Normal,
                            border = new RectOffset(1, 1, 1, 1),
                            fixedHeight = 26
                        };
                        
                        if (isActive)
                        {
                            Color activeBgColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                            tagStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, activeBgColor);
                            tagStyle.normal.textColor = Color.white;
                        }
                        else
                        {
                            Color inactiveBgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                            tagStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, inactiveBgColor);
                            tagStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                        }

                        float buttonWidth = 80f;

                        if (currentLineWidth + buttonWidth > availableWidth && currentLineWidth > 0)
                        {
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal(GUILayout.Height(26));
                            GUILayout.Space(categoryLabelWidth + categorySpacing);
                            currentLineWidth = 0f;
                        }

                        if (GUILayout.Button(tag.name, tagStyle, GUILayout.Height(26), GUILayout.Width(buttonWidth)))
                        {
                            if (isActive)
                            {
                                _activeFilters.Remove(tag.name);
                            }
                            else
                            {
                                _activeFilters.Add(tag.name);
                            }
                        }

                        currentLineWidth += buttonWidth + buttonSpacing;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            // 列表显示
            _scrollPosLibrary = EditorGUILayout.BeginScrollView(_scrollPosLibrary);
            
            // 筛选逻辑
            var filteredList = _library;
            
            // 标签筛选 (OR 逻辑)
            if (_activeFilters.Count > 0)
            {
                filteredList = filteredList.Where(x => x.tags.Any(t => _activeFilters.Contains(t))).ToList();
            }

            if (filteredList.Count == 0)
            {
                GUILayout.Label("未找到特效资源。请点击 '扫描' 或清空筛选器。\nNo assets found. Click '扫描' or clear filters.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                GUILayout.Label($"找到 {filteredList.Count} 个特效资源", EditorStyles.miniLabel);
                
                foreach (var item in filteredList)
                {
                    DrawLibraryItem(item);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void AddToFirstEmptySlot(VFXLibraryData.VFXAssetItemData item)
        {
            // 方案B：智能提取 (Smart Extraction)
            // 不直接实例化整个 Prefab，而是只提取需要的子节点部分
            
            // 1. 实例化根 Prefab 到内存 (不可见)
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.rootPrefab);
            instance.SetActive(false); // 避免它运行

            // 2. 找到目标子节点
            Transform targetTransform = string.IsNullOrEmpty(item.childPath) ? instance.transform : instance.transform.Find(item.childPath);
            
            GameObject extractedGo = null;

            if (targetTransform != null)
            {
                // 3. 复制目标节点 (这就切断了与原 Prefab 的连接，变成了纯净的 GameObject)
                extractedGo = Instantiate(targetTransform.gameObject);
                extractedGo.name = item.name; // 保持清晰的命名
                extractedGo.transform.position = Vector3.zero; // 重置位置到原点，方便观察
                
                // 确保它是激活的
                extractedGo.SetActive(true);
            }
            else
            {
                Debug.LogError($"[VFX Composer] Could not find child path '{item.childPath}' in prefab '{item.rootPrefab.name}'");
            }

            // 4. 销毁临时实例
            DestroyImmediate(instance);

            if (extractedGo == null) return;

            // 5. 放入插槽
            bool added = false;
            for (int i = 0; i < _composerSlots.Count; i++)
            {
                if (_composerSlots[i] == null)
                {
                    _composerSlots[i] = extractedGo;
                    added = true;
                    break;
                }
            }
            // 如果满了，添加新槽位
            if (!added)
            {
                _composerSlots.Add(extractedGo);
                _slotLocks.Add(false); // 保持同步
                _generateCount = _composerSlots.Count;
            }
        }

        private bool DrawLibraryItem(VFXLibraryData.VFXAssetItemData item)
        {
            EditorGUILayout.BeginHorizontal(_cardStyle);
            
            // 绘制动态缩略图
            Rect previewRect = GUILayoutUtility.GetRect(80, 80, GUILayout.Width(80), GUILayout.Height(80));
            // 传入 item.childPath，确保组合器中的动态缩略图仅展示子节点
            VFXPreviewUtils.DrawDynamicThumbnailFixedOrigin(previewRect, item.rootPrefab, item.path, item.childPath);

            EditorGUILayout.BeginVertical();
            GUILayout.Label(new GUIContent(item.name, $"Source: {item.path}\nChild: {item.childPath}"), EditorStyles.boldLabel);
            GUILayout.Label($"类型: {item.type} | 源: {item.rootPrefab.name}", EditorStyles.miniLabel);
            
            if (item.tags != null && item.tags.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (var tagName in item.tags)
                {
                    Color tagColor = VFXTagPoolManager.GetTagColor(tagName);

                    var tagStyle = new GUIStyle(GUIStyle.none);
                    tagStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, tagColor);
                    tagStyle.padding = new RectOffset(6, 6, 2, 2);
                    tagStyle.normal.textColor = VFXEditorUtils.GetContrastColor(tagColor);
                    tagStyle.alignment = TextAnchor.MiddleCenter;
                    tagStyle.fontSize = 10;
                    
                    GUILayout.Box(tagName, tagStyle, GUILayout.Height(18));
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();

            if (GUILayout.Button(new GUIContent("定位", "在项目视图中定位此预制体"), GUILayout.Width(60), GUILayout.Height(22)))
            {
                EditorGUIUtility.PingObject(item.rootPrefab);
            }

            if (GUILayout.Button(new GUIContent("预览", "在SceneView中预览此特效"), GUILayout.Width(60), GUILayout.Height(22)))
            {
                StartPreview(item);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();

            if (GUILayout.Button(new GUIContent("添加", "提取并添加到第一个空插槽"), GUILayout.Width(80), GUILayout.Height(22)))
            {
                AddToFirstEmptySlot(item);
            }

            if (GUILayout.Button(new GUIContent("标签", "编辑此特效的标签"), GUILayout.Width(80), GUILayout.Height(22)))
            {
                VFXTagEditorWindow.ShowWindow(item, null, OnTagsModified);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            
            return false;
        }

        // ===================================================================================
        // 核心逻辑：扫描 (Scanning)
        // ===================================================================================
        
        /// <summary>
        /// 获取项目窗口中选中的文件夹路径
        /// </summary>
        private string GetSelectedFolderInProjectWindow()
        {
            if (Selection.activeObject != null)
            {
                string path = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(path))
                {
                    // 如果选中的是文件夹
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        return path;
                    }
                    // 如果选中的是文件，返回其所在文件夹
                    else
                    {
                        return System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
                    }
                }
            }
            return null;
        }
        
        private void ScanLibrary()
        {
            _library.Clear();

            string[] paths = _scanPath.Split(new char[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = paths[i].Trim();
            }
            
            if (paths.Length == 0)
            {
                EditorUtility.DisplayDialog("Invalid Path", "Please specify at least one scan path.\n请至少指定一个扫描路径。", "OK");
                return;
            }
            
            int validCount = 0;
            int scannedPrefabs = 0;

            foreach (string scanPath in paths)
            {
                if (!AssetDatabase.IsValidFolder(scanPath))
                {
                    Debug.LogWarning($"[VFX Composer] Invalid folder path: {scanPath}");
                    continue;
                }
                
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { scanPath });
                scannedPrefabs += guids.Length;

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject rootGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (rootGo == null) continue;

                    List<Transform> allChildren = new List<Transform>();
                    rootGo.GetComponentsInChildren<Transform>(true, allChildren);

                    foreach (var child in allChildren)
                    {
                        GameObject childGo = child.gameObject;
                        
                        string type;
                        List<string> autoTags;
                        bool isLooping;
                        float duration;
                        
                        if (IsValidVFXPrefab(childGo, out type, out autoTags, out isLooping, out duration))
                        {
                            string childPath = GetRelativePath(rootGo.transform, child);
                            string displayName = string.IsNullOrEmpty(childPath) ? rootGo.name : $"{rootGo.name} / {child.name}";

                            var newItem = new VFXLibraryData.VFXAssetItemData
                            {
                                path = path,
                                name = displayName,
                                rootPrefab = rootGo,
                                childPath = childPath,
                                type = type,
                                tags = autoTags,
                                loop = isLooping,
                                duration = duration
                            };
                            
                            _library.Add(newItem);
                            validCount++;
                        }
                    }
                }
            }
            
            SaveLibrary();

            Debug.Log($"[VFX Composer] Scan Complete. Scanned {scannedPrefabs} prefabs in {paths.Length} folder(s), found {validCount} valid VFX components.");
            EditorUtility.DisplayDialog("Scan Complete", $"扫描完成！\n\n扫描了 {scannedPrefabs} 个预制体\n找到 {validCount} 个有效特效组件\n路径数: {paths.Length}", "OK");
        }

        private string GetRelativePath(Transform root, Transform child)
        {
            if (root == child) return "";
            string path = child.name;
            Transform current = child.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private bool IsValidVFXPrefab(GameObject go, out string type, out List<string> tags, out bool isLooping, out float duration)
        {
            type = "Unknown";
            tags = new List<string>();
            isLooping = false;
            duration = 0f;
            
            bool hasValidRenderer = false;
            List<string> typesFound = new List<string>();

            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.enabled && go.activeSelf) 
                {
                    hasValidRenderer = true;
                    typesFound.Add("Particle");
                    
                    isLooping = ps.main.loop;
                    duration = ps.main.duration;
                }
            }

            var tr = go.GetComponent<TrailRenderer>();
            if (tr != null && tr.enabled && go.activeSelf)
            {
                hasValidRenderer = true;
                typesFound.Add("Trail");
                isLooping = true;
            }

            var lr = go.GetComponent<LineRenderer>();
            if (lr != null && lr.enabled && go.activeSelf)
            {
                hasValidRenderer = true;
                typesFound.Add("Line");
                isLooping = true;
            }

            if (typesFound.Count > 1) type = "Mixed";
            else if (typesFound.Count == 1) type = typesFound[0];

            return hasValidRenderer;
        }

    }
}
