using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Страница окна Live Builder — общее на весь уровень, разделами-свёртками (раскрытость помнится):
    /// - Level Check — всё подозрительное на сцене списком, у строки — Select (<see cref="LiveBuilderLevelCheck"/>)
    /// - Storeys — высота этажа, пол первого, высота разреза; Wall Types — библиотека типов стен, правка на месте
    /// - Materials — материалы проекта (<see cref="LiveBuilderMaterialSettingsDrawer"/>); Mesh Files — уборка ненужных файлов мешей
    /// </summary>
    public sealed class LiveBuilderLevelPage
    {
        private const string PreferencePrefix = "Level.";
        private const LiveBuilderAccentColor Zone = LiveBuilderAccentColor.Level;

        // Что нашла последняя проверка и при какой версии сцены: поправил что-то — список помечен «устарел».
        private List<LiveBuilderLevelIssue> lastIssues;
        private int lastCheckSceneVersion = -1;


        public void Draw()
        {
            DrawLevelCheck();
            DrawStoreys();
            DrawWallTypes();
            DrawMaterials();
            DrawMeshFiles();
        }


        // ── Level Check ──────────────────────────────────────────────────

        private void DrawLevelCheck()
        {
            using var section = LiveBuilderWindowControls.FoldoutSection(PreferencePrefix + "Check", "Level Check", LevelCheckSummary(), Zone);

            if (!section.IsOpen)
                return;

            if (GUILayout.Button(new GUIContent("Check Level", "Всё подозрительное на сцене списком: перекрещённые контуры, повёрнутые здания, " +
                                                              "здание мимо этажей уровня по ошибке, привязка к пропавшему этажу")))
            {
                lastIssues = LiveBuilderLevelCheck.FindIssues();
                lastCheckSceneVersion = BoxModelRebuilder.SceneVersion;
            }

            if (lastIssues == null)
                return;

            if (lastCheckSceneVersion != BoxModelRebuilder.SceneVersion)
                LiveBuilderWindowControls.Note("Scene edited after the check — check again.");

            if (lastIssues.Count == 0)
                LiveBuilderWindowControls.Note("Nothing suspicious.");

            foreach (var issue in lastIssues)
                DrawIssue(issue);
        }

        private string LevelCheckSummary()
        {
            if (lastIssues == null)
                return "not checked yet";

            return lastIssues.Count == 0 ? "nothing found" : lastIssues.Count + " found";
        }

        private static void DrawIssue(LiveBuilderLevelIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(issue.Text, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(issue.Target == null))
                    if (GUILayout.Button(new GUIContent("Select", "Выделить и показать в окне сцены"), EditorStyles.miniButton, GUILayout.Width(60f)))
                    {
                        Selection.activeObject = issue.Target;
                        SceneView.FrameLastActiveSceneView();
                    }
            }
        }


        // ── Storeys ──────────────────────────────────────────────────────

        private static void DrawStoreys()
        {
            var summary = LiveBuilderStoreys.IsStoredInScene ? "stored in the scene" : "not stored in the scene";
            using var section = LiveBuilderWindowControls.FoldoutSection(PreferencePrefix + "Storeys", "Storeys", summary, Zone);

            if (!section.IsOpen)
                return;

            DrawStoreysPlace();

            var storeyHeight = EditorGUILayout.FloatField(new GUIContent("Storey Height, m", "От пола до пола следующего этажа. По метрикам — 4. Новые здания берут её же. " +
                                                                                            "Хранится в сцене — у всех одна"),
                LiveBuilderStoreys.StoreyHeight);

            if (!Mathf.Approximately(storeyHeight, LiveBuilderStoreys.StoreyHeight))
                LiveBuilderStoreys.StoreyHeight = storeyHeight;

            var groundLevel = EditorGUILayout.FloatField(new GUIContent("Storey 1 Floor, m", "Высота в мире, от которой считаются этажи. Дом на цоколе 1 м — 1. " +
                                                                                            "Хранится в сцене — у всех один"),
                LiveBuilderStoreys.GroundLevel);

            if (!Mathf.Approximately(groundLevel, LiveBuilderStoreys.GroundLevel))
                LiveBuilderStoreys.GroundLevel = groundLevel;

            var sectionHeight = EditorGUILayout.FloatField(new GUIContent("Section Height, m", "На сколько выше пола этажа режет вид «Комнаты» и «Стены вниз». " +
                                                                                              "Своя у каждого, в сцену не пишется"),
                LiveBuilderStoreys.SectionHeightAboveFloor);

            if (!Mathf.Approximately(sectionHeight, LiveBuilderStoreys.SectionHeightAboveFloor))
                LiveBuilderStoreys.SectionHeightAboveFloor = sectionHeight;
        }

        // Этажи ещё не в сцене — числа свои, на другом компьютере могут быть другими.
        private static void DrawStoreysPlace()
        {
            if (LiveBuilderStoreys.IsStoredInScene)
                return;

            LiveBuilderWindowControls.Note("Storeys of this scene are not stored in it yet: the numbers are yours, another person may see others.");

            if (GUILayout.Button(new GUIContent("Store in Scene", "Положить в сцену объект «Live Builder Level» с этими числами — " +
                                                                  "у всех, кто откроет сцену, этажи будут те же. Первая правка чисел ниже делает то же самое")))
                LiveBuilderStoreys.StoreInScene();
        }


        // ── Wall Types ───────────────────────────────────────────────────

        // Как в Revit: тип — толщина и материал, общие для всех стен этого типа.
        private static void DrawWallTypes()
        {
            var types = LiveBuilderWallTypes.AllTypes();
            using var section = LiveBuilderWindowControls.FoldoutSection(PreferencePrefix + "WallTypes", "Wall Types", types.Count + " types", Zone);

            if (!section.IsOpen)
                return;

            if (types.Count == 0)
            {
                LiveBuilderWindowControls.Note("No types yet. Starter set: exterior 0.3, partition 0.15, thin partition 0.1.");

                if (GUILayout.Button("Create Starter Set"))
                    LiveBuilderWallTypes.CreateStarterTypes();

                return;
            }

            foreach (var type in types)
                DrawWallType(type);

            EditorGUILayout.Space(2f);

            if (GUILayout.Button(new GUIContent("+ New Type", "Файл в папке типов; назови его в окне проекта")))
                LiveBuilderWallTypes.CreateNewType();
        }

        // Строка типа: имя — кнопка «выделить файл», толщина, материал. Правка — с Undo.
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
                var thickness = EditorGUILayout.FloatField(new GUIContent("", "Толщина, м"), type.Thickness, GUILayout.Width(44f));
                GUILayout.Label("m", EditorStyles.miniLabel, GUILayout.Width(12f));
                var material = (Material)EditorGUILayout.ObjectField(type.Material, typeof(Material), false, GUILayout.MinWidth(70f));

                if (!EditorGUI.EndChangeCheck())
                    return;

                Undo.RecordObject(type, "Change Wall Type");
                type.Thickness = thickness;
                type.Material = material;
                EditorUtility.SetDirty(type);
            }
        }


        // ── Materials ────────────────────────────────────────────────────

        private static void DrawMaterials()
        {
            using var section = LiveBuilderWindowControls.FoldoutSection(PreferencePrefix + "Materials", "Materials", "new builds, tile sizes", Zone);

            if (section.IsOpen)
                LiveBuilderMaterialSettingsDrawer.Draw();
        }


        // ── Mesh Files ───────────────────────────────────────────────────

        // Удалил объект или этажи — файл меша остаётся; кнопка находит такие по всему проекту.
        private static void DrawMeshFiles()
        {
            using var section = LiveBuilderWindowControls.FoldoutSection(PreferencePrefix + "MeshFiles", "Mesh Files", "clean unused", Zone, false);

            if (!section.IsOpen)
                return;

            if (GUILayout.Button(new GUIContent("Remove Unused Meshes…", "Удалённые объекты и этажи оставляют файлы мешей в папках «_BoxModels». " +
                                                                        "Находит те, на которые не ссылается ни одна сцена и ни один префаб, показывает и убирает в корзину системы. " +
                                                                        "Проверяет все сцены и префабы проекта и открытую сцену с несохранёнными правками")))
            {
                BoxModelMeshCleanup.RemoveUnusedMeshFiles();
                GUIUtility.ExitGUI();
            }
        }
    }
}
