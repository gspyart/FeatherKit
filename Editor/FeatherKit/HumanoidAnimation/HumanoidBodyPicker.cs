using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Схема тела из кнопок: клик выделяет ручку, Shift или Ctrl — добавляет к выделению.
    /// Персонаж смотрит на тебя, поэтому его правая сторона — слева, как в зеркале.
    ///
    /// Кнопки рук и ног выбирают ручку того режима, который у конечности включён:
    /// «Кисть» в IK — кубик, в FK — кольцо. «Плечо» и «Бедро» есть только в FK.
    /// Рисуется и в инспекторе рига, и в панели окна сцены.
    /// </summary>
    internal static class HumanoidBodyPicker
    {
        private const float ButtonWidth = 76f;
        private const float ButtonHeight = 20f;
        private const float ColumnGap = 4f;

        private const string RotationTip = "Вращать (E). Уносит за собой всё, что выше по телу.";
        private const string FingersTip = "Вращать (E). Выделяет все фаланги пальцев этой руки.";
        private const string FKOnlyTip = "Есть только в режиме FK. Переключить — в блоке «Руки и ноги».";
        private const string MissingTip = "У этого персонажа нет такой кости.";

        private static readonly Color CenterTint = new Color(1f, 0.85f, 0.45f);
        private static readonly Color LeftTint = new Color(0.55f, 0.75f, 1f);
        private static readonly Color RightTint = new Color(1f, 0.6f, 0.6f);
        private static readonly Color SelectedTint = new Color(0.35f, 1f, 0.55f);

        // Три колонки: правая сторона персонажа, середина, левая. Пустая ячейка — null.
        private static readonly BodyPickerButton[][] Rows =
        {
            Row(null, Center(HumanBodyBones.Head, "Голова", "Вращать (E). Поворот головы."), null),
            Row(null, Center(HumanBodyBones.Neck, "Шея", RotationTip), null),
            Row(Clavicle(HumanBodyBones.RightShoulder), Center(HumanBodyBones.UpperChest, "Верх груди", RotationTip), Clavicle(HumanBodyBones.LeftShoulder)),
            Row(UpperArm(HumanoidLimb.RightArm, HumanBodyBones.RightUpperArm), Center(HumanBodyBones.Chest, "Грудь", RotationTip), UpperArm(HumanoidLimb.LeftArm, HumanBodyBones.LeftUpperArm)),
            Row(Elbow(HumanoidLimb.RightArm, HumanBodyBones.RightLowerArm), Center(HumanBodyBones.Spine, "Живот", RotationTip), Elbow(HumanoidLimb.LeftArm, HumanBodyBones.LeftLowerArm)),
            Row(Hand(HumanoidLimb.RightArm, HumanBodyBones.RightHand), BodyRoot(), Hand(HumanoidLimb.LeftArm, HumanBodyBones.LeftHand)),
            Row(Thigh(HumanoidLimb.RightLeg, HumanBodyBones.RightUpperLeg), null, Thigh(HumanoidLimb.LeftLeg, HumanBodyBones.LeftUpperLeg)),
            Row(Knee(HumanoidLimb.RightLeg, HumanBodyBones.RightLowerLeg), null, Knee(HumanoidLimb.LeftLeg, HumanBodyBones.LeftLowerLeg)),
            Row(Foot(HumanoidLimb.RightLeg, HumanBodyBones.RightFoot), null, Foot(HumanoidLimb.LeftLeg, HumanBodyBones.LeftFoot)),
        };

        private static readonly BodyPickerButton[] FingersRow =
        {
            BodyPickerButton.Fixed(new PickedControls(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightLittleDistal, HumanoidRigControlType.BoneRotation), "Пальцы", FingersTip),
            null,
            BodyPickerButton.Fixed(new PickedControls(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftLittleDistal, HumanoidRigControlType.BoneRotation), "Пальцы", FingersTip),
        };

        private static GUIStyle sideCaptionStyle;


        // ── Отрисовка ──────────────────────────────

        public static void Draw(HumanoidAnimationRig rig)
        {
            EnsureStyles();

            DrawRow(rig, null, "правая", "левая");

            foreach (var row in Rows)
                DrawRow(rig, row);

            if (rig.HasFingerControls)
            {
                GUILayout.Space(4f);
                DrawRow(rig, FingersRow);
            }
        }


        private static void DrawRow(HumanoidAnimationRig rig, BodyPickerButton[] row, string rightCaption = null, string leftCaption = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                for (var column = 0; column < 3; column++)
                {
                    if (column > 0)
                        GUILayout.Space(ColumnGap);

                    if (row == null)
                    {
                        var caption = column == 0 ? rightCaption : column == 2 ? leftCaption : null;
                        GUILayout.Label(caption ?? string.Empty, sideCaptionStyle, GUILayout.Width(ButtonWidth));
                    }
                    else if (row[column] == null)
                    {
                        GUILayout.Space(ButtonWidth);
                    }
                    else
                    {
                        DrawButton(rig, row[column], column);
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }


        private static void DrawButton(HumanoidAnimationRig rig, BodyPickerButton button, int column)
        {
            var target = button.Resolve(rig, out var tooltip);
            var handles = target.HasValue ? target.Value.FindHandles(rig) : new List<Transform>();

            if (target.HasValue && handles.Count == 0)
                tooltip = MissingTip;

            var isSelected = handles.Count > 0 && AreAllSelected(handles);

            var previousColor = GUI.backgroundColor;
            GUI.backgroundColor = isSelected ? SelectedTint : column == 0 ? RightTint : column == 2 ? LeftTint : CenterTint;

            using (new EditorGUI.DisabledScope(handles.Count == 0))
            {
                if (GUILayout.Button(new GUIContent(button.Label, tooltip), GUILayout.Width(ButtonWidth), GUILayout.Height(ButtonHeight)))
                    Select(handles, Event.current.shift || EditorGUI.actionKey);
            }

            GUI.backgroundColor = previousColor;
        }


        private static bool AreAllSelected(List<Transform> handles)
        {
            var selected = new HashSet<Transform>(Selection.transforms);

            foreach (var handle in handles)
            {
                if (!selected.Contains(handle))
                    return false;
            }

            return true;
        }


        // Shift или Ctrl — добавить к выделению или убрать, если уже выделены.
        private static void Select(List<Transform> handles, bool isAdditive)
        {
            var objects = new List<Object>();

            if (isAdditive)
            {
                objects.AddRange(Selection.objects);

                if (AreAllSelected(handles))
                {
                    foreach (var handle in handles)
                        objects.Remove(handle.gameObject);

                    Selection.objects = objects.ToArray();
                    return;
                }
            }

            foreach (var handle in handles)
            {
                if (!objects.Contains(handle.gameObject))
                    objects.Add(handle.gameObject);
            }

            Selection.objects = objects.ToArray();
        }


        private static void EnsureStyles()
        {
            if (sideCaptionStyle != null)
                return;

            sideCaptionStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        }


        // ── Описание схемы ──────────────────────────────

        private static BodyPickerButton[] Row(BodyPickerButton right, BodyPickerButton center, BodyPickerButton left) =>
            new[] { right, center, left };

        private static BodyPickerButton Center(HumanBodyBones bone, string label, string tooltip) =>
            BodyPickerButton.Fixed(new PickedControls(bone, HumanoidRigControlType.BoneRotation), label, tooltip);

        private static BodyPickerButton BodyRoot() =>
            BodyPickerButton.Fixed(new PickedControls(HumanBodyBones.Hips, HumanoidRigControlType.BodyRoot), "Таз",
                "Двигать (W) и вращать (E). Всё тело. Стопы и кисти в IK остаются на месте.");

        private static BodyPickerButton Clavicle(HumanBodyBones bone) =>
            BodyPickerButton.Fixed(new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Ключица",
                "Вращать (E). Поднять или свести плечо, рука идёт за ним.");

        private static BodyPickerButton UpperArm(HumanoidLimb limb, HumanBodyBones bone) =>
            BodyPickerButton.ForLimb(limb, "Плечо", null, FKOnlyTip,
                new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Вращать (E). Плечевой сустав: вся рука.");

        private static BodyPickerButton Elbow(HumanoidLimb limb, HumanBodyBones bone) =>
            BodyPickerButton.ForLimb(limb, "Локоть",
                new PickedControls(bone, HumanoidRigControlType.LimbBendHint), "Двигать (W). Куда смотрит локоть, когда рука согнута.",
                new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Вращать (E). Согнуть руку в локте.");

        private static BodyPickerButton Hand(HumanoidLimb limb, HumanBodyBones bone) =>
            BodyPickerButton.ForLimb(limb, "Кисть",
                new PickedControls(bone, HumanoidRigControlType.LimbTarget), "Двигать (W) и вращать (E). Тянешь кисть — рука сгибается сама.",
                new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Вращать (E). Поворот кисти.");

        private static BodyPickerButton Thigh(HumanoidLimb limb, HumanBodyBones bone) =>
            BodyPickerButton.ForLimb(limb, "Бедро", null, FKOnlyTip,
                new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Вращать (E). Тазобедренный сустав: вся нога.");

        private static BodyPickerButton Knee(HumanoidLimb limb, HumanBodyBones bone) =>
            BodyPickerButton.ForLimb(limb, "Колено",
                new PickedControls(bone, HumanoidRigControlType.LimbBendHint), "Двигать (W). Куда смотрит колено, когда нога согнута.",
                new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Вращать (E). Согнуть ногу в колене.");

        private static BodyPickerButton Foot(HumanoidLimb limb, HumanBodyBones bone) =>
            BodyPickerButton.ForLimb(limb, "Стопа",
                new PickedControls(bone, HumanoidRigControlType.LimbTarget), "Двигать (W) и вращать (E). Стопа стоит на месте, когда двигаешь тело.",
                new PickedControls(bone, HumanoidRigControlType.BoneRotation), "Вращать (E). Поворот стопы.");


        /// <summary>
        /// Какие ручки выбрать: одна кость или диапазон (все фаланги) и роль ручки.
        /// </summary>
        private readonly struct PickedControls
        {
            private readonly HumanBodyBones firstBone;
            private readonly HumanBodyBones lastBone;
            private readonly HumanoidRigControlType type;

            public PickedControls(HumanBodyBones bone, HumanoidRigControlType type) : this(bone, bone, type)
            {
            }

            public PickedControls(HumanBodyBones firstBone, HumanBodyBones lastBone, HumanoidRigControlType type)
            {
                this.firstBone = firstBone;
                this.lastBone = lastBone;
                this.type = type;
            }

            public List<Transform> FindHandles(HumanoidAnimationRig rig)
            {
                var handles = new List<Transform>();

                for (var bone = firstBone; bone <= lastBone; bone++)
                {
                    var control = rig.FindControl(bone, type);

                    if (control != null)
                        handles.Add(control.Handle);
                }

                return handles;
            }
        }


        /// <summary>
        /// Кнопка схемы. У корпуса ручка одна. У руки и ноги — своя для каждого режима:
        /// кнопка смотрит, какой режим включён у конечности. Ручки нет в этом режиме —
        /// кнопка серая с подсказкой почему.
        /// </summary>
        private sealed class BodyPickerButton
        {
            private readonly HumanoidLimb? limb;
            private readonly PickedControls? ikControls;
            private readonly string ikTooltip;
            private readonly PickedControls? fkControls;
            private readonly string fkTooltip;

            private BodyPickerButton(string label, HumanoidLimb? limb, PickedControls? ikControls, string ikTooltip, PickedControls? fkControls, string fkTooltip)
            {
                Label = label;
                this.limb = limb;
                this.ikControls = ikControls;
                this.ikTooltip = ikTooltip;
                this.fkControls = fkControls;
                this.fkTooltip = fkTooltip;
            }

            public static BodyPickerButton Fixed(PickedControls controls, string label, string tooltip) =>
                new BodyPickerButton(label, null, controls, tooltip, controls, tooltip);

            public static BodyPickerButton ForLimb(HumanoidLimb limb, string label, PickedControls? ikControls, string ikTooltip, PickedControls? fkControls, string fkTooltip) =>
                new BodyPickerButton(label, limb, ikControls, ikTooltip, fkControls, fkTooltip);

            public string Label { get; }


            /// <summary>Ручки для текущего режима конечности. Нет таких — null.</summary>
            public PickedControls? Resolve(HumanoidAnimationRig rig, out string tooltip)
            {
                // Риг старой сборки без FK — конечность всегда в IK.
                var rigLimb = limb.HasValue ? rig.FindLimb(limb.Value) : null;
                var isInIK = rigLimb == null || rigLimb.IsInIK;

                tooltip = isInIK ? ikTooltip : fkTooltip;
                return isInIK ? ikControls : fkControls;
            }
        }
    }
}
