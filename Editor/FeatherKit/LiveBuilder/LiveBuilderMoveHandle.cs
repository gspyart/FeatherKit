using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Стрелки переноса выделенных построек — как штатный перенос Unity (W), но не выходя из Live Builder: коробка, комната, лестница,
    /// стена по линии, здание, объект из коробок. Стрелки — в центре рамки активного выделенного (<see cref="LiveBuilderSelectionInfo"/>);
    /// тянешь — едут все выделенные, что правит инструмент, на целое число клеток сетки от исходного места, в осях стрелок.
    /// Оси — как переключатель Global / Local в Unity; у коробки, комнаты и лестницы — всегда оси их объекта: они не поворачиваются.
    /// Shift в начале — копия, как Ctrl+D (связь с префабом, имя, место в иерархии сразу за оригиналом): уезжает копия, оригинал стоит.
    /// Копия появляется на первом целом шаге сетки — с оригиналом она не совпадает ни на миг. Весь жест — один Ctrl+Z.
    /// У выреза свои стрелки (<see cref="GeometryCutterHandles"/>): копию Shift'ом они делают через эти же шаги.
    /// </summary>
    public static class LiveBuilderMoveHandle
    {
        // Что едет и где оно было в начале: сдвиг считается от начала, без накопления округлений.
        private static readonly List<Transform> Targets = new();
        private static readonly List<Vector3> StartPositions = new();

        private static bool isDragging;

        // Тянут стрелки выделенного (а не стрелки выреза, которые тоже ведут перенос копии здесь).
        private static bool isSelectionDrag;

        private static bool isCopying;
        private static bool isCopyMade;
        private static int undoGroup;
        private static Vector3 dragStartPosition;
        private static Quaternion dragRotation;

        // Сдвиг по сетке в осях стрелок — на столько уже уехало.
        private static Vector3 snappedOffset;


        // Где стрелки, пока тянешь: начало плюс уже сделанный сдвиг по сетке — стрелки идут вместе с постройкой, а не за мышью.
        public static Vector3 HandlePosition => dragStartPosition + dragRotation * snappedOffset;


        // ── Стрелки выделенного ──────────────────────────────────────────

        // Стрелки у выделенного, что правит инструмент; нечего двигать — их нет.
        public static void Draw(LiveBuilderTool tool)
        {
            EndDragIfReleased();

            // Тянут стрелки выреза — свои стрелки не рисуем: две ручки в одном месте спорили бы за мышь.
            // Выбраны точки контура — у них свои стрелки (<see cref="LiveBuilderContourMoveHandle"/>), стрелки всего объекта спрятаны.
            if ((isDragging && !isSelectionDrag) || (!isDragging && LiveBuilderContourSelection.Knots.Count > 0))
                return;

            var targets = isDragging ? new List<Transform>(Targets) : MovableSelection(tool);

            if (targets.Count == 0)
                return;

            var active = ActiveTarget(targets);
            var position = isDragging ? HandlePosition : CenterOf(active);
            var rotation = isDragging ? dragRotation : AxesOf(active);

            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(position, rotation);

            if (EditorGUI.EndChangeCheck())
            {
                if (!isDragging)
                {
                    BeginDrag(targets, position, rotation, Event.current.shift);
                    isSelectionDrag = true;
                }

                DragTo(moved);
            }

            DrawDragLabel();
        }

        // Выделенные постройки, что правит инструмент (без инструмента — все); часть здания — само здание. Лежащее внутри другой выделенной — едет с ней,
        // отдельно не двигается. Активная — первой: по её осям стрелки и точный сдвиг (<see cref="LiveBuilderExactMove"/>).
        public static List<Transform> MovableSelection(LiveBuilderTool tool)
        {
            var targets = new List<Transform>();
            var active = Selection.activeGameObject != null ? MovableOf(Selection.activeGameObject) : null;

            if (active != null && (tool == null || tool.CanEdit(active.gameObject)))
                targets.Add(active);

            foreach (var selected in Selection.gameObjects)
            {
                var target = MovableOf(selected);

                if (target != null && (tool == null || tool.CanEdit(target.gameObject)) && !targets.Contains(target))
                    targets.Add(target);
            }

            targets.RemoveAll(target => HasAncestorIn(target, targets));
            return targets;
        }

        // Постройка, которую стрелки двигают целиком. Вырез — своими стрелками, повтор — своей ручкой; прочее — штатным переносом.
        public static Transform MovableOf(GameObject selected)
        {
            if (selected.TryGetComponent<SplineBuildingPart>(out _))
            {
                var building = selected.GetComponentInParent<SplineBuilding>();
                return building != null ? building.transform : null;
            }

            var isMovable = selected.TryGetComponent<BoxModelPart>(out _) || selected.TryGetComponent<BoxModelRoom>(out _) || selected.TryGetComponent<BoxModelStairs>(out _)
                            || selected.TryGetComponent<SplineWall>(out _) || selected.TryGetComponent<SplineBuilding>(out _) || selected.TryGetComponent<BoxModel>(out _);

            return isMovable ? selected.transform : null;
        }

        private static bool HasAncestorIn(Transform target, List<Transform> others)
        {
            for (var parent = target.parent; parent != null; parent = parent.parent)
                if (others.Contains(parent))
                    return true;

            return false;
        }

        // Активное выделенное, если оно среди двигаемых, иначе первое из них: у него стрелки и оси.
        private static Transform ActiveTarget(List<Transform> targets)
        {
            var active = Selection.activeGameObject != null ? MovableOf(Selection.activeGameObject) : null;
            return active != null && targets.Contains(active) ? active : targets[0];
        }

        // Середина рамки постройки; размера у неё нет — её точка.
        private static Vector3 CenterOf(Transform target)
        {
            return LiveBuilderSelectionInfo.TryDescribe(target.gameObject, out var info) ? info.Frame.MultiplyPoint3x4(info.LocalBounds.center) : target.position;
        }

        // Коробка, комната, лестница — оси их объекта; прочее — как Global / Local в Unity.
        public static Quaternion AxesOf(Transform target)
        {
            if (target.TryGetComponent<BoxModelPart>(out _) || target.TryGetComponent<BoxModelRoom>(out _) || target.TryGetComponent<BoxModelStairs>(out _))
            {
                var model = target.GetComponentInParent<BoxModel>();
                return model != null ? model.transform.rotation : Quaternion.identity;
            }

            return Tools.pivotRotation == PivotRotation.Local ? target.rotation : Quaternion.identity;
        }


        // ── Перенос ──────────────────────────────────────────────────────

        // Тянут ли сейчас стрелки этого объекта — у выреза свои стрелки, но перенос копии ведёт этот класс.
        public static bool IsDragging(Transform target)
        {
            EndDragIfReleased();
            return isDragging && Targets.Contains(target);
        }


        // Начать перенос этих построек от точки стрелок; isCopy — уедут их копии. Один шаг Ctrl+Z на весь жест.
        public static void BeginDrag(IReadOnlyList<Transform> targets, Vector3 handleStart, Quaternion axes, bool isCopy)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(isCopy ? "Copy Live Builder Objects" : "Move Live Builder Objects");
            undoGroup = Undo.GetCurrentGroup();

            isDragging = true;
            isSelectionDrag = false;
            isCopying = isCopy;
            isCopyMade = false;
            dragStartPosition = handleStart;
            dragRotation = axes;
            snappedOffset = Vector3.zero;
            RememberTargets(targets);
        }

        private static void RememberTargets(IEnumerable<Transform> targets)
        {
            Targets.Clear();
            StartPositions.Clear();

            foreach (var target in targets)
            {
                Targets.Add(target);
                StartPositions.Add(target.position);
            }
        }


        // Стрелки ушли сюда: сдвиг — целыми клетками сетки в осях стрелок. Первый целый шаг копии — сначала сама копия.
        public static void DragTo(Vector3 handle)
        {
            if (!isDragging)
                return;

            var offset = LiveBuilderGrid.Snap(Quaternion.Inverse(dragRotation) * (handle - dragStartPosition));

            if (offset == snappedOffset)
                return;

            snappedOffset = offset;

            if (isCopying && !isCopyMade)
                MakeCopies();

            var worldOffset = dragRotation * snappedOffset;
            Undo.RecordObjects(Targets.ToArray(), isCopying ? "Copy Live Builder Objects" : "Move Live Builder Objects");

            for (var index = 0; index < Targets.Count; index++)
                if (Targets[index] != null)
                    Targets[index].position = StartPositions[index] + worldOffset;
        }

        // Копии — как Ctrl+D: каждая сразу за своим оригиналом, выделены они; дальше едут они, оригиналы стоят на месте.
        private static void MakeCopies()
        {
            Selection.objects = Targets.ConvertAll(target => (Object)target.gameObject).ToArray();
            Unsupported.DuplicateGameObjectsUsingPasteboard();

            var copies = new List<Transform>();

            foreach (var copy in Selection.gameObjects)
                copies.Add(copy.transform);

            RememberTargets(copies);
            isCopyMade = true;
        }


        // Отпустили — весь жест одним шагом Ctrl+Z; следующий начнётся от того, где постройки сейчас.
        public static void EndDragIfReleased()
        {
            if (isDragging && GUIUtility.hotControl == 0)
                EndDrag();
        }

        public static void EndDrag()
        {
            if (!isDragging)
                return;

            Undo.CollapseUndoOperations(undoGroup);
            isDragging = false;
            Targets.Clear();
            StartPositions.Clear();
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // У стрелок, пока тянешь: «X +2 · Z −1 м», у копии — «копия · …»; копия ещё не сделана — подсказка тянуть дальше.
        public static void DrawDragLabel()
        {
            if (!isDragging || Event.current.type != EventType.Repaint)
                return;

            var offsetText = LiveBuilderSceneText.FormatOffset(snappedOffset) ?? (isCopying ? "тяни дальше — копия встанет на клетку сетки" : "0 м");
            LiveBuilderSceneText.DrawLabel(HandlePosition, isCopying ? "копия · " + offsetText : offsetText);
        }
    }
}
