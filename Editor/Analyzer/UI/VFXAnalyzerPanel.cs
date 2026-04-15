using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VFXTools.Editor;

namespace VFXTools.Editor.Analyzer
{
    /// <summary>
    /// 特效需求分析器面板 UI
    /// 集成到 VFX Composer 窗口中
    /// </summary>
    public class VFXAnalyzerPanel
    {
        private VFXRequirementAnalyzer analyzer;
        private VFXLibraryData libraryData;
        
        private bool isPanelExpanded = true;
        private string currentImagePath = "";
        private Texture2D currentImageTexture;
        private bool isDragging = false;
        
        private bool showAPISettings = false;
        private string tempAPIKey = "";
        private bool tempUseCustomVolcEngineModelId = false;
        private string tempVolcEngineModelId = "";
        private int selectedAPIIndex = 0;
        private readonly string[] apiOptions = { "OpenAI GPT-4 Vision", "Claude Vision", "Google Gemini Vision", "火山引擎豆包视觉" };
        
        private Vector2 scrollPosition;
        private Action<List<string>> onApplyFilters;
        
        private float panelHeight = 200f;
        private bool isResizing = false;
        private const float MinPanelHeight = 100f;
        private const float MaxPanelHeight = 600f;
        private const string PanelHeightPrefKey = "VFXAnalyzer_PanelHeight";
        
        private const string LibraryCachePath = "Assets/VFX Tools/Editor/VFXLibraryCache.asset";
        
        public VFXAnalyzerPanel(Action<List<string>> applyFiltersCallback)
        {
            analyzer = new VFXRequirementAnalyzer();
            analyzer.OnAnalysisComplete += HandleAnalysisComplete;
            analyzer.OnError += HandleError;
            analyzer.OnAnalysisStart += HandleAnalysisStart;
            
            onApplyFilters = applyFiltersCallback;
            
            selectedAPIIndex = (int)analyzer.Config.selectedAPI;
            RefreshAPIInputs();
            
            panelHeight = EditorPrefs.GetFloat(PanelHeightPrefKey, 200f);
            
            LoadLibraryData();
        }
        
        private void LoadLibraryData()
        {
            libraryData = AssetDatabase.LoadAssetAtPath<VFXLibraryData>(LibraryCachePath);
        }
        
        public void Draw()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            DrawHeader();
            
            if (isPanelExpanded)
            {
                EditorGUILayout.Space(5);
                DrawContent();
                DrawResizeHandle();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            
            GUIContent headerContent = new GUIContent(
                isPanelExpanded ? "▼ 需求分析器 (Requirement Analyzer)" : "▶ 需求分析器 (Requirement Analyzer)",
                "点击展开/折叠分析器面板"
            );
            
            if (GUILayout.Button(headerContent, EditorStyles.boldLabel, GUILayout.Height(24)))
            {
                isPanelExpanded = !isPanelExpanded;
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("⚙", GUILayout.Width(24), GUILayout.Height(24)))
            {
                showAPISettings = !showAPISettings;
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawContent()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(panelHeight));
            
            if (showAPISettings)
            {
                DrawAPISettings();
                EditorGUILayout.Space(5);
            }
            
            DrawImageDropArea();
            EditorGUILayout.Space(5);
            
            DrawAnalysisResult();
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawAPISettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("API 设置", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("服务:", GUILayout.Width(50));
            int newAPIIndex = EditorGUILayout.Popup(selectedAPIIndex, apiOptions);
            if (newAPIIndex != selectedAPIIndex)
            {
                selectedAPIIndex = newAPIIndex;
                analyzer.SetAPIService((VFXAnalyzerConfig.APIService)selectedAPIIndex);
                RefreshAPIInputs();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("API Key:", GUILayout.Width(50));
            tempAPIKey = EditorGUILayout.PasswordField(tempAPIKey);
            if (GUILayout.Button("保存", GUILayout.Width(50)))
            {
                SaveCurrentAPISettings();
                EditorUtility.DisplayDialog("保存成功", "API Key 已保存", "确定");
            }
            EditorGUILayout.EndHorizontal();

            if ((VFXAnalyzerConfig.APIService)selectedAPIIndex == VFXAnalyzerConfig.APIService.VolcEngine)
            {
                EditorGUILayout.HelpBox("Gemini 只需 API Key。火山引擎支持默认 Endpoint 模式和自定义 Endpoint ID 模式；若默认模式无权限，请切到自定义模式。", MessageType.Info);
                tempUseCustomVolcEngineModelId = EditorGUILayout.ToggleLeft("使用自定义 Endpoint ID", tempUseCustomVolcEngineModelId);
                if (tempUseCustomVolcEngineModelId)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Endpoint:", GUILayout.Width(60));
                    tempVolcEngineModelId = EditorGUILayout.TextField(tempVolcEngineModelId);
                    EditorGUILayout.EndHorizontal();
                }

                string resolvedModelId = tempUseCustomVolcEngineModelId
                    ? tempVolcEngineModelId.Trim()
                    : analyzer.GetDefaultVolcEngineModelId();
                EditorGUILayout.LabelField("当前生效 Endpoint:", string.IsNullOrEmpty(resolvedModelId) ? "未配置" : resolvedModelId);
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("测试连接", GUILayout.Width(80)))
            {
                TestAPIConnection();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void TestAPIConnection()
        {
            SaveCurrentAPISettings();

            if ((VFXAnalyzerConfig.APIService)selectedAPIIndex == VFXAnalyzerConfig.APIService.VolcEngine &&
                tempUseCustomVolcEngineModelId &&
                string.IsNullOrWhiteSpace(tempVolcEngineModelId))
            {
                EditorUtility.DisplayDialog("测试失败", "请先输入自定义 Endpoint ID", "确定");
                return;
            }

            analyzer.TestConnection((result) =>
            {
                if (result.isSuccess)
                {
                    EditorUtility.DisplayDialog("测试成功", $"当前 {apiOptions[selectedAPIIndex]} 配置可用。", "确定");
                    return;
                }

                EditorUtility.DisplayDialog("测试失败", result.errorMessage, "确定");
            });
        }

        private void RefreshAPIInputs()
        {
            tempAPIKey = analyzer.GetCurrentAPIKey();
            tempUseCustomVolcEngineModelId = analyzer.IsUsingCustomVolcEngineModelId();
            tempVolcEngineModelId = analyzer.GetVolcEngineCustomModelId();
        }

        private void SaveCurrentAPISettings()
        {
            analyzer.SetAPIService((VFXAnalyzerConfig.APIService)selectedAPIIndex);
            analyzer.SetAPIKey((VFXAnalyzerConfig.APIService)selectedAPIIndex, tempAPIKey);

            if ((VFXAnalyzerConfig.APIService)selectedAPIIndex == VFXAnalyzerConfig.APIService.VolcEngine)
            {
                string customModelId = tempUseCustomVolcEngineModelId ? tempVolcEngineModelId.Trim() : "";
                analyzer.SetVolcEngineUseCustomModelId(tempUseCustomVolcEngineModelId);
                analyzer.SetVolcEngineCustomModelId(customModelId);
            }
        }
        
        private void DrawResizeHandle()
        {
            Rect resizeRect = GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true));
            
            EditorGUI.DrawRect(resizeRect, new Color(0.3f, 0.3f, 0.3f, 1f));
            
            Rect lineRect = new Rect(resizeRect.x + resizeRect.width * 0.4f, resizeRect.y + 2, resizeRect.width * 0.2f, 2);
            EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 1f));
            
            Event evt = Event.current;
            
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (resizeRect.Contains(evt.mousePosition))
                    {
                        isResizing = true;
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseDrag:
                    if (isResizing)
                    {
                        panelHeight -= evt.delta.y;
                        panelHeight = Mathf.Clamp(panelHeight, MinPanelHeight, MaxPanelHeight);
                        EditorPrefs.SetFloat(PanelHeightPrefKey, panelHeight);
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseUp:
                    if (isResizing)
                    {
                        isResizing = false;
                        evt.Use();
                    }
                    break;
                    
                case EventType.Repaint:
                    if (resizeRect.Contains(evt.mousePosition) || isResizing)
                    {
                        EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);
                    }
                    break;
            }
        }
        
        private void DrawImageDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));
            
            EditorGUI.DrawRect(dropRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            if (currentImageTexture != null)
            {
                float aspectRatio = (float)currentImageTexture.width / currentImageTexture.height;
                float displayHeight = 70f;
                float displayWidth = displayHeight * aspectRatio;
                
                Rect imageRect = new Rect(
                    dropRect.x + (dropRect.width - displayWidth) / 2,
                    dropRect.y + 5,
                    displayWidth,
                    displayHeight
                );
                
                GUI.DrawTexture(imageRect, currentImageTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUIContent dropContent = new GUIContent(
                    "📷 拖放图片到此处\n或点击选择文件\n\n支持: PNG, JPG, JPEG",
                    "上传特效需求截图进行分析"
                );
                GUIStyle dropStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12
                };
                GUI.Label(dropRect, dropContent, dropStyle);
            }
            
            HandleDragAndDrop(dropRect);
            HandleClick(dropRect);
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (!string.IsNullOrEmpty(currentImagePath))
            {
                string fileName = Path.GetFileName(currentImagePath);
                GUILayout.Label($"当前图片: {fileName}", EditorStyles.miniLabel, GUILayout.MaxWidth(200));
                
                if (GUILayout.Button("清除", GUILayout.Width(50)))
                {
                    ClearImage();
                }
            }
            
            GUILayout.FlexibleSpace();
            
            EditorGUI.BeginDisabledGroup(analyzer.IsAnalyzing || !analyzer.HasValidAPIKey() || string.IsNullOrEmpty(currentImagePath));
            
            string buttonText = analyzer.IsAnalyzing ? "分析中..." : "分析需求";
            if (GUILayout.Button(buttonText, GUILayout.Width(80), GUILayout.Height(24)))
            {
                StartAnalysis();
            }
            
            EditorGUI.EndDisabledGroup();
            
            if (!analyzer.HasValidAPIKey())
            {
                string hint = analyzer.Config.selectedAPI == VFXAnalyzerConfig.APIService.VolcEngine
                    ? "请先配置火山 API Key 和可用 Endpoint"
                    : "请先设置 API Key";
                GUILayout.Label(hint, EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void HandleDragAndDrop(Rect dropRect)
        {
            Event evt = Event.current;
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropRect.Contains(evt.mousePosition))
                        break;
                    
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            string path = AssetDatabase.GetAssetPath(obj);
                            if (IsValidImageFile(path))
                            {
                                LoadImage(path);
                                break;
                            }
                        }
                        
                        if (DragAndDrop.paths != null)
                        {
                            foreach (var path in DragAndDrop.paths)
                            {
                                if (IsValidImageFile(path))
                                {
                                    LoadImage(path);
                                    break;
                                }
                            }
                        }
                    }
                    
                    Event.current.Use();
                    break;
            }
        }
        
        private void HandleClick(Rect dropRect)
        {
            Event evt = Event.current;
            
            if (evt.type == EventType.MouseDown && dropRect.Contains(evt.mousePosition))
            {
                string path = EditorUtility.OpenFilePanel("选择特效需求图片", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path) && IsValidImageFile(path))
                {
                    LoadImage(path);
                }
                Event.current.Use();
            }
        }
        
        private bool IsValidImageFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }
        
        private void LoadImage(string path)
        {
            currentImagePath = path;
            
            if (currentImageTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(currentImageTexture);
            }
            
            byte[] fileData = File.ReadAllBytes(path);
            currentImageTexture = new Texture2D(2, 2);
            currentImageTexture.LoadImage(fileData);
            
            analyzer.ClearResult();
        }
        
        private void ClearImage()
        {
            currentImagePath = "";
            if (currentImageTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(currentImageTexture);
                currentImageTexture = null;
            }
            analyzer.ClearResult();
        }
        
        private void StartAnalysis()
        {
            SaveCurrentAPISettings();
            LoadLibraryData();
            
            var allTags = VFXTagPoolManager.GetAllTags();
            List<string> existingTags = allTags.Select(t => t.name).ToList();
            
            Debug.Log($"[VFX Analyzer] 标签池数量: {existingTags.Count}");
            if (existingTags.Count > 0)
            {
                Debug.Log($"[VFX Analyzer] 标签池内容: {string.Join(", ", existingTags)}");
            }
            else
            {
                Debug.LogWarning("[VFX Analyzer] 标签池为空！请检查 VFXTagPool.json 是否存在");
            }
            
            analyzer.AnalyzeImage(currentImagePath, existingTags);
        }
        
        private void DrawAnalysisResult()
        {
            var result = analyzer.LastResult;
            
            if (result == null || (!result.isSuccess && string.IsNullOrEmpty(result.errorMessage)))
            {
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("分析结果", EditorStyles.boldLabel);
            
            if (!result.isSuccess)
            {
                EditorGUILayout.HelpBox(result.errorMessage, MessageType.Error);
            }
            else
            {
                if (!string.IsNullOrEmpty(result.GetSummary()))
                {
                    EditorGUILayout.HelpBox(result.GetSummary(), MessageType.Info);
                }
                
                if (result.matchedTags != null && result.matchedTags.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    GUILayout.Label("匹配标签:", EditorStyles.miniLabel);
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    foreach (var tag in result.matchedTags)
                    {
                        Color tagColor = VFXTagPoolManager.GetTagColor(tag);
                        
                        var tagStyle = new GUIStyle(GUIStyle.none);
                        tagStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, tagColor);
                        tagStyle.padding = new RectOffset(6, 6, 2, 2);
                        tagStyle.normal.textColor = VFXEditorUtils.GetContrastColor(tagColor);
                        tagStyle.alignment = TextAnchor.MiddleCenter;
                        tagStyle.fontSize = 10;
                        
                        GUILayout.Box(tag, tagStyle, GUILayout.Height(20));
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("应用筛选", GUILayout.Width(80), GUILayout.Height(22)))
                    {
                        ApplyFilters();
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("未找到匹配的标签，请尝试手动筛选", MessageType.Warning);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void ApplyFilters()
        {
            var result = analyzer.LastResult;
            if (result != null && result.matchedTags != null && result.matchedTags.Count > 0)
            {
                onApplyFilters?.Invoke(result.matchedTags);
            }
        }
        
        private void HandleAnalysisComplete(VFXAnalysisResult result)
        {
            Debug.Log($"[VFX Analyzer] 分析完成: {result.matchedTags?.Count ?? 0} 个匹配标签");
            
            if (result.matchedTags != null && result.matchedTags.Count > 0)
            {
                Debug.Log($"[VFX Analyzer] 自动应用筛选: {string.Join(", ", result.matchedTags)}");
                ApplyFilters();
            }
        }
        
        private void HandleError(string error)
        {
            Debug.LogError($"[VFX Analyzer] 错误: {error}");
            EditorUtility.DisplayDialog("分析错误", error, "确定");
        }
        
        private void HandleAnalysisStart()
        {
            Debug.Log("[VFX Analyzer] 开始分析...");
        }
        
        public void Cleanup()
        {
            if (currentImageTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(currentImageTexture);
                currentImageTexture = null;
            }
        }
    }
}
