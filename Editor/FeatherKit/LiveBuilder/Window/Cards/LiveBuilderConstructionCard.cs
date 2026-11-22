using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Construction — главные числа выделенного, первой под заголовком: нарисовал — поправил, не уводя взгляд от сцены.
    /// - коробка: размер по осям (разный у выделенных — «—»), Make Cut, угол в осях объекта под «⋯»
    /// - этаж здания: номер, пол, переход к зданию
    /// - остальные постройки: главные поля их инспектора (<see cref="ILiveBuilderCompactInspector"/>), у объекта из коробок — ещё To ProBuilder
    /// </summary>
    public sealed class LiveBuilderConstructionCard : LiveBuilderSelectionCard
    {
        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        private Editor componentEditor;


        public override int Order => ConstructionOrder;

        public override string Title => "Construction";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            var kind = LiveBuilderSelectionKinds.KindOf(selected[0]);
            return kind != LiveBuilderSelectionKind.Other && kind != LiveBuilderSelectionKind.BuildingPart;
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            switch (LiveBuilderSelectionKinds.KindOf(selected[0]))
            {
                case LiveBuilderSelectionKind.Box:
                case LiveBuilderSelectionKind.LegacyCutBox:
                    DrawBoxes(PartsOf(selected));
                    break;

                case LiveBuilderSelectionKind.Storey:
                    DrawStorey(selected[0].GetComponent<SplineBuildingStorey>());
                    break;

                default:
                    DrawInspectorMainFields(selected);
                    break;
            }
        }


        // IDisposable: отпустить готовый инспектор.
        public override void Dispose()
        {
            DestroyInspector(ref componentEditor);
        }


        // ── Коробки ──────────────────────────────────────────────────────

        // Размер каждой оси у всех коробок: одинаковый — число, разный — «—».
        public static LiveBuilderMixedValue[] SizeOf(IReadOnlyList<BoxModelPart> parts)
        {
            var size = new LiveBuilderMixedValue[3];

            for (var axis = 0; axis < 3; axis++)
            {
                var values = new List<float>();

                foreach (var part in parts)
                    values.Add(part.Size[axis]);

                size[axis] = LiveBuilderMixedValue.Of(values);
            }

            return size;
        }


        private static List<BoxModelPart> PartsOf(IReadOnlyList<GameObject> selected)
        {
            var parts = new List<BoxModelPart>();

            foreach (var each in selected)
                if (each.TryGetComponent<BoxModelPart>(out var part))
                    parts.Add(part);

            return parts;
        }


        private static void DrawBoxes(List<BoxModelPart> parts)
        {
            DrawBoxSize(parts);
            var isLegacyCut = parts[0].Mode == BoxModelPartMode.Cut;

            if (isLegacyCut)
                LiveBuilderWindowControls.Note("Legacy cut box: cuts by the old rule until the scene data is upgraded. A new cut is a Geometry Cutter.");

            // После отрисовки: превращение уничтожает эти коробки, а секции ниже ещё рисуют их.
            if (GUILayout.Button(new GUIContent(isLegacyCut ? "Make Adding Box" : "Make Cut",
                    "Выделенные коробки становятся вырезами той же формы, места и материалов (Geometry Cutter). " +
                    "Старая вырезающая коробка становится добавляющей. Один шаг Ctrl+Z")))
                EditorApplication.delayCall += BoxModelActions.ToggleAddCutOfSelected;

            if (parts.Count == 1 && LiveBuilderWindowControls.MoreSection("Card.BoxCorner", false, "corner"))
                DrawCorner(parts[0]);
        }

        // Размер всем выделенным: введённое число ставится по своей оси каждой коробке, остальные оси у каждой — свои.
        private static void DrawBoxSize(List<BoxModelPart> parts)
        {
            var size = SizeOf(parts);
            var isNarrow = !EditorGUIUtility.wideMode;

            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.PrefixLabel(new GUIContent("Size, m", "Размер коробки по осям её объекта: X — ширина, Y — высота, Z — глубина"));

                if (!isNarrow)
                    for (var axis = 0; axis < 3; axis++)
                        DrawSizeAxis(parts, axis, size[axis]);
            }

            if (!isNarrow)
                return;

            for (var axis = 0; axis < 3; axis++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    DrawSizeAxis(parts, axis, size[axis]);
                }
        }

        private static void DrawSizeAxis(List<BoxModelPart> parts, int axis, LiveBuilderMixedValue value)
        {
            var labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 13f;

            if (LiveBuilderMixedValue.Field(new GUIContent(AxisNames[axis]), value, out var newValue, GUILayout.MinWidth(40f)))
            {
                Undo.RecordObjects(parts.ToArray(), "Edit Box Size");

                foreach (var part in parts)
                {
                    var partSize = part.Size;
                    partSize[axis] = newValue;
                    part.Size = partSize;
                }
            }

            EditorGUIUtility.labelWidth = labelWidth;
        }


        // Угол с наименьшими координатами в осях объекта.
        private static void DrawCorner(BoxModelPart part)
        {
            var model = part.GetComponentInParent<BoxModel>();

            if (model == null)
                return;

            var min = BoxModelShapeCollector.LocalMinOf(model, part.transform);
            var newMin = EditorGUILayout.Vector3Field(new GUIContent("Corner, m", "Угол с наименьшими координатами, в осях объекта"), min);

            if (newMin == min)
                return;

            Undo.RecordObject(part.transform, "Move Box");
            part.transform.position = model.transform.TransformPoint(newMin);
        }


        // ── Этаж здания ──────────────────────────────────────────────────

        // Этаж строит здание: номер и пол — только видны, числа — у здания.
        private static void DrawStorey(SplineBuildingStorey storey)
        {
            var building = storey.Building;

            if (building == null)
                return;

            var name = storey.IsRoof ? "Roof" : SplineBuildingStoreys.NameOf(building, storey.Storey);
            LiveBuilderWindowControls.Computed("Storey", name, "Этаж здания — его строит здание: число этажей и высота — у здания");
            LiveBuilderWindowControls.Computed("Floor, m", LiveBuilderSceneText.FormatMeters(storey.transform.position.y), "Пол этажа в мире");

            if (GUILayout.Button(new GUIContent("Select Building", "Выделить здание этого этажа")))
                Selection.activeGameObject = building.gameObject;
        }


        // ── Инспектор постройки ──────────────────────────────────────────

        // Одного вида — один инспектор на всех: разные значения — «—», правка ставит всем.
        private void DrawInspectorMainFields(IReadOnlyList<GameObject> selected)
        {
            var components = OwnComponentsOf(selected);

            if (components.Length == 0)
                return;

            var editor = CachedInspectorOf(components, ref componentEditor);

            if (editor is ILiveBuilderCompactInspector compact)
                compact.DrawMainFields();
            else
                editor.OnInspectorGUI();

            if (components.Length == 1 && components[0] is BoxModel && BoxModelActions.CanFreezeToProBuilder
                && GUILayout.Button(new GUIContent("To ProBuilder", "Копия объекта обычным мешем для ручной правки; сам объект выключается")))
                BoxModelActions.FreezeSelectedModel();
        }
    }
}
