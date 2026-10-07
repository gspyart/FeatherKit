using System;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Нижняя часть окна материалов: настройка выбранного — обычный инспектор Unity плюс шапка
    /// с превью, откуда материал и сколько у него носителей.
    ///
    /// Шапка заведена ради одного предупреждения: правка общего материала из проекта уезжает
    /// всем, кто его носит, — и в других сценах тоже, — а в инспекторе этого никак не видно.
    /// </summary>
    public class InspectedMaterialPanel
    {
        private const float HeaderHeight = 56f;
        private const float PreviewSize = 48f;
        private const float ButtonWidth = 76f;
        private const float ScrollbarWidth = 14f;

        private readonly SceneMaterialUsage usage;
        private readonly MaterialPreviewCache previews;
        private readonly Action requestRefresh;
        private readonly Action requestRepaint;

        private MaterialEditor materialEditor;
        private Vector2 scroll;
        private float contentHeight;


        public InspectedMaterialPanel(SceneMaterialUsage usage, MaterialPreviewCache previews, Action requestRefresh, Action requestRepaint)
        {
            this.usage = usage;
            this.previews = previews;
            this.requestRefresh = requestRefresh;
            this.requestRepaint = requestRepaint;
        }


        public Material Material { get; private set; }


        // ── Выбор ────────────────────────────────────────

        public void Show(Material material)
        {
            if (Material == material)
                return;

            Clear();

            Material = material;
            scroll = Vector2.zero;
            contentHeight = 0f;

            if (material == null)
                return;

            // Свёрнутый в инспекторе материал свёрнут и здесь: Unity помнит это на самом объекте,
            // а окно открывают ради свойств.
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(material, true);
            materialEditor = (MaterialEditor)UnityEditor.Editor.CreateEditor(material, typeof(MaterialEditor));
        }


        /// <summary>Забывает материал и сносит его инспектор.</summary>
        public void Clear()
        {
            if (materialEditor != null)
                UnityEngine.Object.DestroyImmediate(materialEditor);

            materialEditor = null;
            Material = null;
        }


        // ── Отрисовка ────────────────────────────────────

        /// <summary>
        /// Рисует панель в отведённом прямоугольнике. Своим прямоугольником, а не общей
        /// раскладкой: инспектор материала просит высоту по числу свойств, и в общей раскладке
        /// раздувал бы прокрутку до полной длины вместо того, чтобы прокручиваться.
        /// </summary>
        public void Draw(Rect area)
        {
            // Копию могли откатить через Undo: материал пропал, а инспектор остался бы на пустом месте.
            if (Material == null)
            {
                if (materialEditor != null)
                    Clear();

                GUI.Label(area, "Нажми на материал выше — настрою его здесь.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var header = new Rect(area.x, area.y, area.width, HeaderHeight);
            DrawHeader(header);

            var bodyTop = header.yMax + DrawWarning(new Rect(area.x, header.yMax, area.width, 0f));
            var body = new Rect(area.x, bodyTop, area.width, Mathf.Max(0f, area.yMax - bodyTop));

            DrawInspector(body);
        }

        private void DrawHeader(Rect header)
        {
            var content = new Rect(header.x + MaterialRowGUI.Gap, header.y + MaterialRowGUI.Gap, header.width - MaterialRowGUI.Gap * 2f, PreviewSize);

            var previewRect = MaterialRowGUI.CutLeft(ref content, PreviewSize);

            if (Event.current.type == EventType.Repaint)
            {
                var preview = previews.GetPreview(Material);

                if (preview != null)
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
            }

            var buttons = MaterialRowGUI.CutRight(ref content, ButtonWidth);

            DrawName(new Rect(content.x, content.y + 2f, content.width, 18f));
            EditorGUI.LabelField(new Rect(content.x, content.y + 24f, content.width, 16f), Describe(), EditorStyles.miniLabel);

            DrawButtons(buttons);
        }

        // Сценовую копию больше нигде не переименовать: в окне проекта её нет.
        private void DrawName(Rect rect)
        {
            if (SceneMaterialTools.GetOrigin(Material) != MaterialOrigin.Scene)
            {
                EditorGUI.LabelField(rect, Material.name, EditorStyles.boldLabel);
                return;
            }

            var name = EditorGUI.DelayedTextField(rect, Material.name);

            if (name == Material.name || string.IsNullOrWhiteSpace(name))
                return;

            Undo.RecordObject(Material, "Переименовать материал");
            Material.name = name;
            requestRefresh();
        }

        private string Describe()
        {
            var carriers = $"{MaterialRowGUI.CountSlots(usage.CountSlots(Material))} · {MaterialRowGUI.CountObjects(usage.CountOwners(Material))}";

            return SceneMaterialTools.GetOrigin(Material) switch
            {
                MaterialOrigin.Scene => $"Сценовый · {carriers}",
                MaterialOrigin.Project => $"Из проекта · в открытых сценах {carriers}",
                _ => $"Встроенный · в открытых сценах {carriers}"
            };
        }

        private void DrawButtons(Rect rect)
        {
            var top = new Rect(rect.x, rect.y + 2f, rect.width, 18f);
            var bottom = new Rect(rect.x, rect.y + 24f, rect.width, 18f);

            if (GUI.Button(top, "Носители", EditorStyles.miniButton))
                usage.SelectOwners(Material);

            if (SceneMaterialTools.GetOrigin(Material) == MaterialOrigin.Scene)
            {
                if (!GUI.Button(bottom, "В проект…", EditorStyles.miniButton))
                    return;

                var asset = SceneMaterialTools.SaveToProject(Material);

                if (asset != null)
                    Show(asset);

                requestRefresh();
                GUIUtility.ExitGUI();
            }
            else if (GUI.Button(bottom, "В проекте", EditorStyles.miniButton))
            {
                EditorGUIUtility.PingObject(Material);
            }
        }

        /// <summary>Предупреждение под шапкой. Возвращает, сколько высоты оно заняло.</summary>
        private float DrawWarning(Rect line)
        {
            var origin = SceneMaterialTools.GetOrigin(Material);

            if (origin != MaterialOrigin.Project && origin != MaterialOrigin.BuiltIn)
                return 0f;

            var text = origin == MaterialOrigin.Project
                ? "Общий материал из проекта: правка уедет всем, кто его носит, — и в других сценах. Своя копия — на вкладке «Выделение»."
                : "Встроенный материал: Unity не даст его править. Своя копия — на вкладке «Выделение».";

            var type = origin == MaterialOrigin.Project ? MessageType.Warning : MessageType.Info;
            var width = line.width - MaterialRowGUI.Gap * 2f;
            var height = Mathf.Max(EditorStyles.helpBox.CalcHeight(new GUIContent(text, EditorGUIUtility.IconContent("console.warnicon").image), width), 38f);

            EditorGUI.HelpBox(new Rect(line.x + MaterialRowGUI.Gap, line.y, width, height), text, type);
            return height + MaterialRowGUI.Gap;
        }

        // Прокрутка своими прямоугольниками: раскладочная прокрутка Unity вокруг инспектора
        // материала то ужималась до строки, то раздувалась на всю длину. Высоту содержимого
        // меряем сами на каждой перерисовке и прокручиваем уже её.
        //
        // Свойства — в своей области под полем шейдера: инспектор тун-шейдера раскладывает себя
        // от верха области и иначе наезжал бы на всё, что нарисовано над ним.
        private void DrawInspector(Rect body)
        {
            var hasScrollbar = contentHeight > body.height;
            var view = new Rect(0f, 0f, body.width - (hasScrollbar ? ScrollbarWidth : 0f), Mathf.Max(contentHeight, body.height));

            scroll = GUI.BeginScrollView(body, scroll, view);

            var shaderRect = new Rect(view.x + MaterialRowGUI.Gap, view.y + 2f, view.width - MaterialRowGUI.Gap * 2f, EditorGUIUtility.singleLineHeight);
            DrawShaderField(shaderRect);

            var propertiesTop = shaderRect.yMax + MaterialRowGUI.Gap;
            GUILayout.BeginArea(new Rect(view.x, propertiesTop, view.width, view.height - propertiesTop));

            materialEditor.OnInspectorGUI();
            var end = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));

            GUILayout.EndArea();
            GUI.EndScrollView();

            var measured = propertiesTop + end.yMax;

            if (Event.current.type != EventType.Repaint || Mathf.Abs(measured - contentHeight) < 1f)
                return;

            contentHeight = measured;
            requestRepaint();
        }

        // Шейдер своим полем, а не шапкой инспектора Unity: та рисуется мимо раскладки
        // и наезжала на свойства под собой. Без выбора шейдера материал не перевести на другой.
        private void DrawShaderField(Rect rect)
        {
            using (new EditorGUI.DisabledScope(SceneMaterialTools.GetOrigin(Material) == MaterialOrigin.BuiltIn))
            {
                var shader = (Shader)EditorGUI.ObjectField(rect, "Шейдер", Material.shader, typeof(Shader), false);

                // Через инспектор, а не присваиванием: так шейдер получит своё «переехал на меня»
                // и чужие ключевые слова вычистятся, а шаг ляжет в Undo.
                if (shader != null && shader != Material.shader)
                    materialEditor.SetShader(shader);
            }
        }
    }
}
