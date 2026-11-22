using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Заголовок карточки выделенного в панели — оранжевой полосой выделенного:
    /// - что выделено: вид · имя · размер; несколько — сколько и каких видов, плашки видов (клик — оставить выделенными только их)
    /// - путь построек и этажей над выделенным (<see cref="LiveBuilderIntoTarget.PathLinksOf"/>), клик по звену — выделить его
    /// - предупреждения жёлтой строкой: повтор больше предела, контур пересекает сам себя, здание повёрнуто в своём объекте
    /// </summary>
    public static class LiveBuilderCardHeader
    {
        private const string PathSeparator = " › ";
        private const float AccentWidth = 3f;

        private static GUIStyle titleStyle;
        private static GUIStyle pathLinkStyle;
        private static GUIStyle pathTextStyle;
        private static GUIStyle warningStyle;


        private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };

        private static GUIStyle PathLinkStyle => pathLinkStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = LiveBuilderAccentColors.ColorOf(LiveBuilderAccentColor.Selection) },
            hover = { textColor = LiveBuilderAccentColors.ColorOf(LiveBuilderAccentColor.Selection) * 1.2f },
            padding = new RectOffset(0, 0, 1, 1),
            margin = new RectOffset(0, 0, 0, 0)
        };

        private static GUIStyle PathTextStyle => pathTextStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            padding = new RectOffset(0, 0, 1, 1),
            margin = new RectOffset(0, 0, 0, 0)
        };

        private static GUIStyle WarningStyle => warningStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.75f, 0.3f) : new Color(0.55f, 0.4f, 0f) }
        };


        // ── Что показать ─────────────────────────────────────────────────

        // Звенья пути над выделенным и оно само — последним.
        public static List<Transform> PathOf(GameObject selected)
        {
            return LiveBuilderIntoTarget.PathLinksOf(selected.transform);
        }


        // «Box · Add Box · W 4 · D 3 · H 2.5 m»; несколько одного вида — «3 Box selected»; разных — «4 selected».
        public static string TitleOf(IReadOnlyList<GameObject> selected)
        {
            if (selected.Count > 1)
                return LiveBuilderSelectionKinds.IsOneKind(selected)
                    ? selected.Count + " " + LiveBuilderSelectionKinds.NameOf(LiveBuilderSelectionKinds.KindOf(selected[0])) + " selected"
                    : selected.Count + " selected";

            var kindName = LiveBuilderSelectionKinds.NameOf(LiveBuilderSelectionKinds.KindOf(selected[0]));
            var title = kindName + " · " + selected[0].name;
            return LiveBuilderSelectionInfo.TryDescribe(selected[0], out var info) ? title + " · " + info.SizeText : title;
        }


        // Что с выделенным не так — по строке на беду, без повторов.
        public static List<string> WarningsOf(IReadOnlyList<GameObject> selected)
        {
            var warnings = new List<string>();

            foreach (var each in selected)
            {
                if (each.TryGetComponent<LiveBuilderArray>(out var array))
                    AddOnce(warnings, LiveBuilderArrayCopyPoses.WarningsOf(array));

                if (!each.TryGetComponent<SplineBuilding>(out var building))
                    continue;

                if (SplineBuildingMeshBuilder.IsContourBroken(building))
                    AddOnce(warnings, "The outline crosses itself — part of the floor and roof is not built. Move the points apart.");

                if (LiveBuilderLevelCheck.IsRotatedInsideModel(building))
                    AddOnce(warnings, "Rotated inside its object — the interior will drift. Rotate the object instead.");
            }

            return warnings;
        }

        private static void AddOnce(List<string> warnings, IEnumerable<string> added)
        {
            foreach (var warning in added)
                AddOnce(warnings, warning);
        }

        private static void AddOnce(List<string> warnings, string warning)
        {
            if (!warnings.Contains(warning))
                warnings.Add(warning);
        }


        // ── Рисование ────────────────────────────────────────────────────

        public static void Draw(IReadOnlyList<GameObject> selected, float width)
        {
            var rect = EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(new GUIContent(TitleOf(selected)), TitleStyle);

            if (selected.Count == 1)
                DrawPath(selected[0], width);
            else if (!LiveBuilderSelectionKinds.IsOneKind(selected))
                DrawKindChips(selected);

            foreach (var warning in WarningsOf(selected))
                GUILayout.Label(new GUIContent("⚠ " + warning), WarningStyle);

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x - AccentWidth - 3f, rect.y, AccentWidth, rect.height), LiveBuilderAccentColors.ColorOf(LiveBuilderAccentColor.Selection));
        }


        // Звенья — ссылками; не влезает — сокращается с начала: «… › Storey 2 › Window».
        private static void DrawPath(GameObject selected, float width)
        {
            var links = PathOf(selected);

            if (links.Count < 2)
                return;

            var first = FirstFittingLink(links, width - 12f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (first > 0)
                    GUILayout.Label("…" + PathSeparator, PathTextStyle, GUILayout.ExpandWidth(false));

                for (var index = first; index < links.Count; index++)
                {
                    var isLast = index == links.Count - 1;
                    var content = new GUIContent(links[index].name, isLast ? "Выделено" : "Выделить «" + links[index].name + "»");

                    if (isLast)
                        GUILayout.Label(content, PathTextStyle, GUILayout.ExpandWidth(false));
                    else if (GUILayout.Button(content, PathLinkStyle, GUILayout.ExpandWidth(false)))
                        Selection.activeGameObject = links[index].gameObject;

                    if (!isLast)
                        GUILayout.Label(PathSeparator, PathTextStyle, GUILayout.ExpandWidth(false));
                }

                GUILayout.FlexibleSpace();
            }
        }

        // С какого звена путь влезает в ширину; последнее звено — всегда.
        private static int FirstFittingLink(List<Transform> links, float width)
        {
            var separator = PathTextStyle.CalcSize(new GUIContent(PathSeparator)).x;
            var ellipsis = PathTextStyle.CalcSize(new GUIContent("…" + PathSeparator)).x;
            var used = 0f;

            for (var index = links.Count - 1; index >= 0; index--)
            {
                used += PathTextStyle.CalcSize(new GUIContent(links[index].name)).x + (index < links.Count - 1 ? separator : 0f);

                if (index < links.Count - 1 && used + (index > 0 ? ellipsis : 0f) > width)
                    return index + 1;
            }

            return 0;
        }


        // Плашки «2 Box · 1 Cut»: клик — в выделении остаются только они.
        private static void DrawKindChips(IReadOnlyList<GameObject> selected)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var (kind, count) in LiveBuilderSelectionKinds.CountsOf(selected))
                    if (GUILayout.Button(new GUIContent(count + " " + LiveBuilderSelectionKinds.NameOf(kind), "Оставить выделенными только их"),
                            EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        Selection.objects = LiveBuilderSelectionKinds.OfKind(selected, kind).ToArray();

                GUILayout.FlexibleSpace();
            }

            LiveBuilderWindowControls.Note("Only what all of them have. Click a kind to edit it.");
        }
    }
}
