using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона «Уровень» окна Live Builder — общее на весь уровень, что меняют редко:
    /// этажи уровня (высота этажа, пол нулевого, высота разреза) и библиотека типов стен — все типы проекта списком,
    /// толщина и материал правятся прямо здесь, стены этих типов перестраиваются сами.
    /// Внизу — уборка файлов мешей, которые больше никому не нужны.
    /// </summary>
    public static class LiveBuilderLevelPanel
    {
        public static void Draw()
        {
            DrawStoreys();
            DrawWallTypes();
            DrawMeshFiles();
        }


        // ── Этажи уровня ─────────────────────────────────────────────────

        private static void DrawStoreys()
        {
            using (LiveBuilderWindowControls.Card("Этажи уровня"))
            {
                var storeyHeight = EditorGUILayout.FloatField(new GUIContent("Высота этажа, м", "От пола до пола следующего этажа. По метрикам — 4. Новые здания берут её же"),
                    LiveBuilderStoreys.StoreyHeight);

                if (!Mathf.Approximately(storeyHeight, LiveBuilderStoreys.StoreyHeight))
                    LiveBuilderStoreys.StoreyHeight = storeyHeight;

                var groundLevel = EditorGUILayout.FloatField(new GUIContent("Пол нулевого этажа, м", "Высота в мире, от которой считаются этажи. Дом на цоколе 1 м — 1"),
                    LiveBuilderStoreys.GroundLevel);

                if (!Mathf.Approximately(groundLevel, LiveBuilderStoreys.GroundLevel))
                    LiveBuilderStoreys.GroundLevel = groundLevel;

                var sectionHeight = EditorGUILayout.FloatField(new GUIContent("Высота разреза, м", "На сколько выше пола этажа режет вид «Комнаты» и «Стены вниз»"),
                    LiveBuilderStoreys.SectionHeightAboveFloor);

                if (!Mathf.Approximately(sectionHeight, LiveBuilderStoreys.SectionHeightAboveFloor))
                    LiveBuilderStoreys.SectionHeightAboveFloor = sectionHeight;
            }
        }


        // ── Типы стен ────────────────────────────────────────────────────

        // Как в Revit: тип — толщина и материал, общие для всех стен этого типа. Имя — кнопка: выделить файл типа.
        private static void DrawWallTypes()
        {
            using (LiveBuilderWindowControls.Card("Типы стен"))
            {
                var types = LiveBuilderWallTypes.AllTypes();

                if (types.Count == 0)
                {
                    LiveBuilderWindowControls.Note("Типов пока нет. Набор: наружная 0,3, перегородка 0,15, тонкая перегородка 0,1.");

                    if (GUILayout.Button("Создать набор"))
                        LiveBuilderWallTypes.CreateStarterTypes();

                    return;
                }

                foreach (var type in types)
                    DrawWallType(type);

                EditorGUILayout.Space(2f);

                if (GUILayout.Button(new GUIContent("Новый тип", "Файл в папке типов; назови его в окне проекта")))
                    LiveBuilderWallTypes.CreateNewType();
            }
        }

        // Строка типа: имя, толщина, материал. Правка — с Undo.
        private static void DrawWallType(SplineWallType type)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(type.name, "Выделить файл типа"), EditorStyles.label, GUILayout.Width(EditorGUIUtility.labelWidth - 4f)))
                {
                    Selection.activeObject = type;
                    EditorGUIUtility.PingObject(type);
                }

                EditorGUI.BeginChangeCheck();
                var thickness = EditorGUILayout.FloatField(type.Thickness, GUILayout.Width(44f));
                GUILayout.Label("м", EditorStyles.miniLabel, GUILayout.Width(12f));
                var material = (Material)EditorGUILayout.ObjectField(type.Material, typeof(Material), false, GUILayout.MinWidth(70f));

                if (!EditorGUI.EndChangeCheck())
                    return;

                Undo.RecordObject(type, "Change Wall Type");
                type.Thickness = thickness;
                type.Material = material;
                EditorUtility.SetDirty(type);
            }
        }


        // ── Файлы мешей ──────────────────────────────────────────────────

        // Удалил объект или этажи — файл меша остаётся; кнопка находит такие по всему проекту.
        private static void DrawMeshFiles()
        {
            using (LiveBuilderWindowControls.Card("Файлы мешей"))
            {
                LiveBuilderWindowControls.Explanation("MeshFiles", "Удалённые объекты и этажи оставляют файлы мешей в папках «_BoxModels». Кнопка найдёт те, на которые не ссылается " +
                                               "ни одна сцена и ни один префаб, покажет и уберёт в корзину системы.");

                if (GUILayout.Button(new GUIContent("Убрать лишние меши…", "Проверяет все сцены и префабы проекта и открытую сцену с несохранёнными правками")))
                {
                    BoxModelMeshCleanup.RemoveUnusedMeshFiles();
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
