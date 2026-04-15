using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VFXTools.Editor
{
    public class VFXLibraryData : ScriptableObject
    {
        [System.Serializable]
        public class VFXAssetItemData
        {
            public string path;
            public string name;
            public GameObject rootPrefab;
            public string childPath;
            public string type;
            
            public List<string> tags = new List<string>();
            public bool loop;
            public float duration;
        }

        public List<VFXAssetItemData> items = new List<VFXAssetItemData>();

        public VFXAssetItemData GetItemByPath(string path)
        {
            return items.FirstOrDefault(i => i.path == path);
        }

        public void AddItem(VFXAssetItemData item)
        {
            if (!items.Any(i => i.path == item.path))
            {
                items.Add(item);
            }
        }

        public void RemoveItem(string path)
        {
            items.RemoveAll(i => i.path == path);
        }

        public void ClearItems()
        {
            items.Clear();
        }

        public List<VFXAssetItemData> GetItemsWithTag(string tagName)
        {
            return items.Where(i => i.tags.Contains(tagName)).ToList();
        }

        public List<VFXAssetItemData> GetItemsWithTags(List<string> tagNames, bool matchAll = false)
        {
            if (tagNames == null || tagNames.Count == 0)
            {
                return items;
            }

            if (matchAll)
            {
                return items.Where(i => tagNames.All(t => i.tags.Contains(t))).ToList();
            }
            else
            {
                return items.Where(i => tagNames.Any(t => i.tags.Contains(t))).ToList();
            }
        }

        public List<string> GetAllUsedTags()
        {
            var allTags = new HashSet<string>();
            foreach (var item in items)
            {
                foreach (var tag in item.tags)
                {
                    allTags.Add(tag);
                }
            }
            return allTags.ToList();
        }

        public void RemoveTagFromAllItems(string tagName)
        {
            foreach (var item in items)
            {
                item.tags.Remove(tagName);
            }
        }

        public void RenameTagInAllItems(string oldName, string newName)
        {
            foreach (var item in items)
            {
                for (int i = 0; i < item.tags.Count; i++)
                {
                    if (item.tags[i] == oldName)
                    {
                        item.tags[i] = newName;
                    }
                }
            }
        }
    }
}
