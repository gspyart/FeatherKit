using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Выделено» про коробки: одна — режим и размер; у вырезающей — что она режет из стен и зданий («Режет») и какие из них задевает
    /// сейчас — строкой «Режет: «…»» (порядок в списке для этого не важен, <see cref="BoxModelSolverBox.CutsTarget"/>);
    /// скосы рёбер (<see cref="BoxModelChamferPanel"/>), а под свёрнутым «Ещё» — угол в осях объекта, материалы сторон; несколько — сколько их. Всё с Undo.
    /// </summary>
    public sealed class LiveBuilderBoxPartCard : LiveBuilderSelectionCard
    {
        // Что задевает выделенная вырезающая коробка — до правки сцены.
        private static readonly System.Collections.Generic.List<Component> TouchedTargets = new();
        private static BoxModelPart touchedTargetsPart;
        private static int touchedTargetsSceneVersion = -1;

        public override int Order => 10;


        public override bool Draw(GameObject selected)
        {
            var parts = BoxModelActions.SelectedParts();

            if (parts.Count == 1)
                DrawPart(parts[0]);
            else if (parts.Count > 1)
                LiveBuilderWindowControls.Computed("Коробок", parts.Count.ToString());

            return parts.Count > 0;
        }

        // Сверху — режим и размер, их меняют каждый раз, у вырезающей — «Режет» и что она задевает; угол и материалы — под «Ещё».
        private static void DrawPart(BoxModelPart part)
        {
            var model = part.GetComponentInParent<BoxModel>();

            if (model == null)
                return;

            using (LiveBuilderWindowControls.Card("Коробка «" + part.name + "»"))
            {
                var mode = (BoxModelPartMode)LiveBuilderWindowControls.Switch(new GUIContent("Режим", "Добавить объём или вырезать дыру"), (int)part.Mode,
                    ("Добавить", LiveBuilderOptionIcon.BoxAdd), ("Вырезать", LiveBuilderOptionIcon.BoxCut));
                var newSize = EditorGUILayout.Vector3Field(new GUIContent("Размер, м"), part.Size);

                if (mode != part.Mode || newSize != part.Size)
                {
                    Undo.RecordObject(part, "Edit Box");
                    part.Mode = mode;
                    part.Size = newSize;
                }

                if (GUILayout.Button(new GUIContent("Добавить ⇄ Вырезать", "У выделенных коробок: добавляющие вырезают, вырезающие добавляют")))
                    BoxModelActions.ToggleModeOfSelectedParts();

                if (part.Mode == BoxModelPartMode.Cut)
                {
                    DrawCutTargets(part);
                    DrawTouchedTargets(part, model);
                }
            }

            BoxModelChamferPanel.Draw(part);

            if (!LiveBuilderWindowControls.MoreSection("SelectedBox", IsMoreChanged(part), "угол, материалы"))
                return;

            DrawPlacement(part, model);
            DrawMaterials(part);
        }

        // Что вырезающая коробка режет из стен по линии и зданий объекта. Свои коробки объекта режет всегда.
        private static void DrawCutTargets(BoxModelPart part)
        {
            var targets = (LiveBuilderCutTargets)LiveBuilderWindowControls.Switch(new GUIContent("Режет",
                    "Что режет из стен по линии и зданий этого объекта; свои коробки режет всегда. " +
                    "Всё — стены и перекрытия. Перекрытия — дыра под лестницу или шахту: стены и перегородки не трогает. " +
                    "Стены — проём: пол и потолок целы. Порядок в списке объектов не важен: режет всё это, что задевает"),
                (int)part.CutTargets, "Всё", "Только перекрытия", "Только стены");

            if (targets == part.CutTargets)
                return;

            Undo.RecordObject(part, "Change Cut Targets");
            part.CutTargets = targets;
        }

        // Какие стены и здания коробка задевает и режет сейчас — одной строкой; порядок в списке объектов для этого не важен.
        private static void DrawTouchedTargets(BoxModelPart part, BoxModel model)
        {
            var targets = TouchedTargetsOf(part, model);

            LiveBuilderWindowControls.Note(targets.Count > 0
                ? "Режет: " + NamesOf(targets) + "."
                : "Стен и зданий не задевает — режет только коробки объекта.");
        }

        // Стены и здания объекта вокруг коробки, чей собранный меш она задевает и чей вид режет («Режет»). Раз на правку сцены — карточку рисуют часто.
        private static System.Collections.Generic.List<Component> TouchedTargetsOf(BoxModelPart part, BoxModel model)
        {
            if (part == touchedTargetsPart && BoxModelRebuilder.SceneVersion == touchedTargetsSceneVersion)
                return TouchedTargets;

            touchedTargetsPart = part;
            touchedTargetsSceneVersion = BoxModelRebuilder.SceneVersion;
            TouchedTargets.Clear();

            var size = part.Size;
            var localBounds = new Bounds(BoxModelShapeCollector.LocalMinOf(model, part.transform) + size / 2f, size);
            var boxBounds = GeometryCutterLookup.WorldBoundsOf(localBounds, model.transform);
            var stage = UnityEditor.SceneManagement.StageUtility.GetStageHandle(part.gameObject);

            // «Только перекрытия» стену режет, только если у неё есть пол или потолок.
            foreach (var wall in stage.FindComponentsOfType<SplineWall>())
                if ((part.CutTargets != LiveBuilderCutTargets.SlabsOnly || wall.HasSlabs) && IsTouched(wall, part, boxBounds))
                    TouchedTargets.Add(wall);

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
                if (IsTouched(building, part, boxBounds))
                    TouchedTargets.Add(building);

            return TouchedTargets;
        }

        // Стена или здание в том же объекте, что и коробка, и их меш задевает её габарит.
        private static bool IsTouched(Component target, BoxModelPart part, Bounds boxBounds)
        {
            var targetModel = SplineWallRebuilder.OuterModelOf(target);

            if (targetModel == null || !part.transform.IsChildOf(targetModel.transform) || !target.gameObject.activeInHierarchy)
                return false;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
                if (renderer.bounds.Intersects(boxBounds))
                    return true;

            return false;
        }

        private static string NamesOf(System.Collections.Generic.List<Component> targets)
        {
            return string.Join(", ", targets.ConvertAll(target => "«" + target.name + "»"));
        }


        // «Ещё» раскрыто само, если задан хоть один материал.
        private static bool IsMoreChanged(BoxModelPart part)
        {
            return part.Material != null || part.BottomMaterial != null || part.SideMaterial != null || part.TopMaterial != null
                || part.RightMaterial != null || part.LeftMaterial != null || part.FrontMaterial != null || part.BackMaterial != null;
        }


        // Угол в осях объекта.
        private static void DrawPlacement(BoxModelPart part, BoxModel model)
        {
            using (LiveBuilderWindowControls.Card("Место"))
            {
                var min = BoxModelShapeCollector.LocalMinOf(model, part.transform);
                var newMin = EditorGUILayout.Vector3Field(new GUIContent("Угол, м", "Угол с наименьшими координатами, в осях объекта"), min);

                if (newMin != min)
                {
                    Undo.RecordObject(part.transform, "Move Box");
                    part.transform.position = model.transform.TransformPoint(newMin);
                }
            }
        }

        private static void DrawMaterials(BoxModelPart part)
        {
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

                LiveBuilderWindowControls.Explanation("Быстрее — перетащи материал из Project или палитры уровня на грань в окне сцены: он ляжет в эту сторону. " +
                                                      "С Alt — во всю коробку.");
            }
        }
    }
}
