using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Блок «Руки и ноги»: у каждой конечности две кнопки FK | IK, нажатая — текущий режим.
    /// В инспекторе под ними ещё ползунок смеси — для плавного перехода между режимами.
    /// Рисуется и в инспекторе рига, и в панели окна сцены.
    /// </summary>
    internal static class HumanoidLimbModePanel
    {
        private const float LimbLabelWidth = 84f;
        private const float ModeButtonWidth = 40f;

        private const string FKTip = "FK: вращаешь суставы по очереди — плечо, локоть, кисть. Для маха руками, замахов, дуг.";
        private const string IKTip = "IK: тянешь кисть или стопу, сустав сгибается сам. Для хвата за опору и стоп на полу.";
        private const string MatchNote = "Ручки встанут в текущую позу. Чтобы переключение записалось, включите запись.";

        private static readonly Color ActiveModeTint = new Color(0.35f, 0.75f, 1f);

        private static readonly (HumanoidLimb limb, string label)[] LimbLabels =
        {
            (HumanoidLimb.RightArm, "Правая рука"),
            (HumanoidLimb.LeftArm, "Левая рука"),
            (HumanoidLimb.RightLeg, "Правая нога"),
            (HumanoidLimb.LeftLeg, "Левая нога"),
        };


        // ── Отрисовка ──────────────────────────────

        /// <summary>Нарисовать блок. Риг старой сборки без FK — подсказать пересоздать ручки.</summary>
        public static void Draw(HumanoidAnimationRig rig, bool withBlendSliders)
        {
            if (rig.Limbs.Count == 0)
            {
                EditorGUILayout.HelpBox("Риг собран старой версией тулы: у рук и ног только IK. Пересоздайте ручки в «Настройке рига» — появится FK, анимации сохранятся.", MessageType.Info);
                return;
            }

            foreach (var (limb, label) in LimbLabels)
            {
                var rigLimb = rig.FindLimb(limb);

                if (rigLimb == null)
                    continue;

                DrawModeButtons(rig, rigLimb, label);

                if (withBlendSliders)
                    DrawBlendSlider(rig, rigLimb);
            }
        }


        private static void DrawModeButtons(HumanoidAnimationRig rig, HumanoidRigLimb limb, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(LimbLabelWidth));
                GUILayout.FlexibleSpace();

                var wantsFK = DrawModeButton("FK", FKTip, !limb.IsInIK, EditorStyles.miniButtonLeft) && limb.IsInIK;
                var wantsIK = DrawModeButton("IK", IKTip, limb.IsInIK, EditorStyles.miniButtonRight) && !limb.IsInIK;

                if ((wantsFK || wantsIK) && HumanoidAnimationRigActions.CanChangeRigNow())
                    HumanoidLimbModeSwitcher.Switch(rig, limb, toIK: wantsIK);
            }
        }

        private static bool DrawModeButton(string text, string tooltip, bool isActive, GUIStyle style)
        {
            var previousColor = GUI.backgroundColor;

            if (isActive)
                GUI.backgroundColor = ActiveModeTint;

            var pressed = GUILayout.Button(new GUIContent(text, tooltip + "\n\n" + MatchNote), style, GUILayout.Width(ModeButtonWidth));

            GUI.backgroundColor = previousColor;
            return pressed;
        }


        private static void DrawBlendSlider(HumanoidAnimationRig rig, HumanoidRigLimb limb)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(LimbLabelWidth);

                var content = new GUIContent("FK ⟷ IK", "Смесь режимов без совмещения поз. Ключи на ней делают плавный переход за несколько кадров.");
                var weight = EditorGUILayout.Slider(content, limb.IKWeight, 0f, 1f);

                if (!Mathf.Approximately(weight, limb.IKWeight))
                {
                    HumanoidLimbModeSwitcher.SetIKWeight(limb, weight);
                    HumanoidLimbModeSwitcher.SyncVisibility(rig);
                }
            }
        }
    }
}
