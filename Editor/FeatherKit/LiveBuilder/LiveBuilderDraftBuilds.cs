using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Постройки, собранные начерно (<see cref="LiveBuilderBuildQuality.Draft"/>), и их доводка до чистовых.
    /// - помнит подпись черновика — только в памяти: подпись в сцене пишет лишь чистовая сборка, после перезагрузки скриптов черновик доведётся сам
    /// - доводит все черновики сразу перед записью на диск сцены, префаба или файла меша, выходом в Play, перезагрузкой скриптов, запеканием и сборкой игры
    /// </summary>
    [InitializeOnLoad]
    public static class LiveBuilderDraftBuilds
    {
        // Постройка (объект из коробок, колонна, стена, здание, часть здания) → подпись, по которой собран её черновик.
        private static readonly Dictionary<Component, string> DraftSignatures = new();

        // Доводка идёт: файл, сохранённый изнутри неё, снова позвал бы её же.
        private static bool isFinishingEverything;


        static LiveBuilderDraftBuilds()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += FinishEverythingPending;
        }


        // Есть черновики, которые ещё не доведены.
        public static bool HasPending
        {
            get
            {
                RemoveDestroyed();
                return DraftSignatures.Count > 0;
            }
        }


        // ── Черновики ────────────────────────────────────────────────────

        public static void Remember(Component construction, string draftSignature)
        {
            DraftSignatures[construction] = draftSignature;
        }


        // Чистовая сборка — черновика больше нет.
        public static void Forget(Component construction)
        {
            DraftSignatures.Remove(construction);
        }


        // Собрана ли постройка по этой подписи — не хуже, чем просят: черновик годится только черновику, чистовая — любому.
        // Есть черновик — чистовой сборки нет, даже если подпись в сцене та же (вернули правку назад).
        public static bool IsBuilt(Component construction, string builtSignature, string signature, LiveBuilderBuildQuality quality)
        {
            if (DraftSignatures.TryGetValue(construction, out var draftSignature))
                return quality == LiveBuilderBuildQuality.Draft && draftSignature == signature;

            return builtSignature == signature;
        }


        // ── Доводка ──────────────────────────────────────────────────────

        // Каждый черновик — начисто, сейчас. Части здания доводятся вместе со зданием.
        public static void FinishAll()
        {
            RemoveDestroyed();

            if (DraftSignatures.Count == 0)
                return;

            var constructions = new List<Component>(DraftSignatures.Keys);
            var finishedBuildings = new HashSet<SplineBuilding>();

            foreach (var construction in constructions)
            {
                if (construction == null || !DraftSignatures.ContainsKey(construction))
                    continue;

                try
                {
                    Finish(construction, finishedBuildings);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, construction);
                }

                DraftSignatures.Remove(construction);
            }
        }

        private static void Finish(Component construction, HashSet<SplineBuilding> finishedBuildings)
        {
            switch (construction)
            {
                case BoxModel model:
                    BoxModelRebuilder.Rebuild(model);
                    break;

                case SplineWall wall:
                    SplineWallRebuilder.Rebuild(wall);
                    break;

                case LiveBuilderColumn column:
                    LiveBuilderColumnRebuilder.Rebuild(column);
                    break;

                case SplineBuilding building:
                    if (finishedBuildings.Add(building))
                        SplineBuildingRebuilder.Rebuild(building);
                    break;

                case SplineBuildingPart part:
                    var ownBuilding = part.GetComponentInParent<SplineBuilding>(true);

                    if (ownBuilding != null && finishedBuildings.Add(ownBuilding))
                        SplineBuildingRebuilder.Rebuild(ownBuilding);
                    break;
            }
        }


        // Сцена уходит из редактора — на диск, в Play, в запекание, в сборку: черновики и отложенный проход очереди — сейчас.
        public static void FinishEverythingPending()
        {
            if (isFinishingEverything)
                return;

            isFinishingEverything = true;

            try
            {
                LiveBuilderRebuildQueue.RunScheduledFinalPassNow();
                FinishAll();
            }
            finally
            {
                isFinishingEverything = false;
            }
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Удалённые постройки (Ctrl+Z, закрытая сцена) доводить незачем.
        private static void RemoveDestroyed()
        {
            List<Component> destroyed = null;

            foreach (var construction in DraftSignatures.Keys)
                if (construction == null)
                    (destroyed ??= new List<Component>()).Add(construction);

            if (destroyed == null)
                return;

            foreach (var construction in destroyed)
                DraftSignatures.Remove(construction);
        }


        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                FinishEverythingPending();
        }
    }


    /// <summary>Перед сборкой игры доводит черновики Live Builder (<see cref="LiveBuilderDraftBuilds"/>) и пишет файлы мешей на диск.</summary>
    public sealed class LiveBuilderDraftBuildsBeforeGameBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;


        // IPreprocessBuildWithReport
        public void OnPreprocessBuild(BuildReport report)
        {
            LiveBuilderDraftBuilds.FinishEverythingPending();
            BoxModelMeshStorage.SavePendingNow();
        }
    }


    /// <summary>
    /// Перед записью на диск сцены, префаба или файла меша Live Builder доводит черновики (<see cref="LiveBuilderDraftBuilds"/>) — кто бы ни сохранял:
    /// Save Project, чужой SaveAssets, сохранение сцены. Черновик на диск не уходит.
    /// </summary>
    public sealed class LiveBuilderDraftBuildsBeforeAssetSave : AssetModificationProcessor
    {
        // AssetModificationProcessor. Проход очереди сохраняет пересобранный префаб сам — он уже чистовой, а доводка посреди прохода сбила бы его.
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (!LiveBuilderRebuildQueue.IsPassRunning && System.Array.Exists(paths, IsTouchedByDrafts))
                LiveBuilderDraftBuilds.FinishEverythingPending();

            return paths;
        }

        private static bool IsTouchedByDrafts(string path)
        {
            return path.EndsWith(".unity") || path.EndsWith(".prefab") || BoxModelMeshStorage.IsBoxModelMeshPath(path);
        }
    }
}
