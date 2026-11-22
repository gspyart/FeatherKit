using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор здания по контуру: этажи → стены → перекрытия → двускатная крыша, свёрнуто — материалы и тонкая подстройка.
    /// Сверху строка-итог: высота здания и комнаты, чтобы не считать в уме. У здания с этажами-контейнерами — ещё этаж уровня и чья высота этажа.
    /// </summary>
    [CustomEditor(typeof(SplineBuilding))]
    [CanEditMultipleObjects]
    public class SplineBuildingEditor : Editor, ILiveBuilderCompactInspector
    {
        // Полный инспектор: итог высоты и предупреждения → главное → материалы и тонко свёрнуты.
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var building = (SplineBuilding)target;
            DrawRotationWarning(building);

            if (SplineBuildingMeshBuilder.IsContourBroken(building))
                EditorGUILayout.HelpBox("The contour crosses itself — part of the floor and roof is not built. Move the points so the sides do not cross.",
                    MessageType.Warning);

            DrawConstruction(building);

            if (LiveBuilderInspectorLayout.Foldout("SplineBuilding.Materials", "Materials"))
            {
                if (building.WallType == null)
                    LiveBuilderInspectorLayout.Properties(serializedObject, "outerWallMaterial");

                LiveBuilderInspectorLayout.Properties(serializedObject, "innerWallMaterial", "floorMaterial", "ceilingMaterial", "roofMaterial");
            }

            if (LiveBuilderInspectorLayout.Foldout("SplineBuilding.Fine", "Fine Tuning"))
                LiveBuilderInspectorLayout.Properties(serializedObject, "curveStep", "cornerSegments", "hasCollider");

            serializedObject.ApplyModifiedProperties();
        }


        // ILiveBuilderCompactInspector: секция Construction панели; предупреждения панель пишет в заголовке карточки.
        public void DrawMainFields()
        {
            serializedObject.Update();
            DrawConstruction((SplineBuilding)target);
            serializedObject.ApplyModifiedProperties();
        }


        // Высота → этажи → стены → перекрытия → двускатная крыша.
        private void DrawConstruction(SplineBuilding building)
        {
            GUILayout.Label("Height " + LiveBuilderSceneText.FormatMeters(building.TotalHeight) + " m, floor to ceiling " +
                            LiveBuilderSceneText.FormatMeters(building.ClearHeight) + " m", EditorStyles.miniLabel);

            LiveBuilderInspectorLayout.Section("Storeys");
            LiveBuilderInspectorLayout.Properties(serializedObject, "storeyCount");
            DrawStoreyHeight(building);

            LiveBuilderInspectorLayout.Section("Walls");
            LiveBuilderInspectorLayout.Properties(serializedObject, "wallType");

            if (building.WallType != null)
                LiveBuilderInspectorLayout.DrivenValue("Wall Thickness", LiveBuilderSceneText.FormatMeters(building.WallThickness) + " m", "Толщина и материал снаружи — из типа стены", "from type");
            else
                LiveBuilderInspectorLayout.Properties(serializedObject, "wallThickness");

            LiveBuilderInspectorLayout.Properties(serializedObject, "contourSide");

            LiveBuilderInspectorLayout.Section("Slabs");
            LiveBuilderInspectorLayout.Properties(serializedObject, "slabThickness", "hasFloor", "hasRoof", "hasParapet");

            if (building.HasParapet)
                LiveBuilderInspectorLayout.Properties(serializedObject, "parapetHeight");

            DrawGableRoof(building);
        }

        // Здание с этажами уровня: этаж уровня и чья высота; высота уровня — только видна. Старое здание — своя высота, как раньше.
        private void DrawStoreyHeight(SplineBuilding building)
        {
            if (!SplineBuildingStoreys.HasStoreyContainers(building) || targets.Length > 1)
            {
                LiveBuilderInspectorLayout.Properties(serializedObject, "storeyHeight");
                return;
            }

            SplineBuildingLevelFields.Draw(building);

            if (building.LevelPlacement.UsesLevelStoreyHeight)
                LiveBuilderInspectorLayout.DrivenValue("Storey Height", LiveBuilderSceneText.FormatMeters(building.StoreyHeight) + " m",
                    "Высота этажа уровня — меняется в разделе Storeys окна Live Builder", "from level");
            else
                LiveBuilderInspectorLayout.Properties(serializedObject, "storeyHeight");
        }


        // Двускатная крыша: уклон и свес помнятся; кнопка ставит её или пересоздаёт по нынешнему контуру.
        private void DrawGableRoof(SplineBuilding building)
        {
            if (targets.Length > 1)
                return;

            LiveBuilderInspectorLayout.Section("Gable Roof");
            LiveBuilderGableRoofSettings.PitchDegrees = EditorGUILayout.FloatField(new GUIContent("Pitch, °", "Уклон ската от горизонта, градусы"),
                LiveBuilderGableRoofSettings.PitchDegrees);
            LiveBuilderGableRoofSettings.Overhang = EditorGUILayout.FloatField(new GUIContent("Overhang, m", "Насколько крыша выходит за наружные стены"),
                LiveBuilderGableRoofSettings.Overhang);

            var canBuild = SplineBuildingGableRoof.CanBuild(building, out var reason);
            var roofContainer = canBuild ? SplineBuildingStoreys.FindRoof(building) : null;
            var hasRoof = roofContainer != null && SplineBuildingGableRoof.FindRoofObject(roofContainer.transform) != null;

            using (new EditorGUI.DisabledScope(!canBuild))
                if (GUILayout.Button(new GUIContent(hasRoof ? "Rebuild Gable Roof" : "Gable Roof",
                        "Два ската в этаже Roof, конёк вдоль длинной стороны, парапет выключается. Поменял контур — нажми ещё раз")))
                {
                    serializedObject.ApplyModifiedProperties();
                    SplineBuildingGableRoof.Build(building, LiveBuilderGableRoofSettings.PitchDegrees, LiveBuilderGableRoofSettings.Overhang);
                    serializedObject.Update();
                }

            if (!canBuild)
                EditorGUILayout.HelpBox(reason, MessageType.None);
        }


        // Лестницы и коробки интерьера идут по осям объекта из коробок: здание, повёрнутое внутри него, с ними разъедется.
        private static void DrawRotationWarning(SplineBuilding building)
        {
            if (!LiveBuilderLevelCheck.IsRotatedInsideModel(building))
                return;

            EditorGUILayout.HelpBox("The building is rotated inside its box model: interior stairs and boxes follow the model axes and will drift from it. " +
                                    "Rotate the whole model and keep the building inside unrotated.", MessageType.Warning);
        }
    }
}
