using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Кнопки позы в панели «Анимация гуманоида»: скопировать позу, вставить как есть или
    /// зеркально, посмотреть глазами персонажа, вернуть выделенные ручки в исходную позу.
    /// </summary>
    internal static class HumanoidPosePanel
    {
        public static void Draw(HumanoidAnimationRig rig)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Копировать", "Запомнить позу тела в этом кадре."), EditorStyles.miniButtonLeft))
                    HumanoidPoseClipboard.Copy(rig);

                using (new EditorGUI.DisabledScope(!HumanoidPoseClipboard.HasPose))
                {
                    if (GUILayout.Button(new GUIContent("Вставить", "Поставить тело в запомненную позу. При записи — ключ позы."), EditorStyles.miniButtonMid))
                        HumanoidPoseClipboard.Paste(rig, mirrored: false);

                    if (GUILayout.Button(new GUIContent("Зеркально", "Запомненная поза, левое на месте правого: шаг левой → шаг правой."), EditorStyles.miniButtonRight))
                        HumanoidPoseClipboard.Paste(rig, mirrored: true);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var eyesContent = HumanoidEyesView.IsLookingThroughEyes
                    ? new GUIContent("Вернуть камеру", "Камера сцены встанет, где была.")
                    : new GUIContent("Глазами", "Камера сцены встанет в глаза персонажа: видно, как поза выглядит от первого лица.");

                if (GUILayout.Button(eyesContent, EditorStyles.miniButtonLeft))
                    HumanoidEyesView.Toggle(rig);

                if (GUILayout.Button(new GUIContent("Сбросить выделенные", "Вернуть выделенные ручки в исходную позу. При записи поставит ключи."), EditorStyles.miniButtonRight))
                {
                    HumanoidAnimationRigActions.ResetControls(HumanoidAnimationRigActions.FindSelectedControls(rig));
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
