using System.Collections.Generic;
using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Временная сцена для проверок Live Builder: открывается рядом с открытыми и становится активной — всё новое ложится в неё.
    /// Закрылась — сцена выгружена, файлы мешей её построек удалены. Постройки ставятся далеко от начала координат (<see cref="Origin"/>),
    /// чтобы лучи проверок не задевали открытый уровень. Числа построек — свои, не из настроек окна Live Builder: проверка от них не зависит.
    /// </summary>
    public sealed class LiveBuilderTestScene : System.IDisposable
    {
        public static readonly Vector3 Origin = new(5000f, 0f, 5000f);

        private readonly Scene previousActiveScene;
        private readonly Scene scene;


        public LiveBuilderTestScene()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }


        // ── Постройки ────────────────────────────────────────────────────

        // Стена по линии через точки (от Origin), толщина 0,3, высота 3, линия посередине. Уже собрана.
        public SplineWall CreateWall(params Vector3[] points)
        {
            var wall = BoxModelFactory.CreateWall(null, Origin, Quaternion.identity);
            wall.Thickness = 0.3f;
            wall.Height = 3f;
            wall.Alignment = SplineWallAlignment.Center;
            AddKnots(wall.GetComponent<SplineContainer>().Spline, points);
            SplineWallRebuilder.Rebuild(wall);
            return wall;
        }


        // Здание по замкнутому контуру (от Origin): этажи по 4 м, стены 0,3 внутрь от контура. Уже собрано.
        public SplineBuilding CreateBuilding(int storeyCount, params Vector3[] contour)
        {
            var building = BoxModelFactory.CreateBuilding(null, Origin, Quaternion.identity);
            building.StoreyCount = storeyCount;
            building.StoreyHeight = 4f;
            building.WallThickness = 0.3f;
            building.WallType = null;
            building.ContourSide = SplineBuildingContourSide.Outside;

            var spline = building.GetComponent<SplineContainer>().Spline;
            spline.Clear();
            AddKnots(spline, contour);
            spline.Closed = true;
            SplineBuildingRebuilder.Rebuild(building);
            return building;
        }

        private static void AddKnots(Spline spline, Vector3[] points)
        {
            foreach (var point in points)
                spline.Add(new BezierKnot((float3)point), TangentMode.Linear);
        }


        // ── Проверки ─────────────────────────────────────────────────────

        // Луч от точки на расстояние ничего не задел — там дыра.
        public static bool IsOpen(Vector3 from, Vector3 direction, float distance)
        {
            Physics.SyncTransforms();
            return !Physics.Raycast(from, direction.normalized, distance);
        }


        // Двигает точку сплайна, оставляя её усы.
        public static void MoveKnot(Spline spline, int index, Vector3 position)
        {
            var knot = spline[index];
            knot.Position = position;
            spline.SetKnot(index, knot);
        }


        // ── Конец ────────────────────────────────────────────────────────

        // Сцену — закрыть, файлы мешей её построек — удалить.
        public void Dispose()
        {
            var meshFiles = new HashSet<string>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                    AddMeshFile(meshFiles, filter.sharedMesh);

                foreach (var meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
                    AddMeshFile(meshFiles, meshCollider.sharedMesh);
            }

            if (previousActiveScene.IsValid())
                SceneManager.SetActiveScene(previousActiveScene);

            EditorSceneManager.CloseScene(scene, true);

            foreach (var path in meshFiles)
                AssetDatabase.DeleteAsset(path);

            // Сцена не сохранена — меши лежали в общей папке; опустела — убрать и её.
            var folder = BoxModelMeshStorage.FallbackFolder;

            if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.FindAssets("", new[] { folder }).Length == 0)
                AssetDatabase.DeleteAsset(folder);
        }

        private static void AddMeshFile(HashSet<string> meshFiles, Mesh mesh)
        {
            if (BoxModelMeshStorage.IsBoxModelMeshFile(mesh))
                meshFiles.Add(AssetDatabase.GetAssetPath(mesh));
        }
    }
}
