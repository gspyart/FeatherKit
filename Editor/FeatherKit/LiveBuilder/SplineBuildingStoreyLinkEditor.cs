using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инспектор привязки к этажам здания — вместо сырых полей одна понятная строка, как этажи у Revit:
    /// на каком этаже стоит и на каких повторяется — только этот, «с … по …» или до верха здания.
    /// Тот же блок показывает окно Live Builder у выделенного.
    /// </summary>
    [CustomEditor(typeof(SplineBuildingStoreyLink))]
    public class SplineBuildingStoreyLinkEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawControls((SplineBuildingStoreyLink)target);
        }


        // Этаж и повтор по этажам. Номера этажей — с единицы, как в подсказках окна.
        public static void DrawControls(SplineBuildingStoreyLink link)
        {
            if (!link.IsValid)
            {
                EditorGUILayout.HelpBox("Здание не задано — привязка ничего не делает.", MessageType.None);
                return;
            }

            var building = link.Building;
            var count = building.StoreyCount;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("В здании «" + building.name + "», этажей " + count, EditorStyles.miniLabel);

                if (GUILayout.Button("Выделить здание", EditorStyles.miniButton, GUILayout.Width(110f)))
                    Selection.activeGameObject = building.gameObject;
            }

            DrawStorey(link, count);
            DrawSpan(link, count);
            GUILayout.Label("Высоты и шаг — от здания: поменяешь здание — подстроится само.", EditorStyles.wordWrappedMiniLabel);
        }


        // Этаж выше, чем есть у здания: ползунок сам подвинул бы объект от одного выделения — только пишем, что не так.
        private static void DrawStorey(SplineBuildingStoreyLink link, int count)
        {
            if (link.Storey < 0 || link.Storey >= count)
            {
                EditorGUILayout.HelpBox("Стоит на этаже " + (link.Storey + 1) + ", а у здания этажей " + count + ": добавь зданию этажей — тогда этаж можно будет выбрать.",
                    MessageType.Warning);
                return;
            }

            var storey = EditorGUILayout.IntSlider(new GUIContent("Этаж", "На каком этаже здания стоит. Подвинешь — переедет туда на ту же высоту над полом"),
                link.Storey + 1, 1, count) - 1;

            if (storey != link.Storey)
                SplineBuildingLinkSync.MoveToStorey(link, storey);
        }


        // Только этот / по этаж … / до верха. Последний этаж с копией — как у самой привязки: у лестницы — без верхнего этажа.
        private static void DrawSpan(SplineBuildingStoreyLink link, int count)
        {
            var isRange = !link.IsToBuildingTop && link.HasLastStorey;
            var isThisOnly = !link.IsToBuildingTop && !link.HasLastStorey;
            var highestCopyStorey = Mathf.Max(link.Storey, count - 1 - (link.SkipsTopStorey ? 1 : 0));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("Повтор", "Только этот — уникальное. По этаж — копии выше, до выбранного этажа. " +
                                                                    "До верха — на всех этажах выше: сами добавляются и убираются вместе с этажами здания"));

                if (GUILayout.Toggle(isThisOnly, "Только этот", "ButtonLeft") && !isThisOnly)
                    SetSpan(link, false, false, link.LastStorey);

                if (GUILayout.Toggle(isRange, "По этаж…", "ButtonMid") && !isRange)
                    SetSpan(link, false, true, Mathf.Min(link.Storey + 1, highestCopyStorey));

                if (GUILayout.Toggle(link.IsToBuildingTop, "До верха", "ButtonRight") && !link.IsToBuildingTop)
                    SetSpan(link, true, false, link.LastStorey);
            }

            if (!isRange)
                return;

            // Только если двинули ползунок: иначе сама подрезка числа в пределы правила бы привязку от одного выделения.
            EditorGUI.BeginChangeCheck();
            var lastStorey = EditorGUILayout.IntSlider(new GUIContent("По этаж", "Последний этаж с копией, включительно"),
                Mathf.Clamp(link.LastStorey, link.Storey, highestCopyStorey) + 1, link.Storey + 1, highestCopyStorey + 1) - 1;

            if (EditorGUI.EndChangeCheck() && lastStorey != link.LastStorey)
                SetSpan(link, false, true, lastStorey);
        }

        private static void SetSpan(SplineBuildingStoreyLink link, bool isToTop, bool hasLastStorey, int lastStorey)
        {
            Undo.RecordObject(link, "Change Building Storeys");
            RecordCopies(link);
            link.IsToBuildingTop = isToTop;
            link.HasLastStorey = hasLastStorey;
            link.LastStorey = lastStorey;

            SplineBuildingLinkSync.Sync(link);
        }

        // Число копий вверх (у стены, выреза) — для Undo вместе с привязкой.
        private static void RecordCopies(SplineBuildingStoreyLink link)
        {
            if (link.TryGetComponent<IStoreyCopies>(out var storeyCopies))
                Undo.RecordObject((Object)storeyCopies, "Change Building Storeys");
        }
    }
}
