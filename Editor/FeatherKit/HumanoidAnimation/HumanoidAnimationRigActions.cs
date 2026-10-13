using System;
using System.Collections.Generic;
using FeatherKit.HumanoidAnimation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.HumanoidAnimation
{
    /// <summary>
    /// Действия над ригом, общие для инспектора, панели в сцене и меню: найти выделенный риг
    /// и ручки, сбросить ручки, запечь, пересобрать, удалить; выйти из предпросмотра, когда
    /// действию нужна исходная поза. Каждое объясняет человеку, что не так, если выполнить
    /// его сейчас нельзя. Окном Animation управляет <see cref="HumanoidAnimationWindowRemote"/>.
    /// </summary>
    internal static class HumanoidAnimationRigActions
    {
        private const string DialogTitle = "Анимация гуманоида";


        // ── Найти риг ──────────────────────────────

        /// <summary>Риг, внутри которого лежит выделенный объект. Нет — null.</summary>
        public static HumanoidAnimationRig FindSelectedRig()
        {
            var selected = Selection.activeTransform;
            return selected != null ? selected.GetComponentInParent<HumanoidAnimationRig>() : null;
        }


        // ── Предпросмотр ──────────────────────────────

        /// <summary>
        /// Выйти из предпросмотра перед действием, которому нужна исходная поза. Спрашивает
        /// человека. Окно Animation выключает само, а Timeline — не может: тогда просит выключить там.
        /// </summary>
        public static bool TryLeavePreview(string actionDescription)
        {
            if (!HumanoidAnimationWindowRemote.IsAnyPreviewRunning)
                return true;

            if (!EditorUtility.DisplayDialog(DialogTitle, $"Сейчас включён предпросмотр анимации, а {actionDescription} нужно в исходной позе.\n\nВыключить предпросмотр и продолжить?", "Выключить и продолжить", "Отмена"))
                return false;

            HumanoidAnimationWindowRemote.StopAll();

            if (!HumanoidAnimationWindowRemote.IsAnyPreviewRunning)
                return true;

            ShowMessage("Предпросмотр всё ещё идёт — скорее всего, в окне Timeline. Выключите его там (кнопка Preview) и повторите.");
            return false;
        }


        /// <summary>Можно ли сейчас менять риг и клипы. В Play Mode — нет: всё сделанное пропадёт при выходе.</summary>
        public static bool CanChangeRigNow()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return true;

            ShowMessage("Во время игры риг не меняем и не запекаем: всё сделанное пропадёт при выходе из Play Mode.");
            return false;
        }


        // ── Ручки ──────────────────────────────

        /// <summary>Выделенные ручки рига, без костей и прочего.</summary>
        public static List<HumanoidRigControl> FindSelectedControls(HumanoidAnimationRig rig)
        {
            var selected = new List<HumanoidRigControl>();

            foreach (var control in rig.Controls)
            {
                if (control.Handle != null && Array.IndexOf(Selection.transforms, control.Handle) >= 0)
                    selected.Add(control);
            }

            return selected;
        }


        /// <summary>
        /// Вернуть ручки в позу, в которой их создали. При записи в Animation
        /// это ставит ключи, как обычное движение ручки.
        /// </summary>
        public static void ResetControls(IReadOnlyList<HumanoidRigControl> controls)
        {
            var handles = new List<Object>();

            foreach (var control in controls)
            {
                if (control.Handle != null)
                    handles.Add(control.Handle);
            }

            if (handles.Count == 0)
            {
                ShowMessage("Выделите ручки, которые нужно сбросить. Выбрать их удобно на схеме тела.");
                return;
            }

            Undo.RecordObjects(handles.ToArray(), "Сбросить ручки");

            foreach (var control in controls)
            {
                if (control.Handle == null)
                    continue;

                control.Handle.localPosition = control.RestLocalPosition;
                control.Handle.localRotation = control.RestLocalRotation;
            }
        }


        public static void SetControlsShown(HumanoidAnimationRig rig, bool shown)
        {
            Undo.RecordObject(rig, shown ? "Показать ручки" : "Скрыть ручки");
            rig.ControlsShown = shown;
            HumanoidLimbModeSwitcher.SyncVisibility(rig);
        }


        // ── Запекание ──────────────────────────────

        /// <summary>Запечь все клипы рига. Возвращает, сколько получилось.</summary>
        public static int BakeAll(HumanoidAnimationRig rig)
        {
            if (!CanChangeRigNow())
                return 0;

            var clips = HumanoidSourceClips.CollectFor(rig);

            if (clips.Count == 0)
            {
                ShowMessage("Клипов с ручками этого рига пока нет.\n\nСоздайте клип в окне Animation (кнопка Create) или запишите его в Timeline и подвигайте ручки.");
                return 0;
            }

            return BakeClips(rig, clips);
        }


        /// <summary>Запечь один клип. Клип не анимирует ручки рига — объяснить и ничего не делать.</summary>
        public static void BakeOne(HumanoidAnimationRig rig, AnimationClip clip)
        {
            if (!CanChangeRigNow())
                return;

            if (!HumanoidSourceClips.IsSourceFor(clip, rig))
            {
                ShowMessage($"В клипе «{clip.name}» нет кривых ручек этого рига.\n\nЗапекаются только клипы, где анимированы ручки из «{HumanoidAnimationRig.ControlsRootName}».");
                return;
            }

            BakeClips(rig, new List<AnimationClip> { clip });
        }


        private static int BakeClips(HumanoidAnimationRig rig, List<AnimationClip> clips)
        {
            var baked = 0;

            try
            {
                for (var i = 0; i < clips.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(DialogTitle, "Запекаю " + clips[i].name, (float)i / clips.Count);

                    try
                    {
                        HumanoidClipBaker.Bake(rig, clips[i]);
                        baked++;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        Debug.LogError($"Не получилось запечь «{clips[i].name}». Подробности — в ошибке выше.", clips[i]);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            return baked;
        }


        // ── Устройство рига ──────────────────────────────

        /// <summary>Пересоздать ручки. Во время предпросмотра нельзя: кости стоят не в исходной позе.</summary>
        public static void Rebuild(HumanoidAnimationRig rig, bool withFingers)
        {
            if (!CanChangeRigNow() || !TryLeavePreview("пересоздавать ручки"))
                return;

            if (PrefabUtility.IsPartOfPrefabInstance(rig))
            {
                ShowMessage("Рабочая копия стала префабом — внутри префаба объекты не пересоздать.\n\nПКМ по копии → Prefab → Unpack Completely, потом повторите.");
                return;
            }

            HumanoidRigControlsBuilder.Rebuild(rig, withFingers);
        }


        public static void DeleteRig(HumanoidAnimationRig rig)
        {
            if (!CanChangeRigNow())
                return;

            if (!EditorUtility.DisplayDialog(DialogTitle, $"Удалить «{rig.name}» из сцены?\n\nКлипы останутся в проекте. Ctrl+Z вернёт копию.", "Удалить", "Отмена"))
                return;

            Undo.DestroyObjectImmediate(rig.gameObject);
        }


        public static void ShowMessage(string message) =>
            EditorUtility.DisplayDialog(DialogTitle, message, "Понятно");
    }
}
