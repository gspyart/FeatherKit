using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Карточка «Скосы» выделенной коробки в окне Live Builder: скосы её рёбер (<see cref="BoxModelEdgeChamfer"/>) числами —
    /// насколько срез заходит на каждую из двух сторон ребра, и «×» — убрать. Новый скос — точкой на ребре в окне сцены (<see cref="BoxModelChamferHandles"/>).
    /// </summary>
    public static class BoxModelChamferPanel
    {
        private static readonly string[] FaceNames = { "+X", "−X", "верх", "низ", "+Z", "−Z" };


        public static void Draw(BoxModelPart part)
        {
            using (LiveBuilderWindowControls.Card("Скосы"))
            {
                if (part.Mode != BoxModelPartMode.Add)
                {
                    LiveBuilderWindowControls.Note("Скосы — только у добавляющих коробок: скос срезает объём.");
                    return;
                }

                // Копия: строку можно убрать посреди списка.
                foreach (var chamfer in new System.Collections.Generic.List<BoxModelEdgeChamfer>(part.Chamfers))
                    DrawChamfer(part, chamfer);

                LiveBuilderWindowControls.Note("Новый скос — потяни точку посередине ребра в окне сцены внутрь коробки. " +
                                               "Пандус — верхнее ребро до противоположного нижнего, двускатная крыша — два верхних ребра.");
            }
        }


        // Ребро, насколько срез заходит на каждую его сторону и «×».
        private static void DrawChamfer(BoxModelPart part, BoxModelEdgeChamfer chamfer)
        {
            var firstName = FaceNames[chamfer.FirstFace];
            var secondName = FaceNames[chamfer.SecondFace];

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Ребро " + firstName + " / " + secondName, EditorStyles.miniBoldLabel);

                if (GUILayout.Button(new GUIContent("×", "Убрать скос — ребро снова прямое"), EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    Undo.RecordObject(part, "Remove Chamfer");
                    part.RemoveChamfer(chamfer.FirstFace, chamfer.SecondFace);
                    return;
                }
            }

            var sizeOnFirst = EditorGUILayout.FloatField(new GUIContent("По стороне " + firstName + ", м", "Насколько срез заходит на эту сторону от ребра"),
                chamfer.SizeOnFirstFace);
            var sizeOnSecond = EditorGUILayout.FloatField(new GUIContent("По стороне " + secondName + ", м", "Насколько срез заходит на эту сторону от ребра"),
                chamfer.SizeOnSecondFace);

            if (Mathf.Approximately(sizeOnFirst, chamfer.SizeOnFirstFace) && Mathf.Approximately(sizeOnSecond, chamfer.SizeOnSecondFace))
                return;

            Undo.RecordObject(part, "Edit Chamfer");
            part.SetChamfer(new BoxModelEdgeChamfer(chamfer.FirstFace, chamfer.SecondFace, sizeOnFirst, sizeOnSecond));
        }
    }
}
