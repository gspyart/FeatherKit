using System.Collections.Generic;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Повтор» вкладки «Выделено» — одна на все способы повторить: что сейчас повторяет выделенное (этажи здания, группа повтора,
    /// копии вверх, ряд Array) и что можно сделать здесь же: коробки и лестницы — завернуть в повтор с готовым шагом (вверх на этаж, вдоль X, вдоль Z),
    /// проём — ряд вдоль стены. Сами числа — там, где живут: у группы повтора — её карточка, по этажам здания — «Этажи в здании», ряд — модификатор Array.
    /// Механизмов четыре, а место, где их видно, одно.
    /// </summary>
    public sealed class LiveBuilderRepeatCard : LiveBuilderSelectionCard
    {
        // Ряд проёмов по умолчанию: столько штук, через столько метров между ними.
        private const int RowCount = 3;
        private const float RowGap = 1f;


        public override int Order => 20;


        public override bool Draw(GameObject selected)
        {
            if (selected == null || !IsRepeatable(selected))
                return false;

            using (LiveBuilderWindowControls.Card("Повтор"))
            {
                var status = StatusOf(selected);

                if (status.Count == 0)
                    LiveBuilderWindowControls.Note("Не повторяется.");

                foreach (var line in status)
                    LiveBuilderWindowControls.Note(line);

                DrawWrapActions(selected);
                DrawCutterRowAction(selected);

                LiveBuilderWindowControls.Explanation("Повторить можно четырьмя способами, а видно их все здесь: в здании — по его этажам («Этажи в здании»); " +
                                                      "коробки и лестницы — группой повтора; стены и проёмы в здании — копии вверх, их задаёт «На этажах» у привязки к зданию; проёмы вдоль стены — рядом Array. " +
                                                      "Копии считаются из оригинала: правишь один — меняются все.");
            }

            return true;
        }

        // Коробка, комната, лестница, объект из коробок, стена и проём — то, что умеет повторяться. Здание — само этажи, его части — нет.
        private static bool IsRepeatable(GameObject selected)
        {
            return BoxModelShapeCollector.IsPiece(selected) || selected.GetComponent<BoxModel>() != null || selected.GetComponent<SplineWall>() != null || selected.GetComponent<GeometryCutter>() != null;
        }


        // ── Что повторяет сейчас ─────────────────────────────────────────

        // Все способы, которые сейчас дают выделенному копии, — по строке на каждый.
        private static List<string> StatusOf(GameObject selected)
        {
            var lines = new List<string>();
            var isLinked = selected.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid;

            if (isLinked)
                lines.Add("По этажам здания «" + link.Building.name + "» — меняется в «Этажи в здании» ниже.");
            else if (BoxModelShapeCollector.IsPieceInsideStoreyCopies(selected.transform))
                lines.Add("По этажам здания — вместе с объектом или группой, в которой стоит.");

            for (var parent = selected.transform.parent; parent != null; parent = parent.parent)
                if (parent.TryGetComponent<BoxModelRepeat>(out var repeat))
                    lines.Add("В повторе «" + repeat.name + "»: всего " + repeat.Count + ", шаг " + StepText(repeat.Step) + ".");

            if (!isLinked && selected.TryGetComponent<IStoreyCopies>(out var storeyCopies) && storeyCopies.CopiesUp > 1)
                lines.Add("Копии вверх: всего " + storeyCopies.CopiesUp + ", через " + LiveBuilderSceneText.FormatMeters(storeyCopies.CopyStep) + " м.");

            if (selected.TryGetComponent<ArrayModifier>(out var array))
                lines.Add("Рядом Array: всего " + array.Counts.x * array.Counts.y * array.Counts.z + " — числа в его компоненте.");

            return lines;
        }

        private static string StepText(Vector3 step)
        {
            return LiveBuilderSceneText.FormatMeters(step.x) + " × " + LiveBuilderSceneText.FormatMeters(step.y) + " × " + LiveBuilderSceneText.FormatMeters(step.z) + " м";
        }


        // ── Что можно сделать ────────────────────────────────────────────

        // Коробки и лестницы — завернуть в повтор: вверх на этаж (вне здания — там этажи повторяет привязка), вдоль X, вдоль Z.
        private static void DrawWrapActions(GameObject selected)
        {
            if (LiveBuilderRepeatActions.SelectedPieces().Count == 0)
                return;

            if (!LiveBuilderRepeatActions.CanWrapSelection(out var reason))
            {
                LiveBuilderWindowControls.Note("Завернуть в повтор нельзя: " + reason);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var bounds = LiveBuilderRepeatActions.SelectedBounds();

                if (!IsInBuilding(selected) && GUILayout.Button(new GUIContent("Повтор вверх ↑", "Группа повтора, шаг — высота этажа")))
                    LiveBuilderRepeatActions.WrapSelection(new Vector3(0f, LiveBuilderStoreys.StoreyHeight, 0f));

                if (GUILayout.Button(new GUIContent("По X", "Шаг — ширина выделенного по X, по сетке")))
                    LiveBuilderRepeatActions.WrapSelection(new Vector3(StepAround(bounds.size.x), 0f, 0f));

                if (GUILayout.Button(new GUIContent("По Z", "Шаг — ширина выделенного по Z, по сетке")))
                    LiveBuilderRepeatActions.WrapSelection(new Vector3(0f, 0f, StepAround(bounds.size.z)));
            }
        }

        // Проём без ряда — «Ряд вдоль стены»: модификатор Array, копии встают по ширине проёма через зазор.
        private static void DrawCutterRowAction(GameObject selected)
        {
            if (!selected.TryGetComponent<GeometryCutter>(out var cutter) || selected.GetComponent<ArrayModifier>() != null)
                return;

            if (!GUILayout.Button(new GUIContent("Ряд вдоль стены ×" + RowCount,
                    "Ещё копии этого проёма вдоль стены через " + LiveBuilderSceneText.FormatMeters(RowGap) + " м — модификатор Array; сколько и зазор — в его компоненте")))
                return;

            var array = Undo.AddComponent<ArrayModifier>(cutter.gameObject);
            array.Counts = new Vector3Int(RowCount, 1, 1);
            array.StepsFromSize = true;
            array.Gap = new Vector3(RowGap, 0f, 0f);
            ArrayModifierBuilder.Rebuild(array);
        }

        // Выделенное — в здании по контуру: этажи там повторяет привязка к зданию.
        private static bool IsInBuilding(GameObject selected)
        {
            return selected.GetComponentInParent<SplineBuilding>() != null;
        }

        // Шаг повтора по размеру выделенного — с запасом в клетку сетки, чтобы копии не слиплись.
        private static float StepAround(float size)
        {
            var grid = LiveBuilderGrid.CurrentStep;
            return Mathf.Max(grid, Mathf.Ceil(size / grid) * grid + grid);
        }
    }
}
