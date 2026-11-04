using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Выделено» про коробки: одна — режим, угол и размер в осях объекта, скосы рёбер (<see cref="BoxModelChamferPanel"/>),
    /// материалы сторон; несколько — сколько их. Всё с Undo.
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
            DrawMaterials(part);
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

                LiveBuilderWindowControls.Explanation("BoxMaterials", "Быстрее — перетащи материал из Project или палитры уровня на грань в окне сцены: он ляжет в эту сторону. " +
                                               "С Alt — во всю коробку.");
            }
        }
    }
}
