using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Грань одной коробки: какая коробка и какая сторона (<see cref="BoxFaces"/>).
    /// С куском (<see cref="BoxModelFacePiece"/>) — не вся грань, а только видимый прямоугольник на ней.
    /// </summary>
    public readonly struct BoxModelPartFace
    {
        public BoxModelPartFace(BoxModelPart part, int face, BoxModelFacePiece piece = null)
        {
            Part = part;
            Face = face;
            Piece = piece;
        }


        public BoxModelPart Part { get; }
        public int Face { get; }

        // Кусок грани; null — грань целиком.
        public BoxModelFacePiece Piece { get; }

        public bool IsPiece => Piece != null;
        public BoxModelPartFace WholeFace => new(Part, Face);
        public BoxModel Model => Part != null ? Part.GetComponentInParent<BoxModel>() : null;


        // Кусок грани под мышью и коробка, которая его создала; у стенки ниши — грань вырезающей коробки; у копии повтора — грань оригинала.
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

            partFace = new BoxModelPartFace(part, BoxFaces.Index(axis, isOnMaxSide), new BoxModelFacePiece(hit.Surface));
            return true;
        }


        public bool IsSameAs(BoxModelPartFace other)
        {
            return Part == other.Part && Face == other.Face && (IsPiece ? Piece.IsSameAs(other.Piece) : !other.IsPiece);
        }
    }


    /// <summary>
    /// Выделенные грани коробок в режиме «Грани»: клик — выделить кусок грани, двойной клик — грань целиком, Shift — добавить или убрать.
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
            var index = SelectedFaces.FindIndex(selected => selected.IsSameAs(partFace));

            if (!isAdditive)
                SelectedFaces.Clear();

            if (isAdditive && index >= 0)
                SelectedFaces.RemoveAt(index);
            else
                SelectedFaces.Add(partFace);

            SelectModelsInUnity();
        }


        // Двойной клик по куску: вместо него — вся грань его коробки, остальное выделенное остаётся.
        public static void ReplaceWithWholeFace(BoxModelPartFace piece)
        {
            var wholeFace = piece.WholeFace;
            SelectedFaces.RemoveAll(selected => selected.IsSameAs(piece));

            if (!SelectedFaces.Exists(selected => selected.IsSameAs(wholeFace)))
                SelectedFaces.Add(wholeFace);

            SelectModelsInUnity();
        }


        // Кусок вытянули в новую коробку: вместо куска — грань новой коробки.
        public static void Replace(BoxModelPartFace replaced, BoxModelPartFace replacement)
        {
            SelectedFaces.RemoveAll(selected => selected.IsSameAs(replaced));
            SelectedFaces.Add(replacement);
            SelectModelsInUnity();
        }


        // Набрать грань выделенной коробки или убрать из набора — Shift+клик по её квадрату. Выделение в Unity не трогает.
        public static void Toggle(BoxModelPartFace partFace)
        {
            var index = SelectedFaces.FindIndex(selected => selected.IsSameAs(partFace));

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
            return SelectedFaces.Exists(selected => selected.IsSameAs(partFace));
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
    }
}
