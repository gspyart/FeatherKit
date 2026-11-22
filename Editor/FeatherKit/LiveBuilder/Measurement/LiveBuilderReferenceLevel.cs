using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Эталонный уровень Live Builder из ТЗ — кодом, одинаковый при каждой сборке: по нему меряют пересборку и сверяют меши до и после правок модуля.
    /// - секция на 9 этажей с окнами, лестницей, шахтой лифта, квартирами и балконами — <see cref="LiveBuilderReferenceSection"/>
    /// - двор — объект на 300 коробок с фаской, пандус, колонны, арка и навес с двускатной крышей — <see cref="LiveBuilderReferenceYard"/>
    /// - меню — уровень в новой сцене, для проверки руками
    /// </summary>
    public static class LiveBuilderReferenceLevel
    {
        public const string RootName = "Reference Level";
        public const float StoreyHeight = 3f;

        // Текстура — по осям постройки, как до этапа 5: отпечаток мешей сверяет и развёртку.
        public const LiveBuilderTextureAlignment TextureAlignment = LiveBuilderTextureAlignment.Object;

        // Двор — позади секции, чтобы их габариты не пересекались.
        private static readonly Vector3 YardOffset = new(0f, 0f, 20f);


        // Строит уровень в активной сцене основного этапа и собирает его одним проходом очереди. origin — пол первого этажа уровня и угол секции.
        public static LiveBuilderReferenceLevelTargets Build(Vector3 origin)
        {
            var root = new GameObject(RootName).transform;
            root.position = origin;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Reference Level");

            StoreLevelStoreys(origin.y);
            var building = LiveBuilderReferenceSection.Build(root, out var window);
            var yard = LiveBuilderReferenceYard.Build(root, root.TransformPoint(YardOffset), out var column);
            KeepTextureAlignment(root);

            LiveBuilderRebuildQueue.RunFinalPassNow();
            return new LiveBuilderReferenceLevelTargets(root, building, window, yard, column);
        }

        private static void KeepTextureAlignment(Transform root)
        {
            foreach (var model in root.GetComponentsInChildren<BoxModel>(true))
                model.TextureAlignment = TextureAlignment;

            foreach (var wall in root.GetComponentsInChildren<SplineWall>(true))
                wall.TextureAlignment = TextureAlignment;

            foreach (var building in root.GetComponentsInChildren<SplineBuilding>(true))
                building.TextureAlignment = TextureAlignment;

            foreach (var column in root.GetComponentsInChildren<LiveBuilderColumn>(true))
                column.TextureAlignment = TextureAlignment;
        }

        // Этажи уровня в сцене — те же 3 м, что у здания: здание стоит на этаже уровня, как его ставит инструмент.
        private static void StoreLevelStoreys(float groundLevel)
        {
            var storeys = LiveBuilderLevelStoreysInScene.FindOrCreate(StoreyHeight, groundLevel, "Build Reference Level");
            storeys.StoreyHeight = StoreyHeight;
            storeys.GroundLevel = groundLevel;
        }


        // ── Меню ─────────────────────────────────────────────────────────

        [MenuItem("Tools/FeatherKit/Live Builder/Open Reference Level")]
        private static void OpenFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            StageUtility.GoToMainStage();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var targets = Build(Vector3.zero);
            Selection.activeGameObject = targets.Building.gameObject;

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }
    }


    /// <summary>Что двигают замеры на эталонном уровне: колонну двора, ряд окон секции (окно с повтором), само здание.</summary>
    public sealed class LiveBuilderReferenceLevelTargets
    {
        public LiveBuilderReferenceLevelTargets(Transform root, SplineBuilding building, GeometryCutter window, BoxModel yard, BoxModelPart column)
        {
            Root = root;
            Building = building;
            Window = window;
            Yard = yard;
            Column = column;
        }


        public Transform Root { get; }
        public SplineBuilding Building { get; }
        public GeometryCutter Window { get; }
        public BoxModel Yard { get; }
        public BoxModelPart Column { get; }
    }
}
