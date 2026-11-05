using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор стены по линии: размер → этажи → пол и потолок (у замкнутой) → вид → тонко. Стена в здании — копии от здания, высота — до потолка, на весь этаж или своя
    /// («Height In Building»); поля от здания только показаны, а этажи меняются в привязке («Spline Building Storey Link» ниже);
    /// у неё же — «не строить стороны вдоль стен здания».
    /// </summary>
    [CustomEditor(typeof(SplineWall))]
    [CanEditMultipleObjects]
    public class SplineWallEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var wall = (SplineWall)target;
            var isLinked = wall.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid;

            LiveBuilderInspectorLayout.Section("Размер");
            LiveBuilderInspectorLayout.Properties(serializedObject, "wallType");

            if (wall.WallType != null)
                LiveBuilderInspectorLayout.DrivenValue("Thickness", LiveBuilderSceneText.FormatMeters(wall.Thickness) + " м", "Толщина — из типа стены", "от типа");
            else
                LiveBuilderInspectorLayout.Properties(serializedObject, "thickness");

            LiveBuilderInspectorLayout.Properties(serializedObject, "alignment");

            // Комнате в здании — какие стороны строить; без здания нечему лежать вдоль.
            if (isLinked)
            {
                LiveBuilderInspectorLayout.Properties(serializedObject, "skipsSidesAlongBuildingWalls", "heightInBuilding");

                if (wall.HeightInBuilding == SplineWallHeightInBuilding.Custom)
                    LiveBuilderInspectorLayout.Properties(serializedObject, "height");
                else
                    LiveBuilderInspectorLayout.DrivenValue("Height", LiveBuilderSceneText.FormatMeters(wall.Height) + " м",
                        wall.HeightInBuilding == SplineWallHeightInBuilding.FullStorey ? "Весь этаж здания, сквозь перекрытие" : "До потолка этажа здания");
            }
            else
            {
                LiveBuilderInspectorLayout.Properties(serializedObject, "height");
            }

            // Копии вверх — дело здания. У стены без здания поле видно, только если копии уже есть: чтобы их можно было убрать.
            if (isLinked || wall.CopiesUp > 1)
                LiveBuilderInspectorLayout.StoreyCopies(serializedObject, wall, isLinked);

            // Пол и потолок — у замкнутой стены: у открытой линии нечего закрывать.
            if (IsClosed(wall))
            {
                LiveBuilderInspectorLayout.Section("Пол и потолок");
                LiveBuilderInspectorLayout.Properties(serializedObject, "hasFloor", "hasCeiling");

                if (wall.HasSlabs)
                    LiveBuilderInspectorLayout.Properties(serializedObject, "slabThickness", "floorMaterial", "ceilingMaterial");
            }

            LiveBuilderInspectorLayout.Section("Вид");

            if (wall.WallType == null)
                LiveBuilderInspectorLayout.Properties(serializedObject, "material");
            else
                LiveBuilderInspectorLayout.DrivenValue("Material", wall.Material != null ? wall.Material.name : "серый набора", "Материал — из типа стены", "от типа");

            if (LiveBuilderInspectorLayout.Foldout("SplineWall.Fine", "Тонко"))
                LiveBuilderInspectorLayout.Properties(serializedObject, "curveStep", "cornerSegments", "hasCollider");

            serializedObject.ApplyModifiedProperties();
        }

        // Хоть одна линия стены замкнута.
        private static bool IsClosed(SplineWall wall)
        {
            if (!wall.TryGetComponent<UnityEngine.Splines.SplineContainer>(out var container))
                return false;

            foreach (var spline in container.Splines)
                if (spline.Closed)
                    return true;

            return false;
        }
    }
}
