using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Точный перенос и копии выделенных построек на заданный сдвиг, как перенос с числом и «×5» в SketchUp: сдвинуть на X, Y, Z метров
    /// или сделать N копий, каждая следующая — на тот же сдвиг дальше. Что двигается и в каких осях — как у стрелок (<see cref="LiveBuilderMoveHandle"/>):
    /// у коробки, комнаты и лестницы — оси их объекта. Копия — как Ctrl+D: связь с префабом, место в иерархии сразу за оригиналом.
    /// Всё действие — один Ctrl+Z. Числа набирают в карточке «Перенос и копии» (<see cref="LiveBuilderMoveCard"/>), помнятся до перезагрузки скриптов.
    /// </summary>
    public static class LiveBuilderExactMove
    {
        // Сколько копий за раз — больше ряд уже делают повтором.
        public const int MaxCopyCount = 50;


        public static Vector3 Offset { get; set; } = new(4f, 0f, 0f);

        public static int CopyCount { get; set; } = 1;


        // ── Что двигается ────────────────────────────────────────────────

        // Выделенные постройки, которые двигают стрелки, — при любом инструменте; активная — первой: по её осям сдвиг.
        public static List<Transform> MovableSelection()
        {
            return LiveBuilderMoveHandle.MovableSelection(null);
        }


        // ── Перенос и копии ──────────────────────────────────────────────

        // Сдвинуть постройки на offset в осях первой из них.
        public static void Move(IReadOnlyList<Transform> targets, Vector3 offset)
        {
            if (targets.Count == 0)
                return;

            var worldOffset = LiveBuilderMoveHandle.AxesOf(targets[0]) * offset;
            var transforms = new List<Object>(targets);
            Undo.RecordObjects(transforms.ToArray(), "Move Live Builder Objects");

            foreach (var target in targets)
                target.position += worldOffset;
        }


        // count копий построек: первая — на offset, каждая следующая — ещё на offset дальше. Выделены копии.
        public static void Copy(IReadOnlyList<Transform> targets, Vector3 offset, int count)
        {
            if (targets.Count == 0 || count < 1)
                return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Copy Live Builder Objects");
            var undoGroup = Undo.GetCurrentGroup();

            var worldOffset = LiveBuilderMoveHandle.AxesOf(targets[0]) * offset;
            var originals = new List<Object>();
            var allCopies = new List<Object>();

            foreach (var target in targets)
                originals.Add(target.gameObject);

            for (var copyIndex = 1; copyIndex <= Mathf.Min(count, MaxCopyCount); copyIndex++)
            {
                Selection.objects = originals.ToArray();
                Unsupported.DuplicateGameObjectsUsingPasteboard();

                foreach (var copy in Selection.gameObjects)
                {
                    Undo.RecordObject(copy.transform, "Copy Live Builder Objects");
                    copy.transform.position += worldOffset * copyIndex;
                    allCopies.Add(copy);
                }
            }

            Selection.objects = allCopies.ToArray();
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
