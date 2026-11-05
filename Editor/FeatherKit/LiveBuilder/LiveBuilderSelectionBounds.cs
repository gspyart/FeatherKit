using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Рамка выделенной постройки в окне сцены, как Dimensions Overlay в ProBuilder: габарит в осях объекта цветом вкладки «Выделено»,
    /// на трёх рёбрах — ширина, глубина и высота, над рамкой — что это. Что выделено и какого размера — <see cref="LiveBuilderSelectionInfo"/>.
    /// Рисует окно сцены Live Builder (<see cref="LiveBuilderSceneTool"/>), пока ничего не рисуешь.
    /// </summary>
    public static class LiveBuilderSelectionBounds
    {
        private const float LineThickness = 2f;


        // Рамка и подписи активного выделенного; не постройка Live Builder — ничего.
        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint || !LiveBuilderSelectionInfo.TryDescribe(Selection.activeGameObject, out var info))
                return;

            var min = info.LocalBounds.min;
            var max = info.LocalBounds.max;
            var color = LiveBuilderTabBar.ColorOf(LiveBuilderTabColor.Selection);
            var size = info.Size;

            // Рамка видна и сквозь стены: здание целиком всё равно что-то закрывает.
            var previousZTest = Handles.zTest;

            using (new Handles.DrawingScope(color, info.Frame))
            {
                Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
                DrawBoxEdges(min, max);
            }

            Handles.zTest = previousZTest;

            // Ширина — по нижнему ребру вдоль X, глубина — вдоль Z, высота — по вертикальному ребру в том же углу.
            var corner = min;
            DrawEdgeLabel(info.Frame, corner, new Vector3(max.x, min.y, min.z), size.x);
            DrawEdgeLabel(info.Frame, corner, new Vector3(min.x, min.y, max.z), size.z);
            DrawEdgeLabel(info.Frame, corner, new Vector3(min.x, max.y, min.z), size.y);

            var topCenter = info.Frame.MultiplyPoint3x4(new Vector3((min.x + max.x) / 2f, max.y, (min.z + max.z) / 2f));
            LiveBuilderSceneText.DrawLabel(topCenter, info.KindName + " · " + info.Target.name);
        }

        // Двенадцать рёбер коробки толстой линией: обычная тонкая теряется на фоне каркасов.
        private static void DrawBoxEdges(Vector3 min, Vector3 max)
        {
            var corners = new Vector3[8];

            for (var index = 0; index < 8; index++)
                corners[index] = new Vector3((index & 1) == 0 ? min.x : max.x, (index & 2) == 0 ? min.y : max.y, (index & 4) == 0 ? min.z : max.z);

            for (var index = 0; index < 8; index++)
                for (var bit = 1; bit < 8; bit <<= 1)
                    if ((index & bit) == 0)
                        Handles.DrawLine(corners[index], corners[index | bit], LineThickness);
        }

        // Подпись длины у середины ребра; ребро нулевой длины — без подписи.
        private static void DrawEdgeLabel(Matrix4x4 frame, Vector3 from, Vector3 to, float length)
        {
            if (length < 0.001f)
                return;

            LiveBuilderSceneText.DrawLabel(frame.MultiplyPoint3x4((from + to) / 2f), LiveBuilderSceneText.FormatMeters(length) + " м");
        }
    }
}
