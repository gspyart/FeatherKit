using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Операции над материалами в слотах объектов сцены: завести новую копию — в сцене или
    /// файлом в проекте — и отдать её выбранным слотам, подставить готовый материал,
    /// сохранить сценовую копию в проект.
    ///
    /// Сценовая копия нужна там, где объект один такой: правки видны сразу, соседей
    /// не задевают, и в проекте не заводится файл ради одной настройки. Понравилась —
    /// уезжает в проект обычным материалом.
    ///
    /// Окно — <see cref="SceneMaterialWindow"/>; здесь только то, что меняет сцену и проект.
    /// </summary>
    public static class SceneMaterialTools
    {
        public const string MakeUniqueLabel = "Уникальный материал в сцене";
        public const string ProjectCopyLabel = "Новый материал в проекте";
        public const string SaveToProjectLabel = "Сохранить материал в проект…";
        public const string AssignLabel = "Подставить материал";

        private const string FolderSettingKey = "FeatherKit.SceneMaterials.Folder";
        private const string DefaultFolder = "Assets";
        private const string CopySuffix = "_Copy";

        private static bool isContextMenuHandled;


        // Папка на проект, а не на машину: у каждого проекта свои места под материалы.
        private static string LastFolder
        {
            get
            {
                var folder = EditorUserSettings.GetConfigValue(FolderSettingKey);
                return !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder) ? folder : DefaultFolder;
            }
            set => EditorUserSettings.SetConfigValue(FolderSettingKey, value);
        }


        // ── Пункты меню ──────────────────────────────────

        [MenuItem("FeatherKit/" + MakeUniqueLabel)]
        private static void MakeUniqueInSelection() => MakeUnique(Selection.gameObjects);

        [MenuItem("FeatherKit/" + MakeUniqueLabel, true)]
        private static bool CanMakeUniqueInSelection() => Selection.gameObjects.Length > 0;


        // Пункт компонента Unity зовёт по разу на каждый выделенный объект, а копия нужна
        // одна на всё выделение: первый вызов делает работу, остальные пропускаются.
        [MenuItem("CONTEXT/Renderer/" + MakeUniqueLabel)]
        private static void MakeUniqueInContext(MenuCommand command)
        {
            if (isContextMenuHandled)
                return;

            isContextMenuHandled = true;
            EditorApplication.delayCall += () => isContextMenuHandled = false;

            var owner = ((Component)command.context).gameObject;
            MakeUnique(Selection.Contains(owner) ? Selection.gameObjects : new[] { owner });
        }


        [MenuItem("CONTEXT/Material/" + SaveToProjectLabel)]
        private static void SaveToProjectInContext(MenuCommand command) => SaveToProject((Material)command.context);

        [MenuItem("CONTEXT/Material/" + SaveToProjectLabel, true)]
        private static bool CanSaveToProjectInContext(MenuCommand command) =>
            command.context is Material material && !EditorUtility.IsPersistent(material);


        /// <summary>
        /// Каждому материалу выделенных объектов — одна сценовая копия на всех, кто его носит.
        /// Возвращает, сколько копий завелось.
        /// </summary>
        public static int MakeUnique(IReadOnlyList<GameObject> owners)
        {
            if (!IsSceneEditable())
                return 0;

            var slots = new List<MaterialSlot>();
            var groups = new List<MaterialSlotGroup>();

            CollectSlots(owners, false, slots);
            MaterialSlotGroup.Build(slots, groups);

            var created = 0;

            RunAsOneUndo(MakeUniqueLabel, () =>
            {
                foreach (var group in groups)
                {
                    if (group.Material == null)
                        continue;

                    AssignSceneCopy(group.Slots, group.Material, SuggestCopyName(group.Material, group.Slots));
                    created++;
                }
            });

            if (created == 0)
                Debug.LogWarning("Нечего делать: у выделенного нет материалов.");
            else
                Debug.Log($"Сценовых материалов заведено: {created}.");

            return created;
        }


        // ── Новая копия ──────────────────────────────────

        /// <summary>Заводит одну сценовую копию <paramref name="source"/> и отдаёт её всем слотам.</summary>
        public static Material AssignSceneCopy(IReadOnlyList<MaterialSlot> slots, Material source, string name)
        {
            var copy = CreateCopy(source, name);

            Undo.RegisterCreatedObjectUndo(copy, MakeUniqueLabel);
            Assign(slots, copy, MakeUniqueLabel);

            return copy;
        }


        /// <summary>Заводит копию <paramref name="source"/> файлом по пути и отдаёт её всем слотам.</summary>
        public static Material AssignProjectCopy(IReadOnlyList<MaterialSlot> slots, Material source, string path)
        {
            var asset = CreateCopy(source, Path.GetFileNameWithoutExtension(path));

            AssetDatabase.CreateAsset(asset, path);
            Assign(slots, asset, ProjectCopyLabel);

            return asset;
        }


        /// <summary>Имя новой копии: объект и материал, если носитель один, иначе материал с пометкой.</summary>
        public static string SuggestCopyName(Material source, IReadOnlyList<MaterialSlot> slots)
        {
            var owner = FindSingleOwner(slots);

            if (owner != null && EditorUtility.IsPersistent(source))
                return $"{owner.name}_{source.name}";

            return source.name.EndsWith(CopySuffix) ? source.name : source.name + CopySuffix;
        }

        private static GameObject FindSingleOwner(IReadOnlyList<MaterialSlot> slots)
        {
            GameObject owner = null;

            foreach (var slot in slots)
            {
                if (owner != null && slot.Owner.gameObject != owner)
                    return null;

                owner = slot.Owner.gameObject;
            }

            return owner;
        }


        // ── Готовый материал ─────────────────────────────

        /// <summary>Подставляет готовый материал — проектный или сценовый — всем слотам.</summary>
        public static void Assign(IReadOnlyList<MaterialSlot> slots, Material material, string label)
        {
            foreach (var slot in slots)
                Assign(slot, material, label);

            if (material != null && !EditorUtility.IsPersistent(material))
                WarnAboutPrefabInstances(slots);
        }

        private static void WarnAboutPrefabInstances(IReadOnlyList<MaterialSlot> slots)
        {
            var owners = new HashSet<GameObject>();

            foreach (var slot in slots)
                if (PrefabUtility.IsPartOfPrefabInstance(slot.Owner))
                    owners.Add(slot.Owner.gameObject);

            if (owners.Count > 0)
                Debug.LogWarning($"Частей префабов среди носителей: {owners.Count}. Ссылка на сценовый материал живёт только в сцене: Apply обнулит её в префабе.");
        }


        public static void Assign(MaterialSlot slot, Material material, string label)
        {
            Undo.RecordObject(slot.Owner, label);

            if (slot.Owner is Renderer renderer)
            {
                var materials = renderer.sharedMaterials;
                materials[slot.Index] = material;
                renderer.sharedMaterials = materials;
            }
            else if (slot.Owner is Graphic graphic)
            {
                graphic.material = material;
            }

            slot.Material = material;
            EditorUtility.SetDirty(slot.Owner);
        }


        // ── Обратно в проект ─────────────────────────────

        /// <summary>Сохраняет сценовый материал ассетом и подставляет его всем, кто им пользовался.</summary>
        public static Material SaveToProject(Material sceneMaterial)
        {
            var path = AskMaterialPath(sceneMaterial.name);

            if (string.IsNullOrEmpty(path))
                return null;

            // Ассетом становится копия: сам материал принадлежит сцене, и переселять его Unity не обещает.
            var asset = CreateCopy(sceneMaterial, Path.GetFileNameWithoutExtension(path));

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            var replaced = 0;
            RunAsOneUndo(SaveToProjectLabel, () => replaced = Replace(sceneMaterial, asset));

            Debug.Log($"Материал сохранён: {path}. Подставлен слотам: {replaced}.", asset);
            return asset;
        }

        private static int Replace(Material from, Material to)
        {
            var slots = new List<MaterialSlot>();
            CollectSceneSlots(slots);

            var replaced = 0;

            foreach (var slot in slots)
            {
                if (slot.Material != from)
                    continue;

                Assign(slot, to, SaveToProjectLabel);
                replaced++;
            }

            return replaced;
        }


        // ── Путь в проекте ───────────────────────────────

        /// <summary>Спрашивает, куда положить один материал. Пустая строка — отказались.</summary>
        public static string AskMaterialPath(string defaultName)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                SaveToProjectLabel, ToFileName(defaultName), "mat", "Куда положить материал", LastFolder);

            if (!string.IsNullOrEmpty(path))
                LastFolder = Path.GetDirectoryName(path)?.Replace('\\', '/');

            return path;
        }


        /// <summary>Спрашивает одну папку сразу под несколько материалов. Null — отказались или выбрали не в Assets.</summary>
        public static string AskMaterialsFolder()
        {
            var chosen = EditorUtility.SaveFolderPanel("Куда сложить материалы", Path.GetFullPath(LastFolder), string.Empty);

            if (string.IsNullOrEmpty(chosen))
                return null;

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            chosen = chosen.Replace('\\', '/');

            if (chosen != projectRoot + "/Assets" && !chosen.StartsWith(projectRoot + "/Assets/"))
            {
                Debug.LogWarning("Материалы кладутся только внутрь папки Assets этого проекта.");
                return null;
            }

            var folder = chosen.Substring(projectRoot.Length + 1);
            LastFolder = folder;

            return folder;
        }


        /// <summary>Свободный путь под материал в папке: занятое имя получает номер.</summary>
        public static string MakeMaterialPath(string folder, string name) =>
            AssetDatabase.GenerateUniqueAssetPath($"{folder}/{ToFileName(name)}.mat");


        private static string ToFileName(string name) => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));


        // ── Слоты ────────────────────────────────────────

        /// <summary>
        /// Слоты выделенных объектов, а с детьми — и всех вложенных. Каждый слот ровно один раз,
        /// даже если выделены и родитель, и его ребёнок.
        /// </summary>
        public static void CollectSlots(IReadOnlyList<GameObject> owners, bool includeChildren, List<MaterialSlot> into)
        {
            var seen = new HashSet<Component>();
            var renderers = new List<Renderer>();
            var graphics = new List<Graphic>();

            foreach (var owner in owners)
            {
                // Объект из окна проекта — ассет: сценовому материалу там негде храниться.
                if (owner == null || EditorUtility.IsPersistent(owner))
                    continue;

                AddUnique(includeChildren ? owner.GetComponentsInChildren<Renderer>(true) : owner.GetComponents<Renderer>(), renderers, seen);
                AddUnique(includeChildren ? owner.GetComponentsInChildren<Graphic>(true) : owner.GetComponents<Graphic>(), graphics, seen);
            }

            foreach (var renderer in renderers)
                AddSlots(renderer, into);

            foreach (var graphic in graphics)
                AddSlots(graphic, into);
        }

        private static void AddUnique<T>(T[] components, List<T> into, HashSet<Component> seen) where T : Component
        {
            foreach (var component in components)
                if (seen.Add(component))
                    into.Add(component);
        }


        /// <summary>Все слоты открытых сцен, а в режиме префаба — самого префаба.</summary>
        public static void CollectSceneSlots(List<MaterialSlot> into)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            var renderers = stage != null
                ? stage.prefabContentsRoot.GetComponentsInChildren<Renderer>(true)
                : Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);

            var graphics = stage != null
                ? stage.prefabContentsRoot.GetComponentsInChildren<Graphic>(true)
                : Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include);

            foreach (var renderer in renderers)
                if (IsListedInHierarchy(renderer))
                    AddSlots(renderer, into);

            foreach (var graphic in graphics)
                if (IsListedInHierarchy(graphic))
                    AddSlots(graphic, into);
        }

        // Спрятанные объекты — служебные у редактора и плагинов: выделить их нельзя, и считать их незачем.
        private static bool IsListedInHierarchy(Component component) =>
            (component.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0;


        private static void AddSlots(Renderer renderer, List<MaterialSlot> into)
        {
            var materials = renderer.sharedMaterials;

            for (var index = 0; index < materials.Length; index++)
                into.Add(new MaterialSlot { Owner = renderer, Index = index, Material = materials[index] });
        }

        private static void AddSlots(Graphic graphic, List<MaterialSlot> into)
        {
            // Пустой слот картинки — общий материал Unity на весь интерфейс, копировать нечего.
            if (graphic.material == null || graphic.material == Graphic.defaultGraphicMaterial)
                return;

            into.Add(new MaterialSlot { Owner = graphic, Index = 0, Material = graphic.material });
        }


        // ── Общее ────────────────────────────────────────

        public static MaterialOrigin GetOrigin(Material material)
        {
            if (material == null)
                return MaterialOrigin.Empty;

            if (!EditorUtility.IsPersistent(material))
                return MaterialOrigin.Scene;

            var path = AssetDatabase.GetAssetPath(material);

            if (path.StartsWith("Assets/"))
                return MaterialOrigin.Project;

            // Из пакета править можно только то, что лежит в проекте копией: встроенный или локальный пакет.
            var package = path.StartsWith("Packages/") ? UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path) : null;

            return package != null && (package.source == PackageSource.Embedded || package.source == PackageSource.Local)
                ? MaterialOrigin.Project
                : MaterialOrigin.BuiltIn;
        }


        public static void SelectOwners(IEnumerable<MaterialSlot> slots)
        {
            var owners = new List<Object>();

            foreach (var slot in slots)
                if (!owners.Contains(slot.Owner.gameObject))
                    owners.Add(slot.Owner.gameObject);

            if (owners.Count == 0)
                return;

            Selection.objects = owners.ToArray();
            EditorGUIUtility.PingObject(owners[0]);
        }


        public static bool IsSceneEditable()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
                return true;

            Debug.LogWarning("В режиме префаба сценового материала не бывает: префаб — ассет, и ссылку на объект сцены он не сохранит.");
            return false;
        }


        /// <summary>Всё, что сделано внутри, отменяется одним Ctrl+Z.</summary>
        public static void RunAsOneUndo(string label, System.Action action)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(label);

            var group = Undo.GetCurrentGroup();
            action();

            Undo.CollapseUndoOperations(group);
        }


        private static Material CreateCopy(Material source, string name)
        {
            var copy = Object.Instantiate(source);

            // Встроенные материалы Unity помечены «не сохранять», и копия унесла бы метку с собой.
            copy.hideFlags = HideFlags.None;
            copy.name = name;

            return copy;
        }
    }
}
