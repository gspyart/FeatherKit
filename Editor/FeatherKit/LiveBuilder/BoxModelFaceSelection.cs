using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Одна грань одной коробки: какая коробка и какая сторона (<see cref="BoxFaces"/>).</summary>
    public readonly struct BoxModelPartFace
    {
        public BoxModelPartFace(BoxModelPart part, int face)
        {
            Part = part;
            Face = face;
        }


        public BoxModelPart Part { get; }
        public int Face { get; }

        public BoxModel Model => Part != null ? Part.GetComponentInParent<BoxModel>() : null;


        // Грань коробки, которая создала поверхность под мышью; у стенки ниши — грань вырезающей коробки; у копии повтора — грань оригинала.
        // Сторону ищем по плоскости: у вырезающей стенка смотрит внутрь коробки, поэтому сторона поверхности не годится.
        public static bool TryFindUnder(BoxModelSurfaceHit hit, out BoxModelPartFace partFace)
        {
            partFace = default;
            var sourceBox = BoxModelPicker.SourceBoxOf(hit);
            var part = sourceBox?.OwnerPart;

            if (part == null)
                return false;

            // Сторону ищем по той копии, на которой клик: у повтора копии стоят в другом месте, чем оригинал.
            var axis = BoxFaces.Axis(hit.Surface.Face);
            var min = sourceBox.Box.Min;
            var max = sourceBox.Box.Max;
            var isOnMaxSide = Mathf.Abs(max[axis] - hit.Surface.Plane) <= Mathf.Abs(min[axis] - hit.Surface.Plane);

            partFace = new BoxModelPartFace(part, BoxFaces.Index(axis, isOnMaxSide));
            return true;
        }
    }


    /// <summary>
    /// Выделенные грани коробок в режиме «Грани»: клик — выделить одну, Shift — добавить или убрать.
    /// Unity такое выделение не хранит, поэтому оно живёт здесь; объекты этих граней при этом выделены в Unity.
    /// Грани коробок, которых больше нет или чей объект сняли с выделения, отбрасываются сами.
    /// </summary>
    public static class BoxModelFaceSelection
    {
        private static readonly List<BoxModelPartFace> SelectedFaces = new();


        public static IReadOnlyList<BoxModelPartFace> Faces
        {
            get
            {
                SelectedFaces.RemoveAll(IsStale);
                return SelectedFaces;
            }
        }


        // ── Изменение ────────────────────────────────────────────────────

        // Без Shift — только эта грань; с Shift — добавить к выделенным или убрать, если уже выделена.
        public static void Select(BoxModelPartFace partFace, bool isAdditive)
        {
            var index = SelectedFaces.FindIndex(selected => IsSame(selected, partFace));

            if (!isAdditive)
                SelectedFaces.Clear();

            if (isAdditive && index >= 0)
                SelectedFaces.RemoveAt(index);
            else
                SelectedFaces.Add(partFace);

            SelectModelsInUnity();
        }


        // Набрать грань выделенной коробки или убрать из набора — Shift+клик по её квадрату. Выделение в Unity не трогает.
        public static void Toggle(BoxModelPartFace partFace)
        {
            var index = SelectedFaces.FindIndex(selected => IsSame(selected, partFace));

            if (index >= 0)
                SelectedFaces.RemoveAt(index);
            else
                SelectedFaces.Add(partFace);
        }


        public static void Clear()
        {
            SelectedFaces.Clear();
        }


        public static bool Contains(BoxModelPartFace partFace)
        {
            SelectedFaces.RemoveAll(IsStale);
            return SelectedFaces.Exists(selected => IsSame(selected, partFace));
        }


        // ── Помощники ────────────────────────────────────────────────────

        // В Unity выделены объекты выделенных граней: видно каркасы, в инспекторе — объект.
        private static void SelectModelsInUnity()
        {
            var models = new List<Object>();

            foreach (var selected in SelectedFaces)
                if (selected.Model != null && !models.Contains(selected.Model.gameObject))
                    models.Add(selected.Model.gameObject);

            Selection.objects = models.ToArray();
        }

        // Грань живёт, пока выделен её объект (режим «Грани») или сама коробка (набор Shift+кликом по квадратам).
        private static bool IsStale(BoxModelPartFace partFace)
        {
            return partFace.Part == null || partFace.Model == null
                   || !Selection.Contains(partFace.Model.gameObject) && !Selection.Contains(partFace.Part.gameObject);
        }

        private static bool IsSame(BoxModelPartFace first, BoxModelPartFace second)
        {
            return first.Part == second.Part && first.Face == second.Face;
        }
    }
}
