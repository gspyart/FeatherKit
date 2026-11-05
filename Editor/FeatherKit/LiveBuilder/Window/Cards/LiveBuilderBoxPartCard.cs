using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Выделено» про коробки: одна — режим и размер, скосы рёбер (<see cref="BoxModelChamferPanel"/>), а под свёрнутым «Ещё» — угол в осях объекта,
    /// у вырезающей — что она режет из стен и зданий, материалы сторон; несколько — сколько их. Всё с Undo.
    /// </summary>
    public sealed class LiveBuilderBoxPartCard : LiveBuilderSelectionCard
    {
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

        // Сверху — режим и размер, их меняют каждый раз; угол, что режет и материалы — под «Ещё».
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
            }

            BoxModelChamferPanel.Draw(part);

            if (!LiveBuilderWindowControls.MoreSection("SelectedBox", IsMoreChanged(part), part.Mode == BoxModelPartMode.Cut ? "угол, что режет, материалы" : "угол, материалы"))
                return;

            DrawPlacement(part, model);
            DrawMaterials(part);
        }

        // «Ещё» раскрыто само, если вырезающая режет не всё или задан хоть один материал.
        private static bool IsMoreChanged(BoxModelPart part)
        {
            return (part.Mode == BoxModelPartMode.Cut && part.CutTargets != LiveBuilderCutTargets.Everything)
                || part.Material != null || part.BottomMaterial != null || part.SideMaterial != null || part.TopMaterial != null
                || part.RightMaterial != null || part.LeftMaterial != null || part.FrontMaterial != null || part.BackMaterial != null;
        }


        // Угол в осях объекта; у вырезающей — что она режет.
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

                if (part.Mode == BoxModelPartMode.Cut)
                    DrawCutTargets(part);
            }
        }

        // Что вырезающая коробка режет из стен по линии и зданий объекта. Свои коробки объекта режет всегда.
        private static void DrawCutTargets(BoxModelPart part)
        {
            var targets = (LiveBuilderCutTargets)LiveBuilderWindowControls.Switch(new GUIContent("Режет",
                    "Что режет из стен по линии и зданий этого объекта; свои коробки режет всегда. " +
                    "Всё — стены и перекрытия. Перекрытия — дыра под лестницу или шахту: стены и перегородки не трогает. " +
                    "Стены — проём: пол и потолок целы. Режет только то, что выше неё в иерархии"),
                (int)part.CutTargets, "Всё", "Перекрытия", "Стены");

            if (targets == part.CutTargets)
                return;

            Undo.RecordObject(part, "Change Cut Targets");
            part.CutTargets = targets;
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
