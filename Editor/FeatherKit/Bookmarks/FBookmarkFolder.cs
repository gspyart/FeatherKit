using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>Папка закладок: имя, свёрнута ли, и закладки по порядку.</summary>
    [Serializable]
    public class FBookmarkFolder
    {
        [SerializeField] private string name;
        [SerializeField] private bool isExpanded = true;
        [SerializeField] private List<FBookmark> bookmarks = new List<FBookmark>();


        public FBookmarkFolder(string name)
        {
            this.name = name;
        }


        public string Name
        {
            get => name;
            set => name = value;
        }

        public bool IsExpanded
        {
            get => isExpanded;
            set => isExpanded = value;
        }

        public List<FBookmark> Bookmarks => bookmarks;


        public bool Contains(string globalId)
        {
            return bookmarks.Exists(bookmark => bookmark.GlobalId == globalId);
        }
    }
}
