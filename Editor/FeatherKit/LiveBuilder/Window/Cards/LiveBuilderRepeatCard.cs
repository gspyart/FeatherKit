using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Повтор» вкладки «Выделено» — одно место для всех способов повторить, и числа правятся прямо здесь: «Сколько всего» и шаг
    /// у группы повтора (её саму или ту, в которой стоит выделенное), копии вверх у стены и проёма вне здания, ряд Array у проёма.
    /// По этажам здания — строкой: там повтор задаёт «На этажах» у привязки. Не повторяется — один блок «Повторить»: сколько всего
    /// и куда — вверх на этаж, по X, по Z; какой механизм встанет (группа повтора, копии вверх, ряд Array), решает карточка сама.
    /// </summary>
    public sealed class LiveBuilderRepeatCard : LiveBuilderSelectionCard
    {
        // Зазор между проёмами в новом ряду, м.
        private const float RowGap = 1f;

        private const int MaxNewCount = 50;

        // «Сколько всего» у нового повтора — помнится до перезагрузки скриптов.
        private static int newRepeatCount = 3;


        public override int Order => 20;


        public override bool Draw(GameObject selected)
        {
            if (selected == null)
                return false;

            var ownRepeat = selected.GetComponent<BoxModelRepeat>();

            if (ownRepeat == null && !IsRepeatable(selected))
                return false;

            using (LiveBuilderWindowControls.Card("Повтор"))
            {
                var isRepeated = ownRepeat != null
                    ? DrawRepeatGroup(ownRepeat, "Группа повтора: всё внутри — ещё раз на каждый шаг.")
                    : DrawCurrentRepeats(selected);

                if (!isRepeated)
                    DrawNewRepeat(selected);

                LiveBuilderWindowControls.Explanation("Копии считаются из оригинала: правишь один — меняются все. В здании этажи повторяет «На этажах» у привязки, " +
                                                      "а не эта карточка. Исключение для одной копии — коробка ниже группы в списке объектов: она главнее.");
            }

            return true;
        }

        // Коробка, комната, лестница, объект из коробок, стена и проём — то, что умеет повторяться. Здание — само этажи, его части — нет.
        private static bool IsRepeatable(GameObject selected)
        {
            return BoxModelShapeCollector.IsPiece(selected) || selected.GetComponent<BoxModel>() != null
                   || selected.GetComponent<SplineWall>() != null || selected.GetComponent<GeometryCutter>() != null;
        }


        // ── Что повторяет сейчас ─────────────────────────────────────────

        // Все способы, которые сейчас дают выделенному копии, — каждый своими числами. Повторяется хоть чем-то — true.
        private static bool DrawCurrentRepeats(GameObject selected)
        {
            var isRepeated = DrawStoreyLinkLine(selected);
            var parentRepeat = NearestParentRepeat(selected.transform);

            if (parentRepeat != null)
                isRepeated |= DrawRepeatGroup(parentRepeat, "В группе повтора «" + parentRepeat.name + "»:");

            isRepeated |= DrawCopiesUp(selected);
            isRepeated |= DrawArrayRow(selected);
            return isRepeated;
        }

        // По этажам здания — только строкой: числа у привязки, в карточке «Этажи в здании».
        private static bool DrawStoreyLinkLine(GameObject selected)
        {
            if (selected.TryGetComponent<SplineBuildingStoreyLink>(out var link) && link.IsValid)
            {
                LiveBuilderWindowControls.Note("По этажам здания «" + link.Building.name + "» — «На этажах» в карточке «Этажи в здании».");
                return true;
            }

            if (!BoxModelShapeCollector.IsPieceInsideStoreyCopies(selected.transform))
                return false;

            LiveBuilderWindowControls.Note("По этажам здания — вместе с объектом или группой, в которой стоит.");
            return true;
        }

        private static BoxModelRepeat NearestParentRepeat(Transform node)
        {
            return node.parent != null ? node.parent.GetComponentInParent<BoxModelRepeat>() : null;
        }


        // Сколько всего и шаг группы повтора — прямо здесь; «шаг — этаж» и «убрать повтор».
        private static bool DrawRepeatGroup(BoxModelRepeat repeat, string title)
        {
            LiveBuilderWindowControls.Note(title);

            var count = EditorGUILayout.IntSlider(new GUIContent("Сколько всего", "Вместе с оригиналом. Ручка повтора в окне сцены делает то же"),
                repeat.Count, 1, BoxModelRepeat.MaxCount);
            var step = EditorGUILayout.Vector3Field(new GUIContent("Шаг, м", "Сдвиг каждой следующей копии в осях объекта"), repeat.Step);

            if (count != repeat.Count || step != repeat.Step)
            {
                Undo.RecordObject(repeat, "Edit Repeat");
                repeat.Count = count;
                repeat.Step = step;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Шаг — этаж", "Шаг вверх на высоту этажа")))
                {
                    Undo.RecordObject(repeat, "Edit Repeat");
                    repeat.Step = new Vector3(0f, LiveBuilderStoreys.StoreyHeight, 0f);
                }

                if (GUILayout.Button(new GUIContent("Убрать повтор", "Копии исчезнут, оригинал останется на месте")))
                {
                    LiveBuilderRepeatActions.Unwrap(repeat);
                    GUIUtility.ExitGUI();
                }
            }

            return true;
        }


        // Копии вверх у стены или проёма вне здания — сколько и шаг; в здании их задаёт привязка, тогда не здесь.
        private static bool DrawCopiesUp(GameObject selected)
        {
            if (selected.GetComponent<SplineBuildingStoreyLink>() != null || !selected.TryGetComponent<IStoreyCopies>(out var copies) || copies.CopiesUp <= 1)
                return false;

            LiveBuilderWindowControls.Note("Копии вверх:");
            var count = EditorGUILayout.IntSlider(new GUIContent("Сколько всего", "Друг над другом, вместе с этим"), copies.CopiesUp, 1, 60);
            var step = EditorGUILayout.FloatField(new GUIContent("Шаг вверх, м", "Обычно высота этажа"), copies.CopyStep);

            if (count != copies.CopiesUp || !Mathf.Approximately(step, copies.CopyStep))
            {
                Undo.RecordObject((Object)copies, "Edit Copies Up");
                copies.CopiesUp = count;
                copies.CopyStep = Mathf.Max(0.1f, step);
            }

            return true;
        }


        // Ряд Array у проёма — сколько в ряд и зазор; остальное — в компоненте Array.
        private static bool DrawArrayRow(GameObject selected)
        {
            if (!selected.TryGetComponent<ArrayModifier>(out var array))
                return false;

            LiveBuilderWindowControls.Note("Ряд (Array) — сетка и прочее в его компоненте:");
            var counts = array.Counts;
            var count = EditorGUILayout.IntSlider(new GUIContent("Сколько в ряд", "Вместе с этим, вдоль его X"), counts.x, 1, 60);
            var gap = array.StepsFromSize
                ? EditorGUILayout.FloatField(new GUIContent("Зазор, м", "Между соседними, от края до края"), array.Gap.x)
                : array.Gap.x;

            if (count == counts.x && Mathf.Approximately(gap, array.Gap.x))
                return true;

            Undo.RecordObject(array, "Edit Array Row");
            array.Counts = new Vector3Int(count, counts.y, counts.z);
            array.Gap = new Vector3(gap, array.Gap.y, array.Gap.z);
            ArrayModifierBuilder.Rebuild(array);
            return true;
        }


        // ── Повторить ────────────────────────────────────────────────────

        // Не повторяется — сколько всего и куда; механизм — по тому, что выделено: проём — ряд Array, стена вне здания — копии вверх,
        // коробки, лестницы и группы — группа повтора.
        private static void DrawNewRepeat(GameObject selected)
        {
            newRepeatCount = EditorGUILayout.IntSlider(new GUIContent("Сколько всего", "Вместе с оригиналом"), newRepeatCount, 2, MaxNewCount);

            if (selected.TryGetComponent<GeometryCutter>(out var cutter))
                DrawCutterRow(cutter);
            else if (selected.TryGetComponent<SplineWall>(out var wall))
                DrawWallCopiesUp(wall);
            else
                DrawWrapActions(selected);
        }

        // Проём — ряд вдоль стены: модификатор Array, копии встают по ширине проёма через зазор.
        private static void DrawCutterRow(GeometryCutter cutter)
        {
            if (!GUILayout.Button(new GUIContent("Повторить вдоль стены", "Ещё проёмы рядом через " + LiveBuilderSceneText.FormatMeters(RowGap) + " м — модификатор Array")))
                return;

            var array = Undo.AddComponent<ArrayModifier>(cutter.gameObject);
            array.Counts = new Vector3Int(newRepeatCount, 1, 1);
            array.StepsFromSize = true;
            array.Gap = new Vector3(RowGap, 0f, 0f);
            ArrayModifierBuilder.Rebuild(array);
            GUIUtility.ExitGUI();
        }

        // Стена вне здания — копии вверх на высоту этажа; в здании этажи задаёт привязка.
        private static void DrawWallCopiesUp(SplineWall wall)
        {
            if (IsInBuilding(wall.gameObject))
            {
                LiveBuilderWindowControls.Note("В здании стену повторяет «На этажах» у привязки.");
                return;
            }

            if (!GUILayout.Button(new GUIContent("Повторить вверх ↑", "Та же стена на этажах выше: шаг — высота этажа")))
                return;

            IStoreyCopies copies = wall;
            Undo.RecordObject(wall, "Copies Up");
            copies.CopiesUp = newRepeatCount;
            copies.CopyStep = LiveBuilderStoreys.StoreyHeight;
        }

        // Коробки и лестницы — завернуть в группу повтора: вверх на этаж (вне здания — там этажи повторяет привязка), вдоль X, вдоль Z.
        private static void DrawWrapActions(GameObject selected)
        {
            // Объект или группу целиком повтор не повторяет: свои коробки он считает сам.
            if (LiveBuilderRepeatActions.SelectedPieces().Count == 0)
            {
                LiveBuilderWindowControls.Note("Объект из коробок целиком — «Копии ×N» в карточке «Перенос и копии»; повтор — у его коробок, комнат и лестниц.");
                return;
            }

            if (!LiveBuilderRepeatActions.CanWrapSelection(out var reason))
            {
                LiveBuilderWindowControls.Note("Повторить нельзя: " + reason);
                return;
            }

            var bounds = LiveBuilderRepeatActions.SelectedBounds();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!IsInBuilding(selected) && GUILayout.Button(new GUIContent("Вверх ↑", "Шаг — высота этажа")))
                    Wrap(new Vector3(0f, LiveBuilderStoreys.StoreyHeight, 0f));

                if (GUILayout.Button(new GUIContent("По X", "Шаг — ширина выделенного по X, по сетке")))
                    Wrap(new Vector3(StepAround(bounds.size.x), 0f, 0f));

                if (GUILayout.Button(new GUIContent("По Z", "Шаг — ширина выделенного по Z, по сетке")))
                    Wrap(new Vector3(0f, 0f, StepAround(bounds.size.z)));
            }
        }

        private static void Wrap(Vector3 step)
        {
            var repeat = LiveBuilderRepeatActions.WrapSelection(step);

            if (repeat != null)
                repeat.Count = newRepeatCount;

            GUIUtility.ExitGUI();
        }


        // ── Помощники ────────────────────────────────────────────────────

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
