using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона «Выделено» окна Live Builder: числа вместо прицеливания мышью.
    /// - коробка — режим, угол и размер в осях объекта, скосы рёбер (<see cref="BoxModelChamferPanel"/>), материалы сторон
    /// - коробки и лестницы — «повторить»: этажами вверх, вдоль X, вдоль Z
    /// - повтор — сколько копий и шаг, «убрать повтор»
    /// - комната, лестница, стена по линии, здание по контуру (и её точки: все углом, плавно, замкнуть), проём, объект из коробок — их собственные поля
    /// Всё с Undo.
    /// </summary>
    public sealed class LiveBuilderSelectionPanel
    {
        private Editor componentEditor;
        private Editor linkEditor;


        // ── Панель ───────────────────────────────────────────────────────

        public void Draw()
        {
            var parts = BoxModelActions.SelectedParts();
            var pieces = LiveBuilderRepeatActions.SelectedPieces();
            var repeat = LiveBuilderRepeatActions.SelectedRepeat();
            var hasAnything = false;

            if (parts.Count == 1)
            {
                DrawPart(parts[0]);
                hasAnything = true;
            }
            else if (parts.Count > 1)
            {
                LiveBuilderWindowControls.Computed("Коробок", parts.Count.ToString());
                hasAnything = true;
            }

            if (pieces.Count > 0)
            {
                DrawRepeatButtons();
                hasAnything = true;
            }

            if (repeat != null)
            {
                DrawRepeat(repeat);
                hasAnything = true;
            }

            if (DrawOwnComponent())
                hasAnything = true;

            if (DrawBuildingLink(Selection.activeGameObject))
                hasAnything = true;

            DrawInnerPathOwners();
            DrawBuildingOfPart();

            if (!hasAnything)
                EditorGUILayout.HelpBox("Ничего не выделено. Кликни в сцене по коробке, комнате, лестнице, зданию, стене или проёму " +
                                        "(инструменты «Коробка», «Комната», «Лестница») или выдели в иерархии.", MessageType.None);
        }


        // Выделено что-то от Live Builder — есть что показать в зоне выделенного.
        public static bool HasEditableSelection()
        {
            var selected = Selection.activeGameObject;

            if (selected == null)
                return false;

            return selected.GetComponentInParent<BoxModel>() != null || selected.GetComponentInParent<SplineWall>() != null
                || selected.GetComponentInParent<SplineBuilding>() != null || selected.GetComponent<BoxModelRepeat>() != null
                || selected.GetComponent<GeometryCutter>() != null;
        }


        // Выделена собранная часть здания (группа этажа, стены, пол, крыша) — числа у самого здания: кнопка перейти к нему.
        // Интерьер (перегородки, лестницы, проёмы) — не собранный, у него свои числа.
        private static void DrawBuildingOfPart()
        {
            var selected = Selection.activeGameObject;

            if (selected == null || selected.GetComponent<SplineBuilding>() != null)
                return;

            var building = selected.GetComponentInParent<SplineBuilding>();
            var isGenerated = selected.GetComponent<SplineBuildingPart>() != null
                || building != null && selected.transform.parent == building.transform && selected.name.StartsWith(SplineBuildingRebuilder.StoreyGroupPrefix);

            if (building == null || !isGenerated)
                return;

            using (LiveBuilderWindowControls.Card("Часть здания"))
            {
                LiveBuilderWindowControls.Note("Собирается сама. Числа — у здания. Спрятать в окне сцены — «глазик» в иерархии.");

                if (GUILayout.Button("Здание «" + building.name + "»"))
                    Selection.activeGameObject = building.gameObject;
            }
        }


        // Выделен объект из коробок, а в нём здания и стены по линии — кнопки, чтобы перейти к ним: их числа — у них самих.
        private static void DrawInnerPathOwners()
        {
            var selected = Selection.activeGameObject;

            if (selected == null || !selected.TryGetComponent<BoxModel>(out var model))
                return;

            var inner = new List<Component>();
            inner.AddRange(model.GetComponentsInChildren<SplineBuilding>(false));
            inner.AddRange(model.GetComponentsInChildren<SplineWall>(false));

            if (inner.Count == 0)
                return;

            using (LiveBuilderWindowControls.Card("Внутри объекта"))
            {
                LiveBuilderWindowControls.Note("Выдели, чтобы менять:");

                foreach (var owner in inner)
                {
                    var kind = owner is SplineBuilding ? "Здание" : "Стена";

                    if (GUILayout.Button(kind + " «" + owner.name + "»"))
                        Selection.activeGameObject = owner.gameObject;
                }
            }
        }


        public void Dispose()
        {
            if (componentEditor != null)
                Object.DestroyImmediate(componentEditor);

            if (linkEditor != null)
                Object.DestroyImmediate(linkEditor);
        }


        // ── Коробка ──────────────────────────────────────────────────────

        private static void DrawPart(BoxModelPart part)
        {
            var model = part.GetComponentInParent<BoxModel>();

            if (model == null)
                return;

            using (LiveBuilderWindowControls.Card("Коробка «" + part.name + "»"))
            {
                var mode = (BoxModelPartMode)LiveBuilderWindowControls.Switch(new GUIContent("Режим", "Добавить объём или вырезать дыру"), (int)part.Mode,
                    ("Добавить", LiveBuilderOptionIcon.BoxAdd), ("Вырезать", LiveBuilderOptionIcon.BoxCut));
                var min = BoxModelRebuilder.LocalMinOf(model, part);
                var newMin = EditorGUILayout.Vector3Field(new GUIContent("Угол, м", "Угол с наименьшими координатами, в осях объекта"), min);
                var newSize = EditorGUILayout.Vector3Field(new GUIContent("Размер, м"), part.Size);

                if (mode != part.Mode || newMin != min || newSize != part.Size)
                {
                    Undo.RecordObjects(new Object[] { part, part.transform }, "Edit Box");
                    part.Mode = mode;
                    part.transform.position = model.transform.TransformPoint(newMin);
                    part.Size = newSize;
                }

                if (GUILayout.Button(new GUIContent("Добавить ⇄ Вырезать", "У выделенных коробок: добавляющие вырезают, вырезающие добавляют")))
                    BoxModelActions.ToggleModeOfSelectedParts();
            }

            BoxModelChamferPanel.Draw(part);

            using (LiveBuilderWindowControls.Card("Материалы"))
            {
                var isCut = part.Mode == BoxModelPartMode.Cut;
                var material = LiveBuilderWindowControls.MaterialField("Вся коробка", "Пусто — материал объекта", part.Material);
                var bottom = LiveBuilderWindowControls.MaterialField(isCut ? "Пол" : "Низ", "Пусто — материал коробки", part.BottomMaterial);
                var sides = LiveBuilderWindowControls.MaterialField(isCut ? "Стены" : "Бока", "Пусто — материал коробки", part.SideMaterial);
                var top = LiveBuilderWindowControls.MaterialField(isCut ? "Потолок" : "Верх", "Пусто — материал коробки", part.TopMaterial);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(isCut ? "Каждая стена отдельно" : "Каждый бок отдельно", EditorStyles.miniBoldLabel);
                var right = LiveBuilderWindowControls.MaterialField("+X", "Пусто — материал всех боков", part.RightMaterial);
                var left = LiveBuilderWindowControls.MaterialField("−X", "Пусто — материал всех боков", part.LeftMaterial);
                var front = LiveBuilderWindowControls.MaterialField("+Z", "Пусто — материал всех боков", part.FrontMaterial);
                var back = LiveBuilderWindowControls.MaterialField("−Z", "Пусто — материал всех боков", part.BackMaterial);

                if (material != part.Material || bottom != part.BottomMaterial || sides != part.SideMaterial || top != part.TopMaterial
                    || right != part.RightMaterial || left != part.LeftMaterial || front != part.FrontMaterial || back != part.BackMaterial)
                {
                    Undo.RecordObject(part, "Edit Box Materials");
                    part.Material = material;
                    part.BottomMaterial = bottom;
                    part.SideMaterial = sides;
                    part.TopMaterial = top;
                    part.RightMaterial = right;
                    part.LeftMaterial = left;
                    part.FrontMaterial = front;
                    part.BackMaterial = back;
                }

                LiveBuilderWindowControls.Explanation("BoxMaterials", "Быстрее — перетащи материал из Project или палитры уровня на грань в окне сцены: он ляжет в эту сторону. " +
                                               "С Alt — во всю коробку.");
            }
        }


        // ── Повтор ───────────────────────────────────────────────────────

        // Завернуть выделенное в повтор с готовым шагом. Выделены части разных уровней списка — сначала поправить выделение.
        private static void DrawRepeatButtons()
        {
            using (LiveBuilderWindowControls.Card("Повторить"))
            {
                if (!LiveBuilderRepeatActions.CanWrapSelection(out var reason))
                {
                    LiveBuilderWindowControls.Note("Нельзя: " + reason);
                    return;
                }

                LiveBuilderWindowControls.Note("Копии считаются из оригинала: правишь один — меняются все.");

                using (new EditorGUILayout.HorizontalScope())
                {
                    var bounds = LiveBuilderRepeatActions.SelectedBounds();

                    // В здании этажи повторяет привязка — блок «Этажи в здании»; второй способ того же здесь не нужен.
                    if (!IsSelectionInBuilding() && GUILayout.Button(new GUIContent("Этажи ↑", "Шаг — высота этажа")))
                        LiveBuilderRepeatActions.WrapSelection(new Vector3(0f, LiveBuilderStoreys.StoreyHeight, 0f));

                    if (GUILayout.Button(new GUIContent("По X", "Шаг — ширина выделенного по X, по сетке")))
                        LiveBuilderRepeatActions.WrapSelection(new Vector3(StepAround(bounds.size.x), 0f, 0f));

                    if (GUILayout.Button(new GUIContent("По Z", "Шаг — ширина выделенного по Z, по сетке")))
                        LiveBuilderRepeatActions.WrapSelection(new Vector3(0f, 0f, StepAround(bounds.size.z)));
                }
            }
        }

        // Выделенное — в здании по контуру: этажи там повторяет привязка к зданию.
        private static bool IsSelectionInBuilding()
        {
            var selected = Selection.activeGameObject;
            return selected != null && selected.GetComponentInParent<SplineBuilding>() != null;
        }

        // Шаг повтора по размеру выделенного — с запасом в клетку сетки, чтобы копии не слиплись.
        private static float StepAround(float size)
        {
            var grid = BoxModelGrid.CurrentStep;
            return Mathf.Max(grid, Mathf.Ceil(size / grid) * grid + grid);
        }


        private static void DrawRepeat(BoxModelRepeat repeat)
        {
            using (LiveBuilderWindowControls.Card("Повтор «" + repeat.name + "»"))
                DrawRepeatFields(repeat);
        }

        private static void DrawRepeatFields(BoxModelRepeat repeat)
        {
            var count = EditorGUILayout.IntSlider(new GUIContent("Сколько всего", "Вместе с оригиналом. Ручка-стрелка в сцене делает то же"),
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
                    LiveBuilderRepeatActions.Unwrap(repeat);
            }

            LiveBuilderWindowControls.Explanation("Repeat", "Исключение для одной копии — коробка ниже группы в списке объектов: она главнее.");
        }


        // ── Свои поля компонента ─────────────────────────────────────────

        // Комната, лестница, стена, проём или объект — их инспектор прямо в окне. Был ли такой.
        private bool DrawOwnComponent()
        {
            var selected = Selection.activeGameObject;

            if (selected == null)
                return false;

            var component = OwnComponentOf(selected);

            if (component == null)
                return false;

            using (LiveBuilderWindowControls.Card(selected.name))
            {
                DrawCachedInspector(component, null, ref componentEditor);

                if (component is BoxModel && BoxModelActions.CanFreezeToProBuilder && GUILayout.Button(new GUIContent("В ProBuilder", "Копия объекта обычным мешем для ручной правки; сам объект выключается")))
                    BoxModelActions.FreezeSelectedModel();
            }

            if (component is SplineWall or SplineBuilding)
                DrawPathActions(component.GetComponent<SplineContainer>(), component is SplineWall);

            if (component is SplineBuilding building)
                DrawLevelAlignment(building);

            return true;
        }

        // Здание не на этажах уровня (поставлено раньше или сдвинуто) — его этажи не совпадают с окном: кнопка выровнять.
        private static void DrawLevelAlignment(SplineBuilding building)
        {
            if (LiveBuilderBuildingContext.IsAlignedToLevel(building))
                return;

            using (LiveBuilderWindowControls.Card("Этажи уровня"))
            {
                EditorGUILayout.HelpBox("Этажи здания не совпадают с этажами уровня: низ здания не на полу этажа или высота этажа другая. " +
                                        "Тогда «этаж» в окне и в здании — разные.", MessageType.Warning);

                if (GUILayout.Button(new GUIContent("Поставить на этажи уровня", "Низ здания — на ближайший пол этажа уровня, высота этажа — как в окне. Интерьер переедет вместе")))
                    LiveBuilderBuildingContext.AlignToLevel(building);
            }
        }


        // Этажи в здании — у всего, что стоит в здании: привязано — этаж и повтор; не привязано — кнопка привязать. Был ли блок.
        private bool DrawBuildingLink(GameObject selected)
        {
            if (selected == null)
                return false;

            var link = selected.GetComponent<SplineBuildingStoreyLink>();

            if (link != null && link.IsValid)
            {
                using (LiveBuilderWindowControls.Card("Этажи в здании"))
                {
                    DrawCachedInspector(link, typeof(SplineBuildingStoreyLinkEditor), ref linkEditor);
                }

                return true;
            }

            var building = selected.GetComponentInParent<SplineBuilding>();

            if (building == null || !CanLinkToStoreys(selected))
                return false;

            using (LiveBuilderWindowControls.Card("Этажи в здании"))
            {
                LiveBuilderWindowControls.Note("Стоит в здании, но не следит за его этажами.");

                if (GUILayout.Button(new GUIContent("Привязать к этажам здания «" + building.name + "»",
                        "Встанет на свой этаж здания и будет следить за высотой этажа; повтор по этажам — здесь же")))
                    SplineBuildingLinkSync.Attach(selected, building, false, selected.GetComponent<BoxModelStairs>() != null);
            }

            return true;
        }

        // Привязать можно перегородку, вырез, лестницу, коробку и комнату — не само здание и не его собранные части.
        private static bool CanLinkToStoreys(GameObject selected)
        {
            return selected.GetComponent<SplineWall>() != null || selected.GetComponent<GeometryCutter>() != null
                || selected.GetComponent<BoxModelStairs>() != null || selected.GetComponent<BoxModelPart>() != null || selected.GetComponent<BoxModelRoom>() != null;
        }

        // Стена или здание: все точки углом или плавно; стену — замкнуть или разомкнуть (здание всегда замкнуто).
        // Вид точек — выбранных в сцене, а если не выбрано ни одной, всех; убрать выбранные; замкнуть стену. Как править в сцене — подпись.
        private static void DrawPathActions(SplineContainer container, bool canOpen)
        {
            using (LiveBuilderWindowControls.Card("Точки"))
            {
                var total = 0;

                foreach (var spline in container.Splines)
                    total += spline.Count;

                var isOwnSelection = LiveBuilderContourSelection.Owner != null && LiveBuilderContourSelection.Owner.GetComponent<SplineContainer>() == container;
                var selected = isOwnSelection ? LiveBuilderContourSelection.Knots.Count : 0;
                LiveBuilderWindowControls.Computed("Точек", total + (selected > 0 ? ", выбрано " + selected : ""));

                var current = LiveBuilderContourSelection.CommonShapeOf(container);
                var chosen = LiveBuilderWindowControls.Switch(
                    new GUIContent(selected > 0 ? "Выбранные" : "Все точки", "Угол — прямые отрезки. Плавно — кривая сама проходит через точку. " +
                                                                           "Безье — у точки два уса, ими гнёшь кривую в сцене. Выбрано в сцене — меняются выбранные, иначе все"),
                    current, ("Угол", LiveBuilderOptionIcon.Corner), ("Плавно", LiveBuilderOptionIcon.Smooth), ("Безье", LiveBuilderOptionIcon.Bezier));

                if (chosen != current && chosen >= 0)
                    LiveBuilderContourSelection.ApplyShape(container, (LiveBuilderKnotShape)chosen);

                if (selected > 0)
                    DrawCornerRadius(container);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(selected == 0))
                        if (GUILayout.Button(new GUIContent("Убрать выбранные", "Как Delete в сцене. Меньше трёх точек у замкнутого контура и двух у линии не останется")))
                            LiveBuilderContourKeys.RemoveSelectedKnots(container);

                    using (new EditorGUI.DisabledScope(!container.Spline.Closed && container.Spline.Count < 3))
                        if (canOpen && GUILayout.Button(new GUIContent(container.Spline.Closed ? "Разомкнуть" : "Замкнуть",
                                "Замкнуть — последняя точка соединится с первой, разомкнуть — замыкающий отрезок уберётся. В сцене — клавиша C")))
                            LiveBuilderContourKeys.ToggleClosed(container);
                }

                LiveBuilderWindowControls.Explanation("Points", "В сцене, в «Выделении» (" + LiveBuilderToolSetting.HotkeyOf(LiveBuilderTool.Select) + ") или в инструменте этой постройки: тяни точку, «+» на отрезке — новая точка, " +
                                               "«+» за концом — продлить, Shift+клик или Shift+рамка — выбрать несколько, двойной клик — угол → плавно → Безье, " +
                                               "у Безье тяни усы, у угла — голубая ручка скругления, стрелки — на шаг сетки, Delete — убрать.");
            }
        }

        // Скругление выбранных углов одним числом: разные — поле пустое, введёшь — у всех станет это.
        private static void DrawCornerRadius(SplineContainer container)
        {
            var corners = new List<(Spline Spline, int Knot)>();

            foreach (var (splineIndex, knotIndex) in LiveBuilderContourSelection.Knots)
                if (LiveBuilderCornerRadiusHandle.CanRound(container.Splines[splineIndex], knotIndex))
                    corners.Add((container.Splines[splineIndex], knotIndex));

            if (corners.Count == 0)
                return;

            var first = LiveBuilderCornerRadii.RadiusOf(corners[0].Spline, corners[0].Knot);
            var isMixed = corners.Exists(corner => !Mathf.Approximately(LiveBuilderCornerRadii.RadiusOf(corner.Spline, corner.Knot), first));

            EditorGUI.showMixedValue = isMixed;
            EditorGUI.BeginChangeCheck();
            var radius = EditorGUILayout.FloatField(new GUIContent("Скругление, м", "Радиус выбранных углов. 0 — острый угол. Из скольких кусков дуга — " +
                                                                                    "в «Тонко» у стены или здания, 1 кусок — срез"), first);
            EditorGUI.showMixedValue = false;

            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(container, "Round Corners");

            foreach (var (spline, knot) in corners)
                LiveBuilderCornerRadii.SetRadius(spline, knot, Mathf.Max(0f, radius));
        }

        // Инспектор компонента внутри окна. Undo и пересборка здания пересоздают объект с тем же номером —
        // готовый инспектор тогда держит мёртвый объект и сыплет ошибками: такой собираем заново.
        private static void DrawCachedInspector(Component component, System.Type editorType, ref Editor editor)
        {
            if (editor != null && (editor.target == null || editor.target != component))
            {
                Object.DestroyImmediate(editor);
                editor = null;
            }

            Editor.CreateCachedEditor(component, editorType, ref editor);
            editor.OnInspectorGUI();
        }

        private static Component OwnComponentOf(GameObject selected)
        {
            var candidates = new List<Component>
            {
                selected.GetComponent<GeometryCutter>(),
                selected.GetComponent<BoxModelRoom>(),
                selected.GetComponent<BoxModelStairs>(),
                selected.GetComponent<SplineWall>(),
                selected.GetComponent<SplineBuilding>(),
                selected.GetComponent<BoxModel>()
            };

            return candidates.Find(candidate => candidate != null);
        }
    }
}
