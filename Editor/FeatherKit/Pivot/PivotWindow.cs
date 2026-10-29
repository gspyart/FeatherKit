using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Окно «Пивот»: переносит точку опоры выделенных объектов, не двигая их детей.
    /// Два способа — в центр детей одной кнопкой или в точку, заданную числами;
    /// заданная точка видна в окне сцены оранжевым шаром, пока работаешь с окном.
    /// Сам перенос — <see cref="PivotMover"/>; окно только решает, кого и куда.
    /// </summary>
    public class PivotWindow : EditorWindow
    {
        private const string RelativeToParentKey = "FeatherKit.Pivot.RelativeToParent";

        private static readonly Color ManualPointColor = new(1f, 0.55f, 0.1f);

        private static readonly GUIContent[] SpaceTitles =
        {
            new("Мир", "Координаты мира"),
            new("Родитель", "Как Position в инспекторе: относительно родителя объекта")
        };

        private Vector3 manualPoint;
        private bool isRelativeToParent;

        // Только верхние из выделенных: у ребёнка выделенного пивот переносить незачем — он внутри группы.
        private static Transform[] Targets => Selection.transforms;


        [MenuItem("FeatherKit/Pivot")]
        public static void Open()
        {
            var window = GetWindow<PivotWindow>();
            window.titleContent = new GUIContent("Pivot", EditorGUIUtility.IconContent("ToolHandleCenter").image);
            window.minSize = new Vector2(260f, 220f);
        }


        // ── Что выделено ─────────────────────────────────

        private static void DrawSelectionProblems()
        {
            if (Targets.Length == 0)
            {
                EditorGUILayout.HelpBox("Выдели объекты в сцене или иерархии.", MessageType.Info);
                return;
            }

            foreach (var target in Targets)
            {
                var reason = PivotMover.ExplainWhyCannotMovePivot(target);

                if (reason != null)
                    EditorGUILayout.HelpBox(reason, MessageType.Warning);
            }
        }


        // ── В центр детей ────────────────────────────────

        private static void DrawChildrenCenterSection()
        {
            EditorGUILayout.LabelField("В центр детей", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Центр", "Середина габаритов детей")))
                PivotMover.MoveToChildrenCenter(Targets, ChildrenCenterPoint.Center);

            if (GUILayout.Button(new GUIContent("Центр низа", "Середина нижней грани габаритов — объект будет стоять на пивоте")))
                PivotMover.MoveToChildrenCenter(Targets, ChildrenCenterPoint.BottomCenter);

            EditorGUILayout.EndHorizontal();
        }


        // ── В заданную точку ─────────────────────────────

        private void DrawManualSection()
        {
            EditorGUILayout.LabelField("В точку", EditorStyles.boldLabel);

            var chosenSpace = GUILayout.Toolbar(isRelativeToParent ? 1 : 0, SpaceTitles) == 1;

            if (chosenSpace != isRelativeToParent)
                SwitchSpace(chosenSpace);

            EditorGUI.BeginChangeCheck();
            manualPoint = EditorGUILayout.Vector3Field(GUIContent.none, manualPoint);

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Взять текущий", "Подставить пивот активного объекта — удобно поправить одну ось")))
                TakeCurrentPivot();

            if (GUILayout.Button(new GUIContent("Применить", "Перенести пивот выделенных в эту точку")))
                PivotMover.MovePivots(Targets, ManualPointInWorld, "Перенести пивот");

            EditorGUILayout.EndHorizontal();
        }

        // Точка в полях остаётся той же точкой мира — меняются только числа.
        private void SwitchSpace(bool relativeToParent)
        {
            var active = Selection.activeTransform;

            if (active != null)
            {
                var worldPoint = ManualPointInWorld(active);
                manualPoint = relativeToParent && active.parent != null ? active.parent.InverseTransformPoint(worldPoint) : worldPoint;
            }

            isRelativeToParent = relativeToParent;
            EditorPrefs.SetBool(RelativeToParentKey, isRelativeToParent);
            SceneView.RepaintAll();
        }

        private void TakeCurrentPivot()
        {
            var active = Selection.activeTransform;

            if (active == null)
                return;

            manualPoint = isRelativeToParent ? active.localPosition : active.position;
            GUI.FocusControl(null);
            SceneView.RepaintAll();
        }


        // ── Точка в окне сцены ───────────────────────────

        // Только пока работают с окном: иначе метка висит в сцене всё время, пока окно открыто.
        private void DrawManualPointInScene(SceneView sceneView)
        {
            if (focusedWindow != this && mouseOverWindow != this)
                return;

            Handles.color = ManualPointColor;

            foreach (var target in Targets)
            {
                var point = ManualPointInWorld(target);
                Handles.SphereHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * 0.12f, EventType.Repaint);
                Handles.DrawDottedLine(target.position, point, 3f);
            }
        }


        // ── Общие помощники ──────────────────────────────

        private Vector3 ManualPointInWorld(Transform target) =>
            isRelativeToParent && target.parent != null ? target.parent.TransformPoint(manualPoint) : manualPoint;


        // ── Unity-события ────────────────────────────────

        private void OnEnable()
        {
            isRelativeToParent = EditorPrefs.GetBool(RelativeToParentKey, false);
            SceneView.duringSceneGui += DrawManualPointInScene;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawManualPointInScene;
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            DrawSelectionProblems();
            EditorGUILayout.Space();
            DrawChildrenCenterSection();
            EditorGUILayout.Space();
            DrawManualSection();
        }

        private void OnFocus() => SceneView.RepaintAll();

        private void OnLostFocus() => SceneView.RepaintAll();

        private void OnSelectionChange() => Repaint();

        private void OnHierarchyChange() => Repaint();
    }
}
