using FeatherKit.HumanoidAnimation;
using UnityEditor;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Сам выбирает инструмент под выделенную ручку: кольцо — вращать, кубик, шарик и таз —
    /// двигать. Не надо помнить, где W, а где E.
    ///
    /// Срабатывает только в момент выделения: нажал E на кубике, чтобы повернуть кисть, —
    /// инструмент твой до следующего выделения. Чужие инструменты (не перемещение, поворот,
    /// масштаб и их сборки) не трогает.
    /// </summary>
    [InitializeOnLoad]
    internal static class HumanoidControlToolSwitcher
    {
        static HumanoidControlToolSwitcher()
        {
            Selection.selectionChanged += SwitchToolForSelection;
        }


        // ── Выбор инструмента ──────────────────────────────

        // Все выделенные ручки только вращаются — поворот; все двигаются — перемещение.
        // Выделены разные — не угадываем.
        private static void SwitchToolForSelection()
        {
            if (!IsStandardTransformTool(Tools.current))
                return;

            var rig = HumanoidAnimationRigActions.FindSelectedRig();

            if (rig == null)
                return;

            var controls = HumanoidAnimationRigActions.FindSelectedControls(rig);

            if (controls.Count == 0)
                return;

            var rotationOnlyCount = 0;

            foreach (var control in controls)
            {
                if (control.Type == HumanoidRigControlType.BoneRotation)
                    rotationOnlyCount++;
            }

            if (rotationOnlyCount == controls.Count)
                Tools.current = Tool.Rotate;
            else if (rotationOnlyCount == 0)
                Tools.current = Tool.Move;
        }


        private static bool IsStandardTransformTool(Tool tool) =>
            tool == Tool.Move || tool == Tool.Rotate || tool == Tool.Scale || tool == Tool.Rect || tool == Tool.Transform;
    }
}
