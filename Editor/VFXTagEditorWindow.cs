using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VFXTools.Editor
{
    public class VFXTagEditorWindow : EditorWindow
    {
        private VFXLibraryData.VFXAssetItemData _targetItem;
        private VFXFilterData.FilterItemData _filterItem;
        private VFXFavoriteLibrary.ItemData _favoriteItem;
        private VFXLibraryData _libraryData;
        private VFXFilterData _filterData;
        private System.Action _onModified;
        private Vector2 _scrollPos;
        private bool _isFilterMode = false;
        private bool _isFavoriteMode = false;
        private bool _hasUnsavedChanges = false;

        public static void ShowWindow(VFXLibraryData.VFXAssetItemData item, VFXLibraryData libraryData, System.Action onModified)
        {
            var window = GetWindow<VFXTagEditorWindow>(true, $"编辑标签 - {item.name}", true);
            window._targetItem = item;
            window._libraryData = libraryData;
            window._onModified = onModified;
            window._isFilterMode = false;
            window._isFavoriteMode = false;
            window.minSize = new Vector2(400, 500);
        }

        public static void ShowWindow(VFXFilterData.FilterItemData item, VFXFilterData filterData, System.Action onModified)
        {
            var window = GetWindow<VFXTagEditorWindow>(true, $"编辑标签 - {item.name}", true);
            window._filterItem = item;
            window._filterData = filterData;
            window._onModified = onModified;
            window._isFilterMode = true;
            window._isFavoriteMode = false;
            window.minSize = new Vector2(400, 500);
        }

        public static void ShowWindow(VFXFavoriteLibrary.ItemData item, System.Action onModified)
        {
            var window = GetWindow<VFXTagEditorWindow>(true, $"编辑标签 - {item.name}", true);
            window._favoriteItem = item;
            window._onModified = onModified;
            window._isFilterMode = false;
            window._isFavoriteMode = true;
            window.minSize = new Vector2(400, 500);
        }

        private List<string> GetCurrentTags()
        {
            if (_isFilterMode && _filterItem != null)
            {
                return _filterItem.tags;
            }
            else if (_isFavoriteMode && _favoriteItem != null)
            {
                return _favoriteItem.tags;
            }
            else if (_targetItem != null)
            {
                return _targetItem.tags;
            }
            return new List<string>();
        }

        private string GetCurrentItemName()
        {
            if (_isFilterMode && _filterItem != null)
            {
                return _filterItem.name;
            }
            else if (_isFavoriteMode && _favoriteItem != null)
            {
                return _favoriteItem.name;
            }
            else if (_targetItem != null)
            {
                return _targetItem.name;
            }
            return "未知";
        }

        private void OnDestroy()
        {
            if (_hasUnsavedChanges)
            {
                SaveChanges();
            }
        }

        private void OnGUI()
        {
            var currentTags = GetCurrentTags();

            DrawHeader(currentTags);
            EditorGUILayout.Space(5);
            DrawCurrentTags(currentTags);
            EditorGUILayout.Space(5);
            DrawAvailableTags(currentTags);
            EditorGUILayout.Space(5);
            DrawActions();
        }

        private void DrawHeader(List<string> currentTags)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"特效: {GetCurrentItemName()}", EditorStyles.boldLabel);
            GUILayout.Label($"当前标签数: {currentTags.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawCurrentTags(List<string> currentTags)
        {
            GUILayout.Label("当前标签", EditorStyles.boldLabel);

            if (currentTags.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无标签", MessageType.Info);
                return;
            }

            var tagsToDraw = currentTags.ToList();
            string tagToRemove = null;

            float availableWidth = EditorGUIUtility.currentViewWidth - 40f;
            float tagSpacing = 5f;
            float currentLineWidth = 0f;
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Space(0);
            
            foreach (var tagName in tagsToDraw)
            {
                Color tagColor = VFXTagPoolManager.GetTagColor(tagName);

                var colorStyle = new GUIStyle(GUIStyle.none);
                colorStyle.normal.background = VFXEditorUtils.MakeTexture(2, 2, tagColor);
                colorStyle.padding = new RectOffset(8, 8, 4, 4);
                colorStyle.normal.textColor = VFXEditorUtils.GetContrastColor(tagColor);
                colorStyle.alignment = TextAnchor.MiddleCenter;

                float tagWidth = colorStyle.CalcSize(new GUIContent(tagName)).x + 40f;
                
                if (currentLineWidth + tagWidth > availableWidth && currentLineWidth > 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    currentLineWidth = 0f;
                }

                EditorGUILayout.BeginHorizontal(colorStyle, GUILayout.Height(24), GUILayout.Width(tagWidth));
                GUILayout.Label(tagName);
                if (GUILayout.Button("×", GUILayout.Width(20), GUILayout.Height(20)))
                {
                    tagToRemove = tagName;
                }
                EditorGUILayout.EndHorizontal();
                
                currentLineWidth += tagWidth + tagSpacing;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(tagToRemove))
            {
                RemoveTag(tagToRemove);
            }
        }

        private void DrawAvailableTags(List<string> currentTags)
        {
            GUILayout.Space(10);
            GUILayout.Label("可用标签（点击切换状态）", EditorStyles.boldLabel);

            var categories = VFXTagPoolManager.GetCategories();
            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("暂无可用标签，请联系管理员配置标签池。", MessageType.Info);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox);

            float availableWidth = EditorGUIUtility.currentViewWidth - 40f; // 减去边距
            float buttonMinWidth = 50f;
            float buttonSpacing = 5f;
            float categoryLabelWidth = 90f;

            string tagToAdd = null;
            string tagToRemove = null;

            foreach (var category in categories)
            {
                if (category.tags == null || category.tags.Count == 0) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"【{category.name}】", new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 }, GUILayout.Width(categoryLabelWidth), GUILayout.Height(26));

                float currentLineWidth = 0f;

                foreach (var tag in category.tags)
                {
                    bool isSelected = currentTags.Contains(tag.name);

                    var tagStyle = new GUIStyle(GUI.skin.button)
                    {
                        padding = new RectOffset(10, 10, 4, 4),
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 13,
                        fontStyle = FontStyle.Normal,
                        border = new RectOffset(1, 1, 1, 1)
                    };

                    if (isSelected)
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

                    float buttonWidth = tagStyle.CalcSize(new GUIContent(tag.name)).x + 20f;
                    buttonWidth = Mathf.Max(buttonWidth, buttonMinWidth);

                    if (currentLineWidth + buttonWidth > availableWidth && currentLineWidth > 0)
                    {
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(categoryLabelWidth);
                        currentLineWidth = 0f;
                    }

                    if (GUILayout.Button(tag.name, tagStyle, GUILayout.Height(26), GUILayout.MinWidth(buttonMinWidth), GUILayout.Width(buttonWidth)))
                    {
                        if (isSelected)
                        {
                            tagToRemove = tag.name;
                        }
                        else
                        {
                            tagToAdd = tag.name;
                        }
                    }

                    currentLineWidth += buttonWidth + buttonSpacing;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();

            // 在遍历结束后执行添加或删除操作
            if (!string.IsNullOrEmpty(tagToAdd))
            {
                AddTag(tagToAdd);
            }
            if (!string.IsNullOrEmpty(tagToRemove))
            {
                RemoveTag(tagToRemove);
            }
        }

        private void DrawActions()
        {
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("关闭", GUILayout.Width(80), GUILayout.Height(30)))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AddTag(string tagName)
        {
            var currentTags = GetCurrentTags();
            if (!currentTags.Contains(tagName))
            {
                currentTags.Add(tagName);
                _hasUnsavedChanges = true;
                _onModified?.Invoke();
                Repaint();
            }
        }

        private void RemoveTag(string tagName)
        {
            var currentTags = GetCurrentTags();
            if (currentTags.Remove(tagName))
            {
                _hasUnsavedChanges = true;
                _onModified?.Invoke();
                Repaint();
            }
        }

        private void SaveChanges()
        {
            if (_isFilterMode)
            {
                if (_filterData != null)
                {
                    EditorUtility.SetDirty(_filterData);
                    AssetDatabase.SaveAssets();
                }
            }
            else if (_isFavoriteMode)
            {
                VFXFavoriteManager.SaveLibrary();
                if (_favoriteItem != null)
                {
                    VFXFavoriteManager.SyncTagsToFilterCache(_favoriteItem.path, _favoriteItem.tags);
                }
            }
            else
            {
                if (_libraryData != null)
                {
                    EditorUtility.SetDirty(_libraryData);
                    AssetDatabase.SaveAssets();
                }
            }
            
            _onModified?.Invoke();
            Repaint();
        }
    }
}
