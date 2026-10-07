using System;
using System.Collections.Generic;
using System.Linq;
using FeatherKit.Helpers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Переделывает ОДИН префаб в вариант шаблона. Части, которые нашлись в шаблоне по тому же
    /// пути в иерархии, начинают приходить из него, а значения префаба остаются на них
    /// переопределениями. Своё остаётся добавленным поверх шаблона, лишнее из шаблона по желанию
    /// убирается. Результат ложится поверх того же файла, поэтому GUID префаба не меняется.
    ///
    /// Обещание одно: префаб выглядит и работает как раньше. Поэтому до сохранения каждая
    /// уцелевшая часть сверяется с тем, какой была, и при любом расхождении или пропаже
    /// файл не трогается.
    ///
    /// Ссылки в проекте он НЕ чинит. У объектов внутри префаба сменились внутренние номера,
    /// и какие именно, он отдаёт в итоге — по ним чинит <see cref="PrefabReferenceRemapper"/>.
    ///
    /// Живёт на один префаб: создал, переделал, сохранил или выбросил. Работает в скрытой
    /// сцене превью, открытые сцены не трогает.
    /// </summary>
    public sealed class PrefabToVariantConverter : IDisposable
    {
        // Объект с чужим именем Unity ни с чем в шаблоне не сопоставит.
        private const string ProtectedNameSuffix = " [FeatherKit: не сопоставлять]";

        // Предохранитель от поломанной цепочки вариантов: настоящие столько слоёв не бывают.
        private const int MaxUnpackDepth = 32;

        private const string PathSeparator = " › ";

        private readonly GameObject prefab;
        private readonly GameObject template;
        private readonly bool removeTemplateExtras;
        private readonly string prefabPath;
        private readonly string prefabGuid;
        private readonly PrefabConversionResult result;
        private readonly List<PrefabPartSnapshot> snapshots = new List<PrefabPartSnapshot>();
        private readonly Dictionary<GameObject, string> protectedNames = new Dictionary<GameObject, string>();

        private Scene workScene;
        private GameObject instance;
        private bool isReadyToSave;


        public PrefabToVariantConverter(GameObject prefab, GameObject template, bool removeTemplateExtras)
        {
            this.prefab = prefab;
            this.template = template;
            this.removeTemplateExtras = removeTemplateExtras;

            prefabPath = AssetDatabase.GetAssetPath(prefab);
            prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            result = new PrefabConversionResult(prefabPath);
        }


        public PrefabConversionResult Result => result;


        // ── Проверки ─────────────────────────────────

        /// <summary>Годится ли префаб в шаблоны. Отказ — с причиной словами.</summary>
        public static bool CanBeTemplate(GameObject template, out string reason)
        {
            reason = template == null ? "Выбери шаблон — префаб, от которого пойдут варианты."
                : !IsPrefabAssetRoot(template) ? "Шаблоном может быть только префаб из окна Project."
                : CountMissingScripts(template) > 0 ? "В шаблоне есть пропавшие скрипты — сначала почини их."
                : null;

            return reason == null;
        }


        /// <summary>Можно ли переделать этот префаб в вариант шаблона. Отказ — с причиной словами.</summary>
        public static bool CanConvert(GameObject prefab, GameObject template, out string reason)
        {
            reason = FindConvertRefusal(prefab, template);
            return reason == null;
        }

        private static string FindConvertRefusal(GameObject prefab, GameObject template)
        {
            if (prefab == null)
                return "Префаб не найден — возможно, его удалили.";

            if (!CanBeTemplate(template, out var templateRefusal))
                return templateRefusal;

            if (prefab == template)
                return "Это сам шаблон.";

            switch (PrefabUtility.GetPrefabAssetType(prefab))
            {
                case PrefabAssetType.Model:
                    return "Это модель: её префаб живёт внутри файла модели, и вариантом его не сделать.";

                case PrefabAssetType.NotAPrefab:
                case PrefabAssetType.MissingAsset:
                    return "Это не префаб.";
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            if (!PrefabReferenceRemapper.IsEditableAssetPath(path))
                return "Лежит в чужом пакете — менять его нельзя.";

            if (IsDerivedFromTemplate(prefab, template, out var directBase))
                return directBase == template
                    ? "Уже вариант этого шаблона."
                    : $"Уже происходит от шаблона — через «{directBase.name}».";

            if (AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(template), true).Contains(path))
                return "Шаблон сам собран из этого префаба — получилась бы петля.";

            if (CountMissingScripts(prefab) > 0)
                return "В префабе есть пропавшие скрипты: Unity не умеет их переносить. Сначала почини или убери их.";

            return null;
        }

        // Цепочка вариантов: вариант → его основа → основа основы. Шаблон где-то в ней —
        // префаб уже от него происходит.
        private static bool IsDerivedFromTemplate(GameObject prefab, GameObject template, out GameObject directBase)
        {
            directBase = PrefabUtility.GetCorrespondingObjectFromSource(prefab);

            for (var current = directBase; current != null; current = PrefabUtility.GetCorrespondingObjectFromSource(current))
            {
                if (current == template)
                    return true;
            }

            return false;
        }


        public static bool IsPrefabAssetRoot(GameObject gameObject) =>
            gameObject != null && PrefabUtility.IsPartOfPrefabAsset(gameObject) && gameObject.transform.parent == null;


        // ── Переделка ────────────────────────────────

        /// <summary>
        /// Переделывает копию префаба в скрытой сцене и заполняет итог: что с чем совпало, что
        /// лишнее, не потерялось и не изменилось ли что. Файл не трогается — это и есть предпросмотр.
        /// </summary>
        public PrefabConversionResult Convert()
        {
            if (!CanConvert(prefab, template, out var refusal))
            {
                result.Skip(refusal);
                return result;
            }

            try
            {
                OpenWorkScene();
                RecordParts();
                UnpackToPlainObject();
                ProtectForeignNestedPrefabs();
                ConvertToTemplateInstance();
                RestoreProtectedNames();
                ReportRecordedParts();
                HandleTemplateExtras();
                VerifyNothingChanged();
            }
            catch (Exception exception)
            {
                result.Fail("Unity не смог переделать префаб: " + exception.Message);
            }

            isReadyToSave = result.Status == PrefabConversionStatus.Ready;
            return result;
        }

        // Сцена превью, а не открытая сцена: её никто не видит и не сохранит. Префаб кладётся
        // под пустой держатель — корень сцены превью Unity считает корнем префаба, а его
        // заменять экземпляром шаблона запрещено.
        private void OpenWorkScene()
        {
            workScene = EditorSceneManager.NewPreviewScene();

            var holder = new GameObject("PrefabToVariantHolder");
            SceneManager.MoveGameObjectToScene(holder, workScene);

            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        }

        // Запоминаем всё ДО распаковки: пока копия связана с префабом, у каждой её части
        // виден номер в файле префаба — по нему потом и чинят ссылки.
        private void RecordParts()
        {
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
            {
                var gameObject = transform.gameObject;
                snapshots.Add(new PrefabPartSnapshot(gameObject, FindFileIdInPrefab(gameObject), DescribePath(gameObject)));

                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component != null)
                        snapshots.Add(new PrefabPartSnapshot(component, FindFileIdInPrefab(component), DescribePath(component)));
                }
            }
        }

        // Номер части в файле ЭТОГО префаба. Ноль — если копия с ним не связана.
        private long FindFileIdInPrefab(Object part)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(part);
            if (source == null)
                return 0;

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out var guid, out long fileId) && guid == prefabGuid
                ? fileId
                : 0;
        }

        // Вариант распаковывается слоями: снял вариант — под ним экземпляр его основы.
        // Снимаем, пока корень не станет обычным объектом; вложенные префабы остаются вложенными.
        private void UnpackToPlainObject()
        {
            for (var depth = 0; PrefabUtility.IsOutermostPrefabInstanceRoot(instance); depth++)
            {
                if (depth >= MaxUnpackDepth)
                    throw new InvalidOperationException("цепочка вариантов слишком длинная.");

                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            }
        }

        // Unity сопоставляет по пути и не смотрит, чем объект является: вложенный префаб,
        // встретив в шаблоне обычный объект с тем же именем, склеивается с ним и перестаёт
        // быть вложенным префабом. Такие на время переименовываем — они остаются своими,
        // добавленными поверх шаблона. Встреча с ТЕМ ЖЕ вложенным префабом — законное
        // совпадение, его не трогаем.
        private void ProtectForeignNestedPrefabs()
        {
            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
            {
                var gameObject = transform.gameObject;
                if (gameObject == instance || !PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject))
                    continue;

                var counterpart = template.transform.Find(PathFromRoot(gameObject));
                if (counterpart == null || IsSameNestedPrefab(gameObject, counterpart.gameObject))
                    continue;

                protectedNames[gameObject] = gameObject.name;
                gameObject.name += ProtectedNameSuffix;
            }
        }

        private static bool IsSameNestedPrefab(GameObject nestedRoot, GameObject counterpart)
        {
            return PrefabUtility.IsAnyPrefabInstanceRoot(counterpart)
                   && PrefabUtility.GetCorrespondingObjectFromSource(counterpart) == PrefabUtility.GetCorrespondingObjectFromSource(nestedRoot);
        }

        private void ConvertToTemplateInstance()
        {
            var settings = new ConvertToPrefabInstanceSettings
            {
                // Сопоставление по имени Unity делает с потерями: несовпавший объект пропадает
                // целиком. Проверено — поэтому только по пути.
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true,
                recordPropertyOverridesOfMatches = true,
                changeRootNameToAssetName = false,
                logInfo = false
            };

            PrefabUtility.ConvertToPrefabInstance(instance, template, settings, InteractionMode.AutomatedAction);
        }

        // Имя вложенного корня — переопределение его экземпляра. Если вернули ровно имя
        // из его префаба, запись лишняя: снимаем, чтобы в варианте не висел шум.
        private void RestoreProtectedNames()
        {
            foreach (var pair in protectedNames)
            {
                var gameObject = pair.Key;
                if (gameObject == null)
                    continue;

                gameObject.name = pair.Value;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (source != null && source.name == pair.Value)
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(gameObject).FindProperty("m_Name"), InteractionMode.AutomatedAction);
            }
        }

        // Пропавшая часть — повод не сохранять вовсе: ссылки на неё пропали бы вместе с ней.
        private void ReportRecordedParts()
        {
            var lostCount = 0;

            foreach (var snapshot in snapshots)
            {
                var isLost = snapshot.Target == null;
                if (isLost)
                    lostCount++;

                // Transform неотделим от объекта: отдельная строка про него — только шум.
                if (snapshot.IsTransform)
                    continue;

                var kind = isLost ? PrefabPartMatchKind.Lost
                    : IsOwnPart(snapshot.Target) ? PrefabPartMatchKind.Own
                    : PrefabPartMatchKind.FromTemplate;

                result.AddPart(new PrefabPartMatch(snapshot.Path, snapshot.IsGameObject, kind));
            }

            if (lostCount > 0)
                result.Fail($"При переделке пропало частей: {lostCount} (отмечены в списке). Такой результат не сохраняется.");
        }

        // Своё — то, что осталось добавленным поверх шаблона, или лежит внутри такого.
        private bool IsOwnPart(Object part)
        {
            if (part is Component component)
            {
                if (PrefabUtility.IsAddedComponentOverride(component))
                    return true;

                part = component.gameObject;
            }

            for (var current = ((GameObject)part).transform; current != null && current.gameObject != instance; current = current.parent)
            {
                if (PrefabUtility.IsAddedGameObjectOverride(current.gameObject))
                    return true;
            }

            return false;
        }


        // ── Лишнее из шаблона ────────────────────────

        private void HandleTemplateExtras()
        {
            if (result.Status != PrefabConversionStatus.Ready)
                return;

            var extras = FindTemplateExtras();

            if (removeTemplateExtras)
            {
                RemoveExtraComponents(extras.OfType<Component>().ToList());
                RemoveExtraGameObjects(extras.OfType<GameObject>().ToList());
                return;
            }

            foreach (var extra in extras)
                result.AddPart(new PrefabPartMatch(DescribePath(extra), extra is GameObject, PrefabPartMatchKind.TemplateExtraKept));
        }

        // Лишнее — всё, чего не было у префаба до переделки. Лишний объект уносит с собой
        // и детей, и компоненты, поэтому считаем только верхние.
        private List<Object> FindTemplateExtras()
        {
            var recordedIds = new HashSet<ulong>(snapshots.Select(snapshot => snapshot.InstanceId));
            var extras = new List<Object>();

            foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
            {
                var gameObject = transform.gameObject;

                if (!recordedIds.Contains(gameObject.GetStableId()))
                {
                    if (recordedIds.Contains(transform.parent.gameObject.GetStableId()))
                        extras.Add(gameObject);

                    continue;
                }

                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component != null && !recordedIds.Contains(component.GetStableId()))
                        extras.Add(component);
                }
            }

            return extras;
        }

        // Снимаем по очереди зависимостей: компонент, нужный другому лишнему, ждёт, пока
        // уберут того, кому он нужен. Нужный своему — так не бывает: у префаба он был бы.
        private void RemoveExtraComponents(List<Component> components)
        {
            bool removedAny;

            do
            {
                removedAny = false;

                for (var index = components.Count - 1; index >= 0; index--)
                {
                    var component = components[index];
                    if (IsRequiredByOtherComponent(component))
                        continue;

                    var path = DescribePath(component);
                    Object.DestroyImmediate(component);

                    if (component != null)
                        continue;

                    result.AddPart(new PrefabPartMatch(path, false, PrefabPartMatchKind.TemplateExtraRemoved));
                    components.RemoveAt(index);
                    removedAny = true;
                }
            }
            while (removedAny);

            foreach (var component in components)
            {
                var path = DescribePath(component);
                result.AddPart(new PrefabPartMatch(path, false, PrefabPartMatchKind.TemplateExtraKept));
                result.AddWarning($"«{path}» из шаблона убрать не вышло: на нём держится другой компонент. Он достанется префабу.");
            }
        }

        // Зависимость «мне нужен такой-то компонент» объявляется атрибутом RequireComponent,
        // и снять нужный раньше нуждающегося Unity не даст.
        private static bool IsRequiredByOtherComponent(Component component)
        {
            var type = component.GetType();

            foreach (var other in component.GetComponents<Component>())
            {
                if (other == null || other == component)
                    continue;

                foreach (RequireComponent requirement in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (IsRequiredType(requirement.m_Type0, type) || IsRequiredType(requirement.m_Type1, type) || IsRequiredType(requirement.m_Type2, type))
                        return true;
                }
            }

            return false;
        }

        private static bool IsRequiredType(Type requiredType, Type type) => requiredType != null && requiredType.IsAssignableFrom(type);

        // Удаление объекта из экземпляра Unity записывает переопределением «удалён»:
        // в варианте видно, от чего префаб отказался, и отказ можно отменить.
        private void RemoveExtraGameObjects(List<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                var path = DescribePath(gameObject);

                try
                {
                    Object.DestroyImmediate(gameObject);
                }
                catch (InvalidOperationException)
                {
                    // Не дали удалить — ниже это станет предупреждением.
                }

                if (gameObject == null)
                {
                    result.AddPart(new PrefabPartMatch(path, true, PrefabPartMatchKind.TemplateExtraRemoved));
                    continue;
                }

                result.AddPart(new PrefabPartMatch(path, true, PrefabPartMatchKind.TemplateExtraKept));
                result.AddWarning($"«{path}» из шаблона убрать не вышло. Он достанется префабу.");
            }
        }


        // ── Сверка ───────────────────────────────────

        // Переделка обещает одно: префаб выглядит и работает как раньше. Расхождение значит,
        // что Unity что-то переписал, и сохранять такое нельзя.
        private void VerifyNothingChanged()
        {
            if (result.Status != PrefabConversionStatus.Ready)
                return;

            var changedPaths = snapshots.Where(snapshot => snapshot.HasChangedValues()).Select(snapshot => snapshot.Path).ToList();

            if (changedPaths.Count > 0)
            {
                var shown = string.Join(", ", changedPaths.Take(5));
                var rest = changedPaths.Count > 5 ? $" и ещё {changedPaths.Count - 5}" : "";
                result.Fail($"После переделки изменились значения: {shown}{rest}. Такой результат не сохраняется.");
                return;
            }

            WarnAboutChangedOrder();
        }

        // Порядок детей и компонентов в значениях не виден, а интерфейс раскладывается
        // по нему. Переставлять части, пришедшие из шаблона, Unity не даёт, — только предупреждаем.
        private void WarnAboutChangedOrder()
        {
            foreach (var snapshot in snapshots)
            {
                if (snapshot.HasChangedChildrenOrder())
                    result.AddWarning($"У «{snapshot.Path}» поменялся порядок детей.");

                if (snapshot.HasChangedComponentOrder())
                    result.AddWarning($"У «{snapshot.Path}» поменялся порядок компонентов.");
            }
        }


        // ── Сохранение ───────────────────────────────

        /// <summary>
        /// Сохраняет переделанное поверх файла префаба и собирает, какие внутренние номера
        /// объектов сменились. Только после удачного <see cref="Convert"/>.
        /// </summary>
        public bool Save()
        {
            if (!isReadyToSave)
                return false;

            // Место каждой части запоминаем ДО сохранения: объекты внутри вложенных префабов
            // Unity при сохранении пересоздаёт, и спросить живой объект после него уже нельзя.
            var placesBeforeSave = snapshots
                .Where(snapshot => snapshot.FileIdInPrefab != 0 && snapshot.Target != null)
                .ToDictionary(snapshot => snapshot, snapshot => DescribePlace(snapshot.Target, instance));

            try
            {
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out var success);

                if (!success)
                {
                    result.Fail("Unity не смог сохранить префаб.");
                    return false;
                }
            }
            catch (Exception exception)
            {
                result.Fail("Unity не смог сохранить префаб: " + exception.Message);
                return false;
            }

            CollectFileIdChanges(placesBeforeSave);
            result.MarkConverted();
            isReadyToSave = false;
            return true;
        }

        // Новый номер спрашиваем у сохранённого файла, находя в нём каждую часть на том же
        // месте. Старый запомнен до распаковки — пара и есть то, по чему чинят ссылки.
        //
        // Номера надо брать именно у Unity, а не высчитывать: пересохраняя префаб, она может
        // отдать старый номер вложенного экземпляра корню шаблона, и у всего вложенного
        // номера сменятся, хотя сам он не менялся.
        private void CollectFileIdChanges(Dictionary<PrefabPartSnapshot, string> placesBeforeSave)
        {
            var savedParts = CollectPartsByPlace(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            var missingCount = 0;

            foreach (var pair in placesBeforeSave)
            {
                if (!savedParts.TryGetValue(pair.Value, out var savedPart)
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(savedPart, out var guid, out long newFileId)
                    || guid != prefabGuid)
                {
                    missingCount++;
                    continue;
                }

                if (newFileId != pair.Key.FileIdInPrefab)
                    result.AddFileIdChange(pair.Key.FileIdInPrefab, newFileId);
            }

            if (missingCount > 0)
                result.AddWarning($"В сохранённом префабе не нашлось частей: {missingCount}. Ссылки на них в проекте проверь руками.");
        }

        private static Dictionary<string, Object> CollectPartsByPlace(GameObject root)
        {
            var parts = new Dictionary<string, Object>();

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                parts[DescribePlace(transform.gameObject, root)] = transform.gameObject;

                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null)
                        parts[DescribePlace(component, root)] = component;
                }
            }

            return parts;
        }


        // ── Общие помощники ──────────────────────────

        // Место части в иерархии: номера детей от корня и номер компонента на объекте.
        // Номерами, а не именами — у соседей имена бывают одинаковыми. Порядок сохранение
        // не меняет, поэтому место до и после совпадает.
        private static string DescribePlace(Object part, GameObject root)
        {
            var gameObject = part is Component component ? component.gameObject : (GameObject)part;
            var siblingIndices = new List<int>();

            for (var current = gameObject.transform; current != root.transform; current = current.parent)
                siblingIndices.Add(current.GetSiblingIndex());

            siblingIndices.Reverse();
            var place = string.Join("/", siblingIndices);

            return part is Component placedComponent
                ? place + "#" + Array.IndexOf(gameObject.GetComponents<Component>(), placedComponent)
                : place;
        }


        private string DescribePath(Object part)
        {
            if (part is Component component)
                return DescribePath(component.gameObject) + PathSeparator + ObjectNames.NicifyVariableName(component.GetType().Name);

            var gameObject = (GameObject)part;
            return gameObject == instance ? prefab.name : prefab.name + PathSeparator + PathFromRoot(gameObject).Replace("/", PathSeparator);
        }


        // Путь от корня копии — в том виде, в каком его понимает Transform.Find.
        private string PathFromRoot(GameObject gameObject)
        {
            var names = new List<string>();

            for (var current = gameObject.transform; current != null && current.gameObject != instance; current = current.parent)
                names.Add(current.name);

            names.Reverse();
            return string.Join("/", names);
        }


        private static int CountMissingScripts(GameObject root) =>
            root.GetComponentsInChildren<Transform>(true).Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));


        // ── IDisposable ──────────────────────────────

        public void Dispose()
        {
            if (workScene.IsValid())
                EditorSceneManager.ClosePreviewScene(workScene);
        }


        /// <summary>
        /// Часть префаба, какой она была до переделки: сам объект, его номер в файле префаба,
        /// значения и порядок детей и компонентов. По нему потом сверяют, что ничего
        /// не изменилось, и связывают старый номер с новым.
        /// </summary>
        private sealed class PrefabPartSnapshot
        {
            private readonly string valuesJson;
            private readonly ulong[] childIds;
            private readonly ulong[] componentIds;


            public PrefabPartSnapshot(Object target, long fileIdInPrefab, string path)
            {
                Target = target;
                InstanceId = target.GetStableId();
                FileIdInPrefab = fileIdInPrefab;
                Path = path;

                // Ссылки на другие объекты в этой записи — номерами объектов в памяти, а они
                // переделку переживают. Поэтому совпадение строк значит совпадение и ссылок.
                valuesJson = EditorJsonUtility.ToJson(target);

                if (target is GameObject gameObject)
                {
                    childIds = CollectChildIds(gameObject);
                    componentIds = CollectComponentIds(gameObject);
                }
            }


            public Object Target { get; }

            public ulong InstanceId { get; }

            public long FileIdInPrefab { get; }

            public string Path { get; }

            public bool IsGameObject => Target is GameObject;

            public bool IsTransform => Target is Transform;


            public bool HasChangedValues() => Target != null && EditorJsonUtility.ToJson(Target) != valuesJson;


            // Сравниваем только прежних: лишнее из шаблона, если его оставили, порядком не считается.
            public bool HasChangedChildrenOrder() =>
                childIds != null && Target != null && !KeepOnly(CollectChildIds((GameObject)Target), childIds).SequenceEqual(childIds);


            public bool HasChangedComponentOrder() =>
                componentIds != null && Target != null && !KeepOnly(CollectComponentIds((GameObject)Target), componentIds).SequenceEqual(componentIds);


            private static IEnumerable<ulong> KeepOnly(IEnumerable<ulong> current, ulong[] original) => current.Where(original.Contains);


            private static ulong[] CollectChildIds(GameObject gameObject) =>
                gameObject.transform.Cast<Transform>().Select(child => child.gameObject.GetStableId()).ToArray();


            private static ulong[] CollectComponentIds(GameObject gameObject) =>
                gameObject.GetComponents<Component>().Where(component => component != null).Select(component => component.GetStableId()).ToArray();
        }
    }
}
