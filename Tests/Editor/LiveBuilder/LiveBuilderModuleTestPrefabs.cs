using FeatherKit.EditorTools.LiveBuilder;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.Tests.LiveBuilder
{
    /// <summary>
    /// Префабы-модули для проверок повтора и инструмента «Module» — во временной папке ассетов, папку удаляет сама проверка.
    /// Окно: рама из реек и вырез 1,2 × 1,4. Балконная дверь: вырез 1 × 2,2, плита балкона перед стеной и ограждение.
    /// Коллайдер — только у плиты балкона: лучи сквозь проёмы раму не задевают. Вырезы — «Everything it touches», только стены.
    /// </summary>
    public static class LiveBuilderModuleTestPrefabs
    {
        public const float WindowBottomAboveFloor = 0.9f;


        // ── Префабы ──────────────────────────────────────────────────────

        public static GameObject CreateWindow(string folder)
        {
            var root = new GameObject("Test Window");
            root.AddComponent<LiveBuilderModule>().BottomAboveFloor = WindowBottomAboveFloor;
            AddCutter(root.transform, new Vector2(1.2f, 1.4f));
            AddBar(root.transform, new Vector3(-0.65f, 0.7f, 0f), new Vector3(0.1f, 1.5f, 0.15f), false);
            AddBar(root.transform, new Vector3(0.65f, 0.7f, 0f), new Vector3(0.1f, 1.5f, 0.15f), false);
            AddBar(root.transform, new Vector3(0f, 1.45f, 0f), new Vector3(1.4f, 0.1f, 0.15f), false);
            AddBar(root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(1.4f, 0.1f, 0.15f), false);
            return Save(root, folder);
        }


        public static GameObject CreateBalconyDoor(string folder)
        {
            var root = new GameObject("Test Balcony Door");
            root.AddComponent<LiveBuilderModule>();
            AddCutter(root.transform, new Vector2(1f, 2.2f));
            AddBar(root.transform, new Vector3(0f, -0.075f, 0.9f), new Vector3(2f, 0.15f, 1.2f), true).name = "Slab";
            AddBar(root.transform, new Vector3(0f, 0.45f, 1.475f), new Vector3(2f, 0.9f, 0.05f), false).name = "Railing";
            return Save(root, folder);
        }


        // ── Части ────────────────────────────────────────────────────────

        // Вырез с опорой внизу посередине, насквозь через стену с запасом.
        private static void AddCutter(Transform root, Vector2 size)
        {
            var cutterObject = new GameObject("Opening");
            cutterObject.transform.SetParent(root, false);

            var cutter = cutterObject.AddComponent<GeometryCutter>();
            cutter.Shape = GeometryCutterShape.Box;
            cutter.Width = size.x;
            cutter.Height = size.y;
            cutter.Depth = 1.2f;
            cutter.ShapeOffset = Vector3.zero;
            cutter.Scope = GeometryCutterScope.EverythingItTouches;
            cutter.CutTargets = LiveBuilderCutTargets.WallsOnly;
        }

        private static GameObject AddBar(Transform root, Vector3 center, Vector3 size, bool hasCollider)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.SetParent(root, false);
            bar.transform.localPosition = center;
            bar.transform.localScale = size;

            if (!hasCollider)
                Object.DestroyImmediate(bar.GetComponent<Collider>());

            return bar;
        }

        private static GameObject Save(GameObject root, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + root.name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }


        // ── Проверки ─────────────────────────────────────────────────────

        // Середина проёма выреза в мире.
        public static Vector3 OpeningCenterOf(GameObject module)
        {
            var cutter = module.GetComponentInChildren<GeometryCutter>(true);
            return GeometryCutterOutline.ShapeToWorld(cutter).MultiplyPoint3x4(new Vector3(0f, cutter.Height / 2f, 0f));
        }


        // Луч поперёк модуля сквозь середину его проёма (или на offset от неё) ничего не задел.
        public static bool IsOpenThrough(GameObject module, Vector3 offset = default)
        {
            var outward = module.transform.forward;
            return LiveBuilderTestScene.IsOpen(OpeningCenterOf(module) + offset + outward * 1.5f, -outward, 3f);
        }
    }
}
