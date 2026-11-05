using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вкладка «Уровень» окна Live Builder — общее на весь уровень: сверху «Проверка уровня» — всё подозрительное на сцене списком
    /// с выделением по клику (<see cref="LiveBuilderLevelCheck"/>); дальше то, что меняют редко: этажи уровня (высота этажа, пол первого, высота разреза) и библиотека типов стен — все типы проекта списком,
    /// толщина и материал правятся прямо здесь, стены этих типов перестраиваются сами.
    /// Ниже — уборка файлов мешей, которые больше никому не нужны, и как ведёт себя само окно: переключать ли вкладки самим.
    /// </summary>
    public sealed class LiveBuilderLevelTab : LiveBuilderWindowTab
    {
        // Проверка уровня: что нашла последняя проверка и при какой версии сцены: поправил что-то — список помечен «устарел».
        private static List<LiveBuilderLevelIssue> lastIssues;
        private static int lastCheckSceneVersion = -1;


        // ── Корешок ──────────────────────────────────────────────────────

        public override string Title => "Уровень";

        public override Texture2D Icon => LiveBuilderOptionIcons.White(LiveBuilderOptionIcon.Level);

        public override LiveBuilderTabColor TabColor => LiveBuilderTabColor.Level;


        // ── Место ────────────────────────────────────────────────────────

        public override int Order => 2;

        public override string PreferenceKey => "Level";


        // ── Содержимое ───────────────────────────────────────────────────

        public override void Draw()
        {
            DrawLevelCheck();
            DrawStoreys();
            DrawWallTypes();
            DrawMeshFiles();
            DrawWindowBehaviour();
        }


        // ── Проверка уровня ──────────────────────────────────────────────

        private static void DrawLevelCheck()
        {
            using (LiveBuilderWindowControls.Card("Проверка уровня"))
            {
                if (GUILayout.Button(new GUIContent("Проверить уровень", "Всё подозрительное на сцене списком: перекрещённые контуры, повёрнутые здания, " +
                                                                         "привязка к пропавшему этажу, проём, который ничего не режет, вырезающая коробка выше стены")))
                {
                    lastIssues = LiveBuilderLevelCheck.FindIssues();
                    lastCheckSceneVersion = BoxModelRebuilder.SceneVersion;
                }

                if (lastIssues == null)
                    return;

                if (lastCheckSceneVersion != BoxModelRebuilder.SceneVersion)
                    LiveBuilderWindowControls.Note("Сцену правили после проверки — проверь снова.");

                if (lastIssues.Count == 0)
                    LiveBuilderWindowControls.Note("Ничего подозрительного.");

                foreach (var issue in lastIssues)
                    DrawIssue(issue);
            }
        }

        private static void DrawIssue(LiveBuilderLevelIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(issue.Text, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(issue.Target == null))
                    if (GUILayout.Button(new GUIContent("Выделить", "Выделить и показать в окне сцены"), EditorStyles.miniButton, GUILayout.Width(70f)))
                    {
                        Selection.activeObject = issue.Target;
                        SceneView.FrameLastActiveSceneView();
                    }
            }
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

                var groundLevel = EditorGUILayout.FloatField(new GUIContent("Пол первого этажа, м", "Высота в мире, от которой считаются этажи. Дом на цоколе 1 м — 1"),
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
                LiveBuilderWindowControls.Explanation("Удалённые объекты и этажи оставляют файлы мешей в папках «_BoxModels». Кнопка найдёт те, на которые не ссылается " +
                                                      "ни одна сцена и ни один префаб, покажет и уберёт в корзину системы.");

                if (GUILayout.Button(new GUIContent("Убрать лишние меши…", "Проверяет все сцены и префабы проекта и открытую сцену с несохранёнными правками")))
                {
                    BoxModelMeshCleanup.RemoveUnusedMeshFiles();
                    GUIUtility.ExitGUI();
                }
            }
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Переключать ли вкладки самим: выделил постройку — «Выделено», выбрал инструмент — «Инструмент».
        private static void DrawWindowBehaviour()
        {
            using (LiveBuilderWindowControls.Card("Окно"))
                LiveBuilderWindow.IsSwitchingTabsAutomatically = LiveBuilderWindowControls.Switch(
                    new GUIContent("Вкладки сами", "Да — выделил постройку в сцене или иерархии — открывается «Выделено», выбрал инструмент — «Инструмент». " +
                                                   "Пока печатаешь в поле, не переключаются. Нет — вкладки переключаешь только ты"),
                    LiveBuilderWindow.IsSwitchingTabsAutomatically, "Нет", "Да");
        }
    }
}
