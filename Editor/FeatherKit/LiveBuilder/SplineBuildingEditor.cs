using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор здания по контуру: этажи → стены → перекрытия, свёрнуто — материалы и тонкая подстройка.
    /// Сверху строка-итог: высота здания и комнаты, чтобы не считать в уме.
    /// </summary>
    [CustomEditor(typeof(SplineBuilding))]
    [CanEditMultipleObjects]
    public class SplineBuildingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var building = (SplineBuilding)target;

            GUILayout.Label("Высота " + BoxModelSceneText.FormatMeters(building.TotalHeight) + " м, комната до потолка " +
                            BoxModelSceneText.FormatMeters(building.ClearHeight) + " м", EditorStyles.miniLabel);
            DrawRotationWarning(building);

            LiveBuilderInspectorLayout.Section("Этажи");
            LiveBuilderInspectorLayout.Properties(serializedObject, "storeyCount", "storeyHeight");

            LiveBuilderInspectorLayout.Section("Стены");
            LiveBuilderInspectorLayout.Properties(serializedObject, "wallType");

            if (building.WallType != null)
                LiveBuilderInspectorLayout.DrivenValue("Wall Thickness", BoxModelSceneText.FormatMeters(building.WallThickness) + " м", "Толщина и материал снаружи — из типа стены", "от типа");
            else
                LiveBuilderInspectorLayout.Properties(serializedObject, "wallThickness");

            LiveBuilderInspectorLayout.Properties(serializedObject, "contourSide");

            LiveBuilderInspectorLayout.Section("Перекрытия");
            LiveBuilderInspectorLayout.Properties(serializedObject, "slabThickness", "hasFloor", "hasRoof");

            if (LiveBuilderInspectorLayout.Foldout("SplineBuilding.Materials", "Материалы"))
            {
                if (building.WallType == null)
                    LiveBuilderInspectorLayout.Properties(serializedObject, "outerWallMaterial");

                LiveBuilderInspectorLayout.Properties(serializedObject, "innerWallMaterial", "floorMaterial", "ceilingMaterial", "roofMaterial");
            }

            if (LiveBuilderInspectorLayout.Foldout("SplineBuilding.Fine", "Тонко"))
                LiveBuilderInspectorLayout.Properties(serializedObject, "curveStep", "cornerSegments", "hasCollider");

            serializedObject.ApplyModifiedProperties();
        }

        // Лестницы и коробки интерьера идут по осям объекта из коробок: здание, повёрнутое внутри него, с ними разъедется.
        private static void DrawRotationWarning(SplineBuilding building)
        {
            var model = building.transform.parent != null ? building.transform.parent.GetComponentInParent<BoxModel>() : null;

            if (model == null || Quaternion.Angle(model.transform.rotation, building.transform.rotation) < 0.01f)
                return;

            EditorGUILayout.HelpBox("Здание повёрнуто внутри своего объекта из коробок: лестницы и коробки интерьера идут по осям объекта и разъедутся со зданием. " +
                                    "Поворачивай весь объект, а здание внутри оставь без поворота.", MessageType.Warning);
        }
    }
}
