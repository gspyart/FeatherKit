using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Объекты из коробок, стены по линии и здания по контуру внутри экземпляров префабов. Меш у них — общий файл префаба:
    /// - экземпляр без своих правок коробок, лестниц, повторов, проёмов и точек сам ничего не пересобирает и берёт меш префаба —
    ///   иначе у каждого экземпляра появлялся бы свой файл меша и лишнее переопределение
    /// - устарел сам префаб (поменялся расчёт, файл меша не записался или откатили) — пересобирается префаб; не помогло — по кругу не гоняем
    /// - пересобирается ближайший префаб — тот, с которым сверяли (у варианта — сам вариант). Объект в нём из базового или вложенного префаба
    ///   разбирается так же: со своими правками собирается в нём, без правок берёт меш того префаба, а отставший тот пересобирается
    ///   следом, когда этот уже записан
    /// Экземпляр со своими правками геометрии или прорезанный вырезом не из этого префаба — как обычный объект: свой файл меша.
    /// Вырез убрали — снова меш префаба.
    /// Сверка правок экземпляра дорогая (около миллисекунды), а проверка идёт на каждую правку в сцене: ответ помнится,
    /// пока экземпляр не трогали (<see cref="LiveBuilderRebuildQueue"/> говорит, кого трогали).
    /// </summary>
    public static class BoxModelPrefabInstances
    {
        // Поле подписи — оно меняется при пересборке и геометрией не считается.
        private const string BuiltSignatureProperty = "builtSignature";

        // Префаб и в каком состоянии его уже пересобирали в эту сессию: если и после этого не сходится — по кругу не гоняем.
        private static readonly Dictionary<string, string> RefreshedPrefabStates = new();

        // Отставшие базовые и вложенные префабы, найденные при пересборке другого префаба, — на после его записи.
        private static readonly List<string> PrefabsToRefreshAfterCurrent = new();

        // Идёт пересборка префаба: найденные внутри отставшие префабы ждут в очереди.
        private static bool isRefreshingPrefab;

        // Уже сверенные объекты в экземплярах: объект → его экземпляр, берёт ли он меш префаба и резал ли его чужой вырез при сверке.
        private static readonly Dictionary<EntityId, (EntityId InstanceRoot, bool UsesPrefabGeometry, bool IsCutFromOutside)> CheckedOwners = new();


        // ── Чей меш ──────────────────────────────────────────────────────

        // Шаг пересборщика: меш у постройки — от префаба? Тогда сам он ничего не собирает. Только что сверили — отставший префаб пересобрать
        // (isPrefabUpToDate, prefabState — у каждого вида построек свои) и снять с экземпляра лишние правки меша.
        public static bool TryUsePrefabGeometry(Component owner, System.Func<bool> isPrefabUpToDate, System.Func<string> prefabState)
        {
            if (!UsesPrefabGeometry(owner, out var isRechecked))
                return false;

            if (isRechecked)
            {
                RefreshPrefabIfStale(owner, isPrefabUpToDate(), prefabState());
                TakePrefabGeometry(owner);
            }

            return true;
        }


        // Объект в экземпляре префаба, его геометрию на экземпляре не трогали и не режет вырез снаружи — меш даёт префаб.
        // isRechecked — сверили только что (экземпляр трогали, ещё не сверяли или вырез снаружи появился либо пропал):
        // тогда стоит снять лишние переопределения меша.
        public static bool UsesPrefabGeometry(Component owner, out bool isRechecked)
        {
            isRechecked = false;

            if (!PrefabUtility.IsPartOfPrefabInstance(owner))
                return false;

            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(owner);

            // Вырез — чужой объект, правки экземпляра его не видят: проверка — каждый раз, а не из памяти.
            var isCutFromOutside = IsCutFromOutside(owner, instanceRoot);

            if (CheckedOwners.TryGetValue(owner.GetEntityId(), out var checkedOwner) && checkedOwner.IsCutFromOutside == isCutFromOutside)
                return checkedOwner.UsesPrefabGeometry;

            var usesPrefabGeometry = !isCutFromOutside && !HasGeometryOverrides(owner, instanceRoot);
            CheckedOwners[owner.GetEntityId()] = (instanceRoot.GetEntityId(), usesPrefabGeometry, isCutFromOutside);
            isRechecked = true;
            return usesPrefabGeometry;
        }

        // Режет ли объект вырез не из этого экземпляра префаба: со сцены, из другого префаба или добавленный в экземпляр поверх префаба.
        private static bool IsCutFromOutside(Component owner, GameObject instanceRoot)
        {
            var bounds = new Bounds();
            var hasBounds = false;

            foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>())
            {
                var rendererBounds = GeometryCutterLookup.WorldBoundsOf(renderer);

                if (rendererBounds.size == Vector3.zero)
                    continue;

                if (hasBounds)
                    bounds.Encapsulate(rendererBounds);
                else
                    bounds = rendererBounds;

                hasBounds = true;
            }

            if (!hasBounds)
                return false;

            foreach (var cutter in GeometryCutterLookup.CuttersFor(owner, bounds))
                if (!PrefabUtility.IsPartOfPrefabInstance(cutter) || PrefabUtility.GetOutermostPrefabInstanceRoot(cutter) != instanceRoot)
                    return true;

            return false;
        }


        // Объект поменяли — его экземпляр префаба сверить заново. Не в экземпляре — сверки не касается.
        public static void ForgetCheck(GameObject changed)
        {
            if (CheckedOwners.Count == 0 || !PrefabUtility.IsPartOfPrefabInstance(changed))
                return;

            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(changed);
            var rootId = instanceRoot != null ? instanceRoot.GetEntityId() : default;
            var forgotten = new List<EntityId>();

            foreach (var (ownerId, checkedOwner) in CheckedOwners)
                if (checkedOwner.InstanceRoot == rootId)
                    forgotten.Add(ownerId);

            foreach (var ownerId in forgotten)
                CheckedOwners.Remove(ownerId);
        }


        // Создали, удалили, переставили, отменили, открыли сцену — сверить всех заново.
        public static void ForgetAllChecks()
        {
            CheckedOwners.Clear();
        }

        private static bool HasGeometryOverrides(Component owner, GameObject instanceRoot)
        {
            var ownerTransform = owner.transform;

            foreach (var added in PrefabUtility.GetAddedGameObjects(instanceRoot))
                if (IsInside(added.instanceGameObject.transform, ownerTransform))
                    return true;

            foreach (var added in PrefabUtility.GetAddedComponents(instanceRoot))
                if (IsInside(added.instanceComponent.transform, ownerTransform))
                    return true;

            foreach (var removed in PrefabUtility.GetRemovedComponents(instanceRoot))
                if (removed.containingInstanceGameObject != null && IsInside(removed.containingInstanceGameObject.transform, ownerTransform, true))
                    return true;

            foreach (var removed in PrefabUtility.GetRemovedGameObjects(instanceRoot))
                if (removed.parentOfRemovedGameObjectInInstance != null && IsInside(removed.parentOfRemovedGameObjectInInstance.transform, ownerTransform, true))
                    return true;

            foreach (var overridden in PrefabUtility.GetObjectOverrides(instanceRoot, false))
            {
                var overriddenTransform = TransformOf(overridden.instanceObject);
                var isGeneratedPart = overriddenTransform != null && overriddenTransform.GetComponentInParent<SplineBuildingPart>() != null;

                if (overriddenTransform != null && !isGeneratedPart && IsInside(overriddenTransform, ownerTransform))
                    return true;
            }

            return HasOwnSettingsChanged(owner);
        }

        // Настройки на самом объекте, от которых зависит меш: всё, кроме подписи, у него самого и у его сплайна.
        private static bool HasOwnSettingsChanged(Component owner)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(owner);
            var container = owner.GetComponent<SplineContainer>();
            var sourceContainer = container != null ? PrefabUtility.GetCorrespondingObjectFromSource(container) : null;
            var modifications = PrefabUtility.GetPropertyModifications(owner.gameObject);

            if (modifications == null)
                return false;

            foreach (var modification in modifications)
            {
                var isOwnSetting = modification.target == source && modification.propertyPath != BuiltSignatureProperty;
                var isSplinePoint = sourceContainer != null && modification.target == sourceContainer;

                if (isOwnSetting || isSplinePoint)
                    return true;
            }

            return false;
        }


        // ── Меш префаба ──────────────────────────────────────────────────

        // Снимает с экземпляра переопределения меша, материалов и подписи (у здания — и его собранных частей целиком) — остаются меши префаба.
        // Остальные правки рендерера и коллайдера (тени, триггер, физ. материал) — человека, их не трогаем.
        private static void TakePrefabGeometry(Component owner)
        {
            var generatedComponents = new List<Object> { owner };

            foreach (var part in owner.GetComponentsInChildren<SplineBuildingPart>(true))
                generatedComponents.AddRange(new Object[] { part, part.GetComponent<MeshFilter>(), part.GetComponent<MeshRenderer>(), part.GetComponent<MeshCollider>() });

            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(owner);

            foreach (var overridden in PrefabUtility.GetObjectOverrides(instanceRoot, false))
                if (generatedComponents.Contains(overridden.instanceObject) && !PrefabUtility.IsAddedComponentOverride(overridden.instanceObject))
                    PrefabUtility.RevertObjectOverride(overridden.instanceObject, InteractionMode.AutomatedAction);

            RevertPropertyOverride(owner.GetComponent<MeshFilter>(), "m_Mesh");
            RevertPropertyOverride(owner.GetComponent<MeshRenderer>(), "m_Materials");
            RevertPropertyOverride(owner.GetComponent<MeshCollider>(), "m_Mesh");
        }

        private static void RevertPropertyOverride(Object target, string propertyPath)
        {
            if (target == null || PrefabUtility.IsAddedComponentOverride(target))
                return;

            var property = new SerializedObject(target).FindProperty(propertyPath);

            if (property != null && property.prefabOverride)
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
        }


        // Префаб собран не по своим коробкам или его файл меша отстал — пересобрать префаб.
        // Сверяют на экземпляре без правок с ближайшим префабом (у варианта — с самим вариантом): его и пересобираем.
        private static void RefreshPrefabIfStale(Component owner, bool isPrefabUpToDate, string prefabState)
        {
            if (isPrefabUpToDate)
                return;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(owner);
            var prefabPath = AssetDatabase.GetAssetPath(source);

            if (string.IsNullOrEmpty(prefabPath) || RefreshedPrefabStates.TryGetValue(prefabPath, out var refreshedState) && refreshedState == prefabState)
                return;

            RefreshedPrefabStates[prefabPath] = prefabState;

            // Отставший базовый префаб нашёлся при пересборке варианта — после записи варианта: иначе загруженный вариант
            // записал бы прежние значения базового как свои правки.
            if (isRefreshingPrefab)
            {
                PrefabsToRefreshAfterCurrent.Add(prefabPath);
                return;
            }

            isRefreshingPrefab = true;

            try
            {
                RebuildPrefab(prefabPath);

                // Пересборка базового может найти отставший ещё глубже — очередь растёт, пока не кончится цепочка.
                for (var index = 0; index < PrefabsToRefreshAfterCurrent.Count; index++)
                    RebuildPrefab(PrefabsToRefreshAfterCurrent[index]);
            }
            finally
            {
                PrefabsToRefreshAfterCurrent.Clear();
                isRefreshingPrefab = false;
            }
        }

        // Пересобирает постройки в файле префаба. Свои — всегда; из базового или вложенного префаба — как в сцене:
        // без своих правок берут меш того префаба (отставший — в очередь), со своими — собираются в этом.
        private static void RebuildPrefab(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);

            // Меши построек префаба — в его папке, вырезы — его собственные, а не сцены.
            BoxModelMeshStorage.BeginLoadedPrefab(contents.scene, prefabPath);

            try
            {
                foreach (var model in contents.GetComponentsInChildren<BoxModel>(true))
                    if (!BoxModelRebuilder.TryUsePrefabGeometry(model))
                        BoxModelRebuilder.Rebuild(model);

                foreach (var wall in contents.GetComponentsInChildren<SplineWall>(true))
                    if (!SplineWallRebuilder.TryUsePrefabGeometry(wall))
                        SplineWallRebuilder.Rebuild(wall);

                foreach (var building in contents.GetComponentsInChildren<SplineBuilding>(true))
                    if (!SplineBuildingRebuilder.TryUsePrefabGeometry(building))
                        SplineBuildingRebuilder.Rebuild(building);

                BoxModelMeshSaving.SaveMeshFiles(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                BoxModelMeshStorage.EndLoadedPrefab(contents.scene);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Узел под объектом: сам объект не считается, если не сказано иначе.
        private static bool IsInside(Transform node, Transform owner, bool includesOwner = false)
        {
            return node != null && node.IsChildOf(owner) && (includesOwner || node != owner);
        }

        private static Transform TransformOf(Object instanceObject)
        {
            return instanceObject switch
            {
                Component component => component.transform,
                GameObject gameObject => gameObject.transform,
                _ => null
            };
        }
    }
}
