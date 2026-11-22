using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор стены по линии: размер → пол и потолок (у замкнутой) → вид → тонко; главное — и секция Construction панели.
    /// Стена в этаже здания — высота до потолка, на весь этаж или своя («Height In Building») и «не строить стороны вдоль стен здания»; поля от здания только показаны.
    /// </summary>
    [CustomEditor(typeof(SplineWall))]
    [CanEditMultipleObjects]
    public class SplineWallEditor : Editor, ILiveBuilderCompactInspector
    {
        // Полный инспектор: главное → вид → тонко.
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var wall = (SplineWall)target;
            DrawConstruction(wall);
            LiveBuilderInspectorLayout.Section("Look");

            if (wall.WallType == null)
                LiveBuilderInspectorLayout.Properties(serializedObject, "material");
            else
                LiveBuilderInspectorLayout.DrivenValue("Material", wall.Material != null ? wall.Material.name : "kit gray", "Материал — из типа стены", "from type");

            if (LiveBuilderInspectorLayout.Foldout("SplineWall.Fine", "Fine Tuning"))
                LiveBuilderInspectorLayout.Properties(serializedObject, "curveStep", "cornerSegments", "hasCollider");

            serializedObject.ApplyModifiedProperties();
        }


        // ILiveBuilderCompactInspector: секция Construction панели.
        public void DrawMainFields()
        {
            serializedObject.Update();
            DrawConstruction((SplineWall)target);
            serializedObject.ApplyModifiedProperties();
        }


        // Тип, толщина, линия, высота → пол и потолок у замкнутой.
        private void DrawConstruction(SplineWall wall)
        {
            var isInBuilding = SplineBuildingStoreys.BuildingOfStoreyContent(wall.transform) != null;

            LiveBuilderInspectorLayout.Section("Size");
            LiveBuilderInspectorLayout.Properties(serializedObject, "wallType");

            if (wall.WallType != null)
                LiveBuilderInspectorLayout.DrivenValue("Thickness", LiveBuilderSceneText.FormatMeters(wall.Thickness) + " m", "Толщина — из типа стены", "from type");
            else
                LiveBuilderInspectorLayout.Properties(serializedObject, "thickness");

            LiveBuilderInspectorLayout.Properties(serializedObject, "alignment");

            // Комнате в здании — какие стороны строить; без здания нечему лежать вдоль.
            if (isInBuilding)
            {
                LiveBuilderInspectorLayout.Properties(serializedObject, "skipsSidesAlongBuildingWalls", "heightInBuilding");

                if (wall.HeightInBuilding == SplineWallHeightInBuilding.Custom)
                    LiveBuilderInspectorLayout.Properties(serializedObject, "height");
                else
                    LiveBuilderInspectorLayout.DrivenValue("Height", LiveBuilderSceneText.FormatMeters(wall.Height) + " m",
                        wall.HeightInBuilding == SplineWallHeightInBuilding.FullStorey ? "Весь этаж здания, сквозь перекрытие" : "До потолка этажа здания", "from building");
            }
            else
            {
                LiveBuilderInspectorLayout.Properties(serializedObject, "height");
            }

            // Пол и потолок — у замкнутой стены: у открытой линии нечего закрывать.
            if (IsClosed(wall))
            {
                LiveBuilderInspectorLayout.Section("Floor And Ceiling");
                LiveBuilderInspectorLayout.Properties(serializedObject, "hasFloor", "hasCeiling");

                if (wall.HasSlabs)
                    LiveBuilderInspectorLayout.Properties(serializedObject, "slabThickness", "floorMaterial", "ceilingMaterial");
            }
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
