using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Array — повтор выделенного (<see cref="LiveBuilderArray"/>), общая для всех видов:
    /// - свой повтор: Along, Count / Fill по X, «Y — up: To top», Z, Skip, узор под «⋯», Remove; у нескольких — поля всем, разное — «—»
    /// - повторяется вместе с чем-то выше — строкой и переход к тому повтору
    /// - пусто — «+ Add» с меню: To top, Along X, Along Z, Along wall (<see cref="LiveBuilderArrayActions"/>); у нескольких — каждому, у кого нет
    /// </summary>
    public sealed class LiveBuilderArrayCard : LiveBuilderSelectionCard
    {
        public override int Order => ArrayOrder;

        public override string Title => "Array";

        public override bool IsSharedByAllKinds => true;

        public override string AddTooltip => "Повторить выделенное: вверх по этажам до верха здания, по X, по Z или рядом вдоль стены";


        // Повтор бывает у всего, кроме этажа и собранной части здания; у чужого объекта — только если уже стоит.
        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            foreach (var each in selected)
                if (!IsRepeatable(each) && !each.TryGetComponent<LiveBuilderArray>(out _))
                    return false;

            return true;
        }

        private static bool IsRepeatable(GameObject selected)
        {
            var kind = LiveBuilderSelectionKinds.KindOf(selected);
            return kind is not (LiveBuilderSelectionKind.Other or LiveBuilderSelectionKind.Storey or LiveBuilderSelectionKind.BuildingPart);
        }


        public override bool IsEmptyFor(IReadOnlyList<GameObject> selected)
        {
            foreach (var each in selected)
                if (each.TryGetComponent<LiveBuilderArray>(out _) || ArrayAboveOf(each) != null)
                    return false;

            return true;
        }


        public override string SummaryOf(IReadOnlyList<GameObject> selected)
        {
            var arrays = ArraysOf(selected);

            if (selected.Count > 1)
                return arrays.Count + " of " + selected.Count + " have one";

            if (arrays.Count == 0)
                return null;

            var counts = LiveBuilderArrayLayout.CountsOf(arrays[0], LiveBuilderArrayCopyPoses.FilledContextOf(arrays[0]));
            return counts.x + " × " + counts.y + " × " + counts.z + " copies";
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            var arrays = ArraysOf(selected);

            if (arrays.Count == 0)
            {
                DrawArrayAbove(selected[0]);
                return;
            }

            DrawFields(arrays);

            if (arrays.Count < selected.Count)
                DrawAddToEachButton(selected);
        }


        // ── Свой повтор ──────────────────────────────────────────────────

        // Поля самого повтора: подсказки и Undo — его, правка нескольких — всем сразу.
        private static void DrawFields(List<LiveBuilderArray> arrays)
        {
            var serializedArrays = new SerializedObject(arrays.ToArray());
            serializedArrays.Update();

            DrawEnumSwitch(serializedArrays.FindProperty("along"), "Along", "Axes — по осям объекта. Wall — по X вдоль стены или контура здания над ним, с поворотом",
                "Axes", "Wall");
            DrawAxisX(serializedArrays);
            DrawAxisY(serializedArrays);
            DrawCountAndStep(serializedArrays, "Z");
            DrawSkips(serializedArrays);

            if (LiveBuilderWindowControls.MoreSection("Card.ArrayPattern", HasPattern(arrays), "pattern"))
            {
                EditorGUILayout.PropertyField(serializedArrays.FindProperty("variants"), new GUIContent("Variants", serializedArrays.FindProperty("variants").tooltip), true);
                EditorGUILayout.PropertyField(serializedArrays.FindProperty("pattern"), new GUIContent("Pattern"));
            }

            serializedArrays.ApplyModifiedProperties();

            if (arrays.Count == 1)
                DrawResult(arrays[0]);

            if (GUILayout.Button(new GUIContent("Remove", "Копии исчезнут, оригинал останется на месте")))
            {
                foreach (var array in arrays)
                    Undo.DestroyObjectImmediate(array);

                GUIUtility.ExitGUI();
            }
        }


        // X: ровно N или «сколько влезет» в длину.
        private static void DrawAxisX(SerializedObject serializedArrays)
        {
            var mode = serializedArrays.FindProperty("mode");
            DrawEnumSwitch(mode, "X", "Count — ровно столько копий. Fill — сколько влезет в длину с отступами, шаг растягивается поровну", "Count", "Fill");

            if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)LiveBuilderArrayMode.Count)
            {
                DrawCountAndStep(serializedArrays, "X");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);
                DrawCompact(serializedArrays.FindProperty("length"), "Length", 46f);
                DrawCompact(serializedArrays.FindProperty("margin"), "Margin", 46f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);
                DrawCompact(serializedArrays.FindProperty("stepX"), "Min step", 56f);
            }
        }


        // Y — вверх: «To top» — число до верха здания; шаг — этаж или свой.
        private static void DrawAxisY(SerializedObject serializedArrays)
        {
            var toTop = serializedArrays.FindProperty("countYToBuildingTop");
            var isStoreyStep = serializedArrays.FindProperty("stepYIsStoreyHeight");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Y — up", "Вверх: копии по этажам"), GUILayout.Width(44f));
                DrawToggle(toTop, LiveBuilderOptionIcon.AllStoreysAbove, "To top", "Число — до верхнего этажа здания: добавил этаж — добавилась копия");

                if (toTop.hasMultipleDifferentValues || !toTop.boolValue)
                    DrawCompact(serializedArrays.FindProperty("countY"), "Count", 40f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(48f);
                DrawToggle(isStoreyStep, LiveBuilderOptionIcon.Storey, "Step = storey", "Шаг — высота этажа: здания над повтором, иначе уровня");

                if (isStoreyStep.hasMultipleDifferentValues || !isStoreyStep.boolValue)
                    DrawCompact(serializedArrays.FindProperty("stepY"), "Step", 32f);
            }
        }


        private static void DrawCountAndStep(SerializedObject serializedArrays, string axis)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(axis == "X" ? "" : axis, GUILayout.Width(14f));
                DrawCompact(serializedArrays.FindProperty("count" + axis), "Count", 40f);
                DrawCompact(serializedArrays.FindProperty("step" + axis), "Step", 32f);
            }
        }


        // Пропуски по осям одной строкой: «1, 5», «2-4», «last».
        private static void DrawSkips(SerializedObject serializedArrays)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(new GUIContent("Skip", "Какие копии не строить, номера с 1: «1, 5», «2-4», «last»"), GUILayout.Width(32f));
                DrawCompact(serializedArrays.FindProperty("skipX"), "X", 13f);
                DrawCompact(serializedArrays.FindProperty("skipY"), "Y", 13f);
                DrawCompact(serializedArrays.FindProperty("skipZ"), "Z", 13f);
            }
        }


        // Сколько копий и какой шаг на деле — у Fill и от этажей; у лестницы по этажам — «и на верхнем».
        private static void DrawResult(LiveBuilderArray array)
        {
            var context = LiveBuilderArrayCopyPoses.FilledContextOf(array);
            var counts = LiveBuilderArrayLayout.CountsOf(array, context);
            var steps = LiveBuilderArrayLayout.StepsOf(array, context);
            LiveBuilderWindowControls.Note(counts.x + " × " + counts.y + " × " + counts.z + " copies, step X " + LiveBuilderSceneText.FormatMeters(steps.x) +
                                           " m, step Y " + LiveBuilderSceneText.FormatMeters(steps.y) + " m");

            if (array.CountYToBuildingTop && array.GetComponent<BoxModelStairs>() != null)
                LiveBuilderArrayEditor.DrawTopStoreyToggle(array);
        }


        private static bool HasPattern(List<LiveBuilderArray> arrays)
        {
            return arrays.Exists(array => !string.IsNullOrEmpty(array.Pattern) || array.Variants.Count > 0);
        }


        // ── Поля ─────────────────────────────────────────────────────────

        private static void DrawCompact(SerializedProperty property, string label, float labelWidth)
        {
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), GUILayout.MinWidth(labelWidth + 30f));
            EditorGUIUtility.labelWidth = oldLabelWidth;
        }


        // Перечисление из двух значений — две кнопки; разное у выделенных — ни одна не нажата.
        private static void DrawEnumSwitch(SerializedProperty property, string label, string tooltip, string first, string second)
        {
            var current = property.hasMultipleDifferentValues ? -1 : property.enumValueIndex;
            var chosen = LiveBuilderWindowControls.Switch(new GUIContent(label, tooltip), current, first, second);

            if (chosen >= 0 && chosen != current)
                property.enumValueIndex = chosen;
        }


        private static void DrawToggle(SerializedProperty property, LiveBuilderOptionIcon icon, string text, string tooltip)
        {
            var value = !property.hasMultipleDifferentValues && property.boolValue;
            var pressed = LiveBuilderWindowControls.IconToggle(value, icon, text, tooltip);

            if (pressed != value)
                property.boolValue = pressed;
        }


        // ── Повтор выше ──────────────────────────────────────────────────

        private static LiveBuilderArray ArrayAboveOf(GameObject selected)
        {
            return selected.transform.parent != null ? selected.transform.parent.GetComponentInParent<LiveBuilderArray>() : null;
        }


        // Повторяется вместе с группой или этажом выше — строкой, с кнопкой к тому повтору.
        private static void DrawArrayAbove(GameObject selected)
        {
            var arrayAbove = ArrayAboveOf(selected);

            if (arrayAbove == null)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.Note("Repeated together with «" + arrayAbove.name + "».");

                if (GUILayout.Button(new GUIContent("Select", "Выделить объект с этим повтором"), GUILayout.Width(52f)))
                    Selection.activeGameObject = arrayAbove.gameObject;
            }
        }


        // ── Добавить ─────────────────────────────────────────────────────

        public override void Add(IReadOnlyList<GameObject> selected, Rect buttonRect)
        {
            ShowAddMenu(selected, buttonRect);
        }


        private static void DrawAddToEachButton(IReadOnlyList<GameObject> selected)
        {
            var content = new GUIContent("+ Add to each", "Повтор каждому выделенному, у кого его нет");
            var rect = GUILayoutUtility.GetRect(content, EditorStyles.miniButton);

            if (GUI.Button(rect, content, EditorStyles.miniButton))
                ShowAddMenu(selected, rect);
        }


        private static void ShowAddMenu(IReadOnlyList<GameObject> selected, Rect buttonRect)
        {
            var targets = new List<GameObject>();

            foreach (var each in selected)
                if (!each.TryGetComponent<LiveBuilderArray>(out _))
                    targets.Add(each);

            var menu = new GenericMenu();
            var isInBuilding = SplineBuildingStoreys.BuildingOfStoreyContent(targets[0].transform) != null;
            menu.AddItem(new GUIContent(isInBuilding ? "To top" : "Up (storeys)"), false, () => AddToAll(targets, LiveBuilderArrayActions.RepeatUp));
            menu.AddItem(new GUIContent("Along X"), false, () => AddToAll(targets, target => LiveBuilderArrayActions.AddAlongAxis(target, 0)));
            menu.AddItem(new GUIContent("Along Z"), false, () => AddToAll(targets, target => LiveBuilderArrayActions.AddAlongAxis(target, 2)));

            if (targets.TrueForAll(target => LiveBuilderPathOwners.OwnerAbove(target.transform) != null))
                menu.AddItem(new GUIContent("Along wall"), false, () => AddToAll(targets, LiveBuilderArrayActions.AddAlongWall));
            else
                menu.AddDisabledItem(new GUIContent("Along wall (not in a wall or building)"));

            menu.DropDown(buttonRect);
        }

        private static void AddToAll(List<GameObject> targets, System.Action<GameObject> add)
        {
            Undo.SetCurrentGroupName("Add Array");

            foreach (var target in targets)
                if (target != null)
                    add(target);
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static List<LiveBuilderArray> ArraysOf(IReadOnlyList<GameObject> selected)
        {
            var arrays = new List<LiveBuilderArray>();

            foreach (var each in selected)
                if (each.TryGetComponent<LiveBuilderArray>(out var array))
                    arrays.Add(array);

            return arrays;
        }
    }
}
