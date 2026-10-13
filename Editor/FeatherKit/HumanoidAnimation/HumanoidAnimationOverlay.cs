using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Панель «Анимация гуманоида» в окне сцены. Появляется сама, когда выделено что-то
    /// внутри рига: схема тела для выбора ручек, режимы FK/IK рук и ног, состояние
    /// предпросмотра, сброс и запекание.
    /// Нужна, чтобы не возвращаться в инспектор рига каждый раз, когда выделил ручку.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Анимация гуманоида", true)]
    internal sealed class HumanoidAnimationOverlay : IMGUIOverlay, ITransientOverlay
    {
        private const string OverlayId = "featherkit-humanoid-animation";
        private const float PanelWidth = 260f;

        private static readonly Color PreviewOnColor = new Color(0.35f, 0.85f, 0.45f);
        private static readonly Color PreviewOffColor = new Color(1f, 0.7f, 0.3f);

        private GUIStyle statusStyle;


        // ── Видимость ──────────────────────────────

        // ITransientOverlay
        public bool visible => HumanoidAnimationRigActions.FindSelectedRig() != null;


        // ── Отрисовка ──────────────────────────────

        public override void OnGUI()
        {
            var rig = HumanoidAnimationRigActions.FindSelectedRig();

            if (rig == null)
                return;

            EnsureStyles();
            HumanoidRigClipLibrary.RememberController(rig);
            HumanoidLimbModeSwitcher.SyncVisibility(rig);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PanelWidth)))
            {
                DrawPreviewStatus(rig);
                GUILayout.Space(6f);

                HumanoidBodyPicker.Draw(rig);
                GUILayout.Space(6f);

                HumanoidLimbModePanel.Draw(rig, withBlendSliders: false);
                GUILayout.Space(6f);

                DrawActions(rig);
            }
        }


        private void DrawPreviewStatus(HumanoidAnimationRig rig)
        {
            var isPreviewing = HumanoidAnimationRigActions.IsPreviewing;

            using (new EditorGUILayout.HorizontalScope())
            {
                statusStyle.normal.textColor = isPreviewing ? PreviewOnColor : PreviewOffColor;

                var status = isPreviewing
                    ? new GUIContent("● Предпросмотр включён", "Ручки двигают тело. Включите запись в окне Animation, чтобы ставить ключи.")
                    : new GUIContent("● Предпросмотр выключен", "Без предпросмотра ручки двигаются, но тело стоит на месте.");

                GUILayout.Label(status, statusStyle);
                GUILayout.FlexibleSpace();

                if (!isPreviewing && GUILayout.Button(new GUIContent("Включить", "Включить предпросмотр в окне Animation."), EditorStyles.miniButton))
                {
                    HumanoidAnimationRigActions.StartPreview(rig);
                    GUIUtility.ExitGUI();
                }
            }
        }


        private static void DrawActions(HumanoidAnimationRig rig)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Сбросить выделенные", "Вернуть выделенные ручки в исходную позу. При записи поставит ключи."), EditorStyles.miniButtonLeft))
                {
                    HumanoidAnimationRigActions.ResetControls(HumanoidAnimationRigActions.FindSelectedControls(rig));
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Запечь клипы", "Собрать Humanoid-версии всех клипов рига прямо сейчас. Обычно это делает Ctrl+S."), EditorStyles.miniButtonRight))
                {
                    HumanoidAnimationRigActions.BakeAll(rig);
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button(new GUIContent("Открыть риг в инспекторе", "Все настройки и список клипов."), EditorStyles.miniButton))
                Selection.activeGameObject = rig.gameObject;
        }


        private void EnsureStyles()
        {
            if (statusStyle != null)
                return;

            statusStyle = new GUIStyle(EditorStyles.miniBoldLabel);
        }
    }
}
