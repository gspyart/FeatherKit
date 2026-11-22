using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Поля секции Chamfer панели у выделенной коробки: скосы её рёбер (<see cref="BoxModelEdgeChamfer"/>) числами —
    /// насколько срез заходит на каждую из двух сторон ребра, и «×» — убрать. Полный скос — «Full», у пандуса — куда он поднимается и «Turn».
    /// Новый скос — точкой на ребре в окне сцены (<see cref="BoxModelChamferHandles"/>).
    /// </summary>
    public static class BoxModelChamferPanel
    {
        private static readonly string[] FaceNames = { "+X", "−X", "top", "bottom", "+Z", "−Z" };


        // Скосы только у добавляющих коробок: скос убирает объём.
        public static void Draw(BoxModelPart part)
        {
            if (part.Mode != BoxModelPartMode.Add)
                return;

            DrawRamp(part);

            // Копия: строку можно убрать посреди списка.
            foreach (var chamfer in new System.Collections.Generic.List<BoxModelEdgeChamfer>(part.Chamfers))
                DrawChamfer(part, chamfer);
        }


        // Пандус — тоже скос. Скосов нет — секция Chamfer панели — строка «+ Add».
        public static bool HasChamfers(BoxModelPart part)
        {
            return part.Mode == BoxModelPartMode.Add && part.Chamfers.Count > 0;
        }


        // Пандус: куда поднимается и «Turn» — на четверть оборота по часовой.
        private static void DrawRamp(BoxModelPart part)
        {
            if (!BoxModelRamps.IsRamp(part, out var risesTowardFace))
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Ramp rises toward " + BoxModelRamps.SideName(risesTowardFace), EditorStyles.miniBoldLabel);

                if (GUILayout.Button(new GUIContent("Turn", "Повернуть подъём на четверть оборота по часовой, если смотреть сверху. Tab в окне сцены — то же"),
                        EditorStyles.miniButton, GUILayout.Width(44f)))
                    BoxModelRamps.TurnRamp(part);
            }
        }


        // Ребро, насколько срез заходит на каждую его сторону и «×». Полный скос показан по коробке; поменял число — стал обычным.
        private static void DrawChamfer(BoxModelPart part, BoxModelEdgeChamfer chamfer)
        {
            var firstName = FaceNames[chamfer.FirstFace];
            var secondName = FaceNames[chamfer.SecondFace];

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Edge " + firstName + " / " + secondName, EditorStyles.miniBoldLabel);

                if (GUILayout.Button(new GUIContent("×", "Убрать скос — ребро снова прямое"), EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    Undo.RecordObject(part, "Remove Chamfer");
                    part.RemoveChamfer(chamfer.FirstFace, chamfer.SecondFace);
                    return;
                }
            }

            var shownOnFirst = chamfer.SizeOnFirstFaceIn(part.Size);
            var shownOnSecond = chamfer.SizeOnSecondFaceIn(part.Size);
            var sizeOnFirst = EditorGUILayout.FloatField(SizeLabel(firstName, chamfer.IsFullOnFirstFace), shownOnFirst);
            var sizeOnSecond = EditorGUILayout.FloatField(SizeLabel(secondName, chamfer.IsFullOnSecondFace), shownOnSecond);
            var isFirstChanged = !Mathf.Approximately(sizeOnFirst, shownOnFirst);
            var isSecondChanged = !Mathf.Approximately(sizeOnSecond, shownOnSecond);

            if (!isFirstChanged && !isSecondChanged)
                return;

            Undo.RecordObject(part, "Edit Chamfer");
            part.SetChamfer(new BoxModelEdgeChamfer(chamfer.FirstFace, chamfer.SecondFace, isFirstChanged ? sizeOnFirst : chamfer.SizeOnFirstFace,
                isSecondChanged ? sizeOnSecond : chamfer.SizeOnSecondFace));
        }

        private static GUIContent SizeLabel(string faceName, bool isFull)
        {
            return new GUIContent("On " + faceName + (isFull ? ", m (Full)" : ", m"),
                isFull ? "Срез во всю сторону при любом размере коробки. Поменял число — станет обычным" : "Насколько срез заходит на эту сторону от ребра");
        }
    }
}
