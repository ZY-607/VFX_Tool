using System;
using System.Collections.Generic;

namespace VFXTools.Editor
{
    [Serializable]
    public class VFXFavoriteLibrary
    {
        public List<ItemData> items = new List<ItemData>();

        [Serializable]
        public class ItemData
        {
            public string path;
            public string name;
            public string type;
            public bool loop;
            public float duration;
            public List<string> tags = new List<string>();
            public string addedDate;

            public ItemData()
            {
                addedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            public ItemData(string path, string name, string type, bool loop, float duration, List<string> tags)
            {
                this.path = path;
                this.name = name;
                this.type = type;
                this.loop = loop;
                this.duration = duration;
                this.tags = tags != null ? new List<string>(tags) : new List<string>();
                this.addedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }
}
