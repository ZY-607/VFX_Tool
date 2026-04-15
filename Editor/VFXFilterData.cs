using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VFXTools.Editor
{
    public class VFXFilterData : ScriptableObject
    {
        [System.Serializable]
        public class FilterItemData
        {
            public string path;
            public string name;
            public GameObject prefab;
            public string type;
            public List<string> tags = new List<string>();
            public bool loop;
            public float duration;
        }

        public List<FilterItemData> items = new List<FilterItemData>();

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

        public void RemoveTagFromAllItems(string tagName)
        {
            foreach (var item in items)
            {
                item.tags.Remove(tagName);
            }
        }
    }
}
