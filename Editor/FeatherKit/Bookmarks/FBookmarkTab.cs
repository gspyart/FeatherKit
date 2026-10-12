using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Вкладка закладок: имя и свой набор папок.
    /// Одна и та же ссылка может лежать в разных вкладках — это независимые копии.
    /// </summary>
    [Serializable]
    public class FBookmarkTab
    {
        private const string DefaultFolderName = "Общее";

        [SerializeField] private string name;
        [SerializeField] private int activeFolderIndex;
        [SerializeField] private List<FBookmarkFolder> folders = new List<FBookmarkFolder>();


        public FBookmarkTab(string name)
        {
            this.name = name;
        }


        public string Name
        {
            get => name;
            set => name = value;
        }

        public List<FBookmarkFolder> Folders => folders;

        /// <summary>Куда кладут новые закладки этой вкладки. Нет папок — заводится «Общее».</summary>
        public FBookmarkFolder ActiveFolder
        {
            get
            {
                if (folders.Count == 0)
                    folders.Add(new FBookmarkFolder(DefaultFolderName));

                return folders[Mathf.Clamp(activeFolderIndex, 0, folders.Count - 1)];
            }
        }


        /// <summary>Сделать папку активной. Возвращает, поменялось ли что-то.</summary>
        public bool SetActiveFolder(FBookmarkFolder folder)
        {
            var index = folders.IndexOf(folder);

            if (index < 0 || index == activeFolderIndex)
                return false;

            activeFolderIndex = index;

            return true;
        }
    }
}
