using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Панель «Анимация гуманоида» в окне сцены — всё для анимации в одном месте, без окна
    /// Animation и инспектора. Появляется сама, когда выделено что-то внутри рига: сверху время
    /// и ключи, ниже кнопки позы, режимы FK/IK рук и ног и схема тела для выбора ручек.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Humanoid Animation", true)]
    internal sealed class HumanoidAnimationOverlay : IMGUIOverlay, ITransientOverlay
    {
        private const string OverlayId = "featherkit-humanoid-animation";
        private const string LimbsFoldoutKey = "FeatherKit.HumanoidAnimation.Overlay.Limbs";
        private const string PickerFoldoutKey = "FeatherKit.HumanoidAnimation.Overlay.BodyPicker";
        private const float PanelWidth = 280f;


        // ── Видимость ──────────────────────────────

        // ITransientOverlay
        public bool visible => HumanoidAnimationRigActions.FindSelectedRig() != null;


        // ── Отрисовка ──────────────────────────────

        public override void OnGUI()
        {
            var rig = HumanoidAnimationRigActions.FindSelectedRig();

            if (rig == null)
                return;

            HumanoidRigClipLibrary.RememberController(rig);
            HumanoidLimbModeSwitcher.SyncVisibility(rig);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PanelWidth)))
            {
                HumanoidTimePanel.Draw(rig);
                DrawSeparator();

                HumanoidPosePanel.Draw(rig);
                DrawSeparator();

                if (DrawFoldout(LimbsFoldoutKey, "Руки и ноги: FK или IK", true))
                    HumanoidLimbModePanel.Draw(rig, withBlendSliders: false);

                if (DrawFoldout(PickerFoldoutKey, "Схема тела", false))
                    HumanoidBodyPicker.Draw(rig);

                GUILayout.Space(4f);

                if (GUILayout.Button(new GUIContent("Риг в инспекторе", "Все анимации персонажа и настройки рига."), EditorStyles.miniButton))
                    Selection.activeGameObject = rig.gameObject;
            }
        }


        // Свёрнуто или нет — помнится между перезапусками редактора.
        private static bool DrawFoldout(string key, string title, bool expandedByDefault)
        {
            var expanded = EditorPrefs.GetBool(key, expandedByDefault);
            var newExpanded = EditorGUILayout.Foldout(expanded, title, true);

            if (newExpanded != expanded)
                EditorPrefs.SetBool(key, newExpanded);

            return newExpanded;
        }


        private static void DrawSeparator()
        {
            GUILayout.Space(4f);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true)), new Color(0.5f, 0.5f, 0.5f, 0.3f));
            GUILayout.Space(4f);
        }
    }
}
