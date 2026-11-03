using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Выдавливание коробки, как экструд в ProBuilder: тянешь с Ctrl — коробки стоят, а от граней растут новые коробки того же сечения.
    /// - у выделенной коробки — квадрат одной грани (<see cref="BoxModelPartHandles"/>)
    /// - в режиме «Грани» — стрелки выделенных граней (<see cref="BoxModelFaceMoveHandle"/>): от каждой грани своя коробка
    /// Новая коробка — отдельный объект сразу за исходной в иерархии: та же сторона (добавляет или вырезает), те же материалы.
    /// На месте грани — шов: новую часть можно отдельно сузить, покрасить, удалить.
    /// Отпустил — выделены новые (в «Гранях» — их дальние грани), Ctrl ещё раз — выдавить дальше.
    /// Не вытянул ни на шаг сетки — новой коробки нет. Всё выдавливание — один шаг Ctrl+Z.
    /// </summary>
    public static class BoxModelPartExtrusion
    {
        public const string Hint = "Тяни квадрат — размер, Ctrl — выдавить новую коробку от грани. Delete — удалить коробку";

        // Что выдавливаем сейчас: по одной записи на грань. Не выдавливаем — пусто.
        private static readonly List<FaceExtrusion> Extrusions = new();

        private static int undoGroup;
        private static bool isFromFaceSelection;


        // ── Одна грань коробки ───────────────────────────────────────────

        // Ручки коробки сдвинули: с Ctrl — растить новую коробку и вернуть true; без Ctrl — false, коробка меняет размер как обычно.
        // min, max — углы коробки до сдвига, dragged — после, всё в осях объекта. Ctrl нужен только для начала: дальше можно отпустить.
        public static bool TryExtrude(BoxModel model, BoxModelPart part, Vector3 min, Vector3 max, Vector3 draggedMin, Vector3 draggedMax, bool isExtrudeKeyHeld)
        {
            DropDeletedExtrusions();

            // Выдавливаем — ручки остальных выделенных коробок не трогаем.
            if (Extrusions.Count > 0)
            {
                if (Extrusions[0].Source == part)
                    Grow(Extrusions[0], Extrusions[0].IsPositive ? draggedMax[Extrusions[0].Axis] : draggedMin[Extrusions[0].Axis]);

                return true;
            }

            if (!isExtrudeKeyHeld || !TryFindPulledFace(min, max, draggedMin, draggedMax, out var axis, out var isPositive))
                return false;

            BeginUndoGroup(false);
            var extrusion = Begin(model, part, BoxFaces.Index(axis, isPositive));
            Grow(extrusion, isPositive ? draggedMax[axis] : draggedMin[axis]);
            return true;
        }

        // Сдвинулась грань max — она, иначе min. С Alt или Shift сдвигаются несколько — берётся первая.
        private static bool TryFindPulledFace(Vector3 min, Vector3 max, Vector3 draggedMin, Vector3 draggedMax, out int axis, out bool isPositive)
        {
            for (axis = 0; axis < 3; axis++)
            {
                isPositive = !Mathf.Approximately(draggedMax[axis], max[axis]);

                if (isPositive || !Mathf.Approximately(draggedMin[axis], min[axis]))
                    return true;
            }

            axis = 0;
            isPositive = false;
            return false;
        }


        // ── Выделенные грани ─────────────────────────────────────────────

        // Стрелки выделенных граней сдвинули на worldOffset от начала: с Ctrl — от каждой грани растёт коробка, true;
        // без Ctrl — false, грани двигаются как обычно. Каждая растёт поперёк себя: тянешь вверх — растут только от верхних граней.
        public static bool TryExtrudeFaces(IReadOnlyList<BoxModelPartFace> faces, Vector3 worldOffset, bool isExtrudeKeyHeld)
        {
            DropDeletedExtrusions();

            if (Extrusions.Count == 0)
            {
                if (!isExtrudeKeyHeld || faces.Count == 0)
                    return false;

                BeginUndoGroup(true);

                foreach (var partFace in faces)
                    if (partFace.Model != null)
                        Begin(partFace.Model, partFace.Part, partFace.Face);
            }

            foreach (var extrusion in Extrusions)
            {
                var offset = extrusion.Model.transform.InverseTransformVector(worldOffset)[extrusion.Axis];
                Grow(extrusion, extrusion.SourceFace + offset);
            }

            return true;
        }


        // ── Рост ─────────────────────────────────────────────────────────

        // Новые коробки и правки их размера — один шаг Ctrl+Z.
        private static void BeginUndoGroup(bool startsFromFaceSelection)
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Extrude Box");
            isFromFaceSelection = startsFromFaceSelection;
        }

        // Новая коробка — копия исходной сразу за ней в иерархии: от её места зависит, что она перекрывает.
        private static FaceExtrusion Begin(BoxModel model, BoxModelPart part, int face)
        {
            var partObject = new GameObject(part.Mode == BoxModelPartMode.Add ? BoxModelRebuilder.AddPartName : BoxModelRebuilder.CutPartName)
            {
                tag = part.gameObject.tag,
                layer = part.gameObject.layer
            };
            Undo.RegisterCreatedObjectUndo(partObject, "Extrude Box");

            var partTransform = partObject.transform;
            partTransform.SetParent(part.transform.parent, false);
            partTransform.SetSiblingIndex(part.transform.GetSiblingIndex() + 1);
            partTransform.localRotation = part.transform.localRotation;
            partTransform.localScale = part.transform.localScale;

            var extrudedPart = partObject.AddComponent<BoxModelPart>();
            EditorUtility.CopySerialized(part, extrudedPart);

            var sourceMin = BoxModelRebuilder.LocalMinOf(model, part);
            var extrusion = new FaceExtrusion(model, part, extrudedPart, face, sourceMin, sourceMin + part.Size);
            Extrusions.Add(extrusion);
            return extrusion;
        }

        // Новая коробка — от грани-основы до вытянутой грани по сетке, не тоньше шага; сечение — как у грани.
        private static void Grow(FaceExtrusion extrusion, float pulledFace)
        {
            var axis = extrusion.Axis;
            var sign = extrusion.IsPositive ? 1f : -1f;
            var step = BoxModelGrid.StepOn(axis);
            var depth = (BoxModelGrid.Snap(pulledFace, axis) - extrusion.SourceFace) * sign;
            extrusion.HasDepth = depth >= step * 0.5f;

            var min = extrusion.SourceMin;
            var max = extrusion.SourceMax;

            if (extrusion.IsPositive)
            {
                min[axis] = extrusion.SourceFace;
                max[axis] = extrusion.SourceFace + Mathf.Max(step, depth);
            }
            else
            {
                max[axis] = extrusion.SourceFace;
                min[axis] = extrusion.SourceFace - Mathf.Max(step, depth);
            }

            var extruded = extrusion.Extruded;
            Undo.RecordObjects(new Object[] { extruded, extruded.transform }, "Extrude Box");
            extruded.transform.position = extrusion.Model.transform.TransformPoint(min);
            extruded.Size = max - min;
        }

        // Коробку удалили посреди выдавливания (Ctrl+Z, Delete) — её запись больше не нужна.
        private static void DropDeletedExtrusions()
        {
            Extrusions.RemoveAll(extrusion => extrusion.Extruded == null || extrusion.Source == null || extrusion.Model == null);
        }


        // ── Конец ────────────────────────────────────────────────────────

        // Кнопку мыши отпустили: невытянутые коробки — прочь, вытянутые выделены, шаг Ctrl+Z один. Ни одной — как не было.
        public static void FinishIfReleased()
        {
            if (Extrusions.Count == 0 || GUIUtility.hotControl != 0)
                return;

            DropDeletedExtrusions();
            var kept = new List<FaceExtrusion>();

            foreach (var extrusion in Extrusions)
                if (extrusion.HasDepth)
                    kept.Add(extrusion);

            if (kept.Count == 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Extrusions.Clear();
                return;
            }

            foreach (var extrusion in Extrusions)
                if (!extrusion.HasDepth)
                    Undo.DestroyObjectImmediate(extrusion.Extruded.gameObject);

            SelectExtruded(kept);
            Undo.CollapseUndoOperations(undoGroup);
            Extrusions.Clear();
        }

        // В «Гранях» — дальние грани новых коробок, иначе — сами коробки.
        private static void SelectExtruded(List<FaceExtrusion> kept)
        {
            if (!isFromFaceSelection)
            {
                var extrudedObjects = kept.ConvertAll(extrusion => (Object)extrusion.Extruded.gameObject);
                Selection.objects = extrudedObjects.ToArray();
                return;
            }

            BoxModelFaceSelection.Clear();

            foreach (var extrusion in kept)
                BoxModelFaceSelection.Select(new BoxModelPartFace(extrusion.Extruded, extrusion.Face), true);
        }


        // Подпись у вытянутой грани первой коробки: насколько выдавили.
        public static void DrawDepthLabel()
        {
            if (Extrusions.Count == 0 || Event.current.type != EventType.Repaint)
                return;

            var extrusion = Extrusions[0];

            if (extrusion.Extruded == null || extrusion.Model == null)
                return;

            var min = BoxModelRebuilder.LocalMinOf(extrusion.Model, extrusion.Extruded);
            var max = min + extrusion.Extruded.Size;
            var farFace = (min + max) / 2f;
            farFace[extrusion.Axis] = extrusion.IsPositive ? max[extrusion.Axis] : min[extrusion.Axis];

            BoxModelSceneText.DrawLabel(extrusion.Model.transform.TransformPoint(farFace),
                extrusion.HasDepth ? "выдавить " + BoxModelSceneText.FormatMeters(extrusion.Extruded.Size[extrusion.Axis]) + " м" : "тяни дальше — новая коробка");
        }


        // ── Одна грань в работе ──────────────────────────────────────────

        // Грань-основа и коробка, что от неё растёт. Углы исходной коробки — в осях объекта, на начало выдавливания.
        private sealed class FaceExtrusion
        {
            public FaceExtrusion(BoxModel model, BoxModelPart source, BoxModelPart extruded, int face, Vector3 sourceMin, Vector3 sourceMax)
            {
                Model = model;
                Source = source;
                Extruded = extruded;
                Face = face;
                SourceMin = sourceMin;
                SourceMax = sourceMax;
            }


            public BoxModel Model { get; }
            public BoxModelPart Source { get; }
            public BoxModelPart Extruded { get; }
            public int Face { get; }
            public Vector3 SourceMin { get; }
            public Vector3 SourceMax { get; }
            public bool HasDepth { get; set; }

            public int Axis => BoxFaces.Axis(Face);
            public bool IsPositive => BoxFaces.IsPositive(Face);
            public float SourceFace => IsPositive ? SourceMax[Axis] : SourceMin[Axis];
        }
    }
}
