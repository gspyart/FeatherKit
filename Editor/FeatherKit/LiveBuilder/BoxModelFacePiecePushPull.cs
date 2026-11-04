using System;
using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Тяни-толкай для кусков граней (<see cref="BoxModelFacePiece"/>), как в SketchUp. Кусок — часть грани коробки и сам не двигается,
    /// поэтому от него растёт новая коробка ровно его размера: тянешь наружу — добавляет объём, толкаешь внутрь — вырезает.
    /// Исходная коробка не меняется. Новая — в конце объекта, главнее всего прежнего: выходит ровно то, что видно. Материалы — как у исходной.
    /// Не сдвинул ни на шаг сетки — коробки нет. Всё — один шаг Ctrl+Z.
    /// Отпустил — вместо куска выделена дальняя грань новой коробки: потянешь её — коробка растёт или вырез углубляется.
    /// </summary>
    public static class BoxModelFacePiecePushPull
    {
        // Что тянем сейчас: по записи на кусок. Не тянем — пусто.
        private static readonly List<PiecePush> Pushes = new();

        // Группа Ctrl+Z всего жеста: её открывает тот, кто тянет (BoxModelFaceMoveHandle), до первого Drag.
        private static int undoGroup;

        // В том же жесте тянут и цельные грани: группу откатывать нельзя — откатится и их сдвиг.
        private static bool isUndoGroupShared;


        // ── Тянуть ───────────────────────────────────────────────────────

        // Сдвинуть куски: outwardOffsetOf — насколько кусок ушёл наружу (минус — внутрь), м, уже по сетке. Цельные грани пропускаются.
        // Группу Ctrl+Z на весь жест открывает вызывающий — до первого вызова.
        public static void Drag(IReadOnlyList<BoxModelPartFace> faces, Func<BoxModelPartFace, float> outwardOffsetOf)
        {
            Pushes.RemoveAll(push => push.Box == null || push.Piece.Model == null);

            if (Pushes.Count == 0)
                Begin(faces);

            foreach (var push in Pushes)
                Resize(push, outwardOffsetOf(push.Piece));
        }

        private static void Begin(IReadOnlyList<BoxModelPartFace> faces)
        {
            isUndoGroupShared = false;

            foreach (var partFace in faces)
            {
                if (!partFace.IsPiece)
                    isUndoGroupShared = true;

                if (!partFace.IsPiece || partFace.Model == null)
                    continue;

                if (Pushes.Count == 0)
                {
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Push Pull Face");
                }

                Pushes.Add(new PiecePush(partFace, CreateBox(partFace)));
            }
        }

        // Новая коробка — копия исходной (материалы те же) в конце объекта: главнее всего, что было.
        private static BoxModelPart CreateBox(BoxModelPartFace partFace)
        {
            var source = partFace.Part;
            var boxObject = new GameObject(BoxModelPieceTidying.AddPartName) { tag = source.gameObject.tag, layer = source.gameObject.layer };
            Undo.RegisterCreatedObjectUndo(boxObject, "Push Pull Face");

            var boxTransform = boxObject.transform;
            boxTransform.SetParent(partFace.Model.transform, false);
            boxTransform.SetAsLastSibling();

            var box = boxObject.AddComponent<BoxModelPart>();
            EditorUtility.CopySerialized(source, box);

            // Скосы рёбер остаются у исходной коробки: новая — прямая.
            while (box.Chamfers.Count > 0)
                box.RemoveChamfer(box.Chamfers[0].FirstFace, box.Chamfers[0].SecondFace);

            return box;
        }

        // Наружу — добавляет, внутрь — вырезает; не дотянул до полшага — коробка спрятана, на отпускании её не будет.
        private static void Resize(PiecePush push, float outwardOffset)
        {
            var piece = push.Piece.Piece;
            var step = LiveBuilderGrid.StepOn(piece.Axis);
            var depth = Mathf.Abs(outwardOffset);
            var isPulled = outwardOffset > 0f;
            push.HasDepth = depth >= step * 0.5f;
            piece.BoxFromPlane(Mathf.Max(step, depth), isPulled ? 1f : -1f, out var min, out var max);

            var box = push.Box;
            var mode = isPulled ? BoxModelPartMode.Add : BoxModelPartMode.Cut;
            Undo.RecordObjects(new Object[] { box, box.transform, box.gameObject }, "Push Pull Face");
            box.gameObject.SetActive(push.HasDepth);
            box.gameObject.name = isPulled ? BoxModelPieceTidying.AddPartName : BoxModelPieceTidying.CutPartName;
            box.Mode = mode;
            box.transform.localPosition = min;
            box.Size = max - min;
        }


        // ── Конец ────────────────────────────────────────────────────────

        // Кнопку мыши отпустили: пустые коробки — прочь, вместо кусков выделены дальние грани новых. Ни одной — как не было.
        // Цельные грани тянули тем же жестом — убираем только свои коробки, их сдвиг остаётся.
        public static void FinishIfReleased()
        {
            if (Pushes.Count == 0 || GUIUtility.hotControl != 0)
                return;

            Pushes.RemoveAll(push => push.Box == null);
            var kept = Pushes.FindAll(push => push.HasDepth);

            if (kept.Count == 0 && !isUndoGroupShared)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Pushes.Clear();
                return;
            }

            foreach (var push in Pushes)
                if (!push.HasDepth)
                    Undo.DestroyObjectImmediate(push.Box.gameObject);

            foreach (var push in kept)
                BoxModelFaceSelection.Replace(push.Piece, new BoxModelPartFace(push.Box, FarFaceOf(push)));

            Undo.CollapseUndoOperations(undoGroup);
            Pushes.Clear();
        }

        // Дальняя грань: у вытянутой — наружная, у выреза — дно.
        private static int FarFaceOf(PiecePush push)
        {
            var outwardFace = push.Piece.Piece.OutwardFace;
            return push.Box.Mode == BoxModelPartMode.Add ? outwardFace : BoxFaces.Opposite(outwardFace);
        }


        // Подпись у первого куска: насколько вытянули или вдавили.
        public static void DrawDepthLabel()
        {
            if (Pushes.Count == 0 || Event.current.type != EventType.Repaint)
                return;

            var push = Pushes[0];
            var model = push.Piece.Model;

            if (push.Box == null || model == null)
                return;

            var label = !push.HasDepth
                ? "тяни — коробка, толкай — вырез"
                : (push.Box.Mode == BoxModelPartMode.Add ? "вытянуть " : "вырезать ") + LiveBuilderSceneText.FormatMeters(push.Box.Size[push.Piece.Piece.Axis]) + " м";

            LiveBuilderSceneText.DrawLabel(model.transform.TransformPoint(push.Piece.Piece.Center), label);
        }


        // ── Один кусок в работе ──────────────────────────────────────────

        // Кусок и коробка, что от него растёт.
        private sealed class PiecePush
        {
            public PiecePush(BoxModelPartFace piece, BoxModelPart box)
            {
                Piece = piece;
                Box = box;
            }


            public BoxModelPartFace Piece { get; }
            public BoxModelPart Box { get; }
            public bool HasDepth { get; set; }
        }
    }
}
