using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Повтор выделенного в объекте из коробок (<see cref="BoxModelRepeat"/>): завернуть выделенные коробки, комнаты и лестницы в группу повтора,
    /// развернуть её обратно, найти повтор, к которому относится выделенное. Всё с Undo.
    /// </summary>
    public static class LiveBuilderRepeatActions
    {
        // ── Завернуть и развернуть ───────────────────────────────────────

        // Выделенные коробки, лестницы и группы — в новую группу повтора на месте первой из них. Шаг — как задан.
        public static BoxModelRepeat WrapSelection(Vector3 step)
        {
            var pieces = SelectedPieces();

            if (pieces.Count == 0 || !CanWrapSelection(out _))
                return null;

            pieces.Sort((first, second) => first.GetSiblingIndex().CompareTo(second.GetSiblingIndex()));
            var parent = pieces[0].parent;
            var siblingIndex = pieces[0].GetSiblingIndex();

            Undo.SetCurrentGroupName("Repeat Boxes");
            var groupObject = new GameObject("Repeat");
            Undo.RegisterCreatedObjectUndo(groupObject, "Repeat Boxes");
            groupObject.transform.SetParent(parent, false);
            groupObject.transform.SetSiblingIndex(siblingIndex);

            foreach (var piece in pieces)
                Undo.SetTransformParent(piece, groupObject.transform, true, "Repeat Boxes");

            var repeat = Undo.AddComponent<BoxModelRepeat>(groupObject);
            repeat.Count = 2;
            repeat.Step = step;
            Selection.activeGameObject = groupObject;
            return repeat;
        }


        // Содержимое группы — обратно на её место, сама группа удаляется. Копии исчезают, остаётся оригинал.
        public static void Unwrap(BoxModelRepeat repeat)
        {
            var groupTransform = repeat.transform;
            var parent = groupTransform.parent;
            var siblingIndex = groupTransform.GetSiblingIndex();
            var children = new List<Transform>();

            foreach (Transform child in groupTransform)
                children.Add(child);

            Undo.SetCurrentGroupName("Remove Repeat");

            for (var index = 0; index < children.Count; index++)
            {
                Undo.SetTransformParent(children[index], parent, true, "Remove Repeat");
                children[index].SetSiblingIndex(siblingIndex + index);
            }

            Selection.objects = children.ConvertAll(child => (Object)child.gameObject).ToArray();
            Undo.DestroyObjectImmediate(repeat.gameObject);
        }


        // Завернуть можно только части одного уровня списка: иначе они переедут из своего объекта, из интерьера здания или из своей группы.
        public static bool CanWrapSelection(out string reason)
        {
            var pieces = SelectedPieces();
            reason = "";

            foreach (var piece in pieces)
            {
                if (piece.parent != pieces[0].parent)
                {
                    reason = "выделены части из разных объектов или групп — выдели части одного уровня списка.";
                    return false;
                }
            }

            return true;
        }


        // ── Что выделено ─────────────────────────────────────────────────

        // Выделенные части объектов из коробок: коробки, комнаты, лестницы и группы повтора.
        public static List<Transform> SelectedPieces()
        {
            var pieces = new List<Transform>();

            foreach (var selected in Selection.gameObjects)
            {
                var isPiece = selected.GetComponent<BoxModelPart>() != null || selected.GetComponent<BoxModelStairs>() != null
                    || selected.GetComponent<BoxModelRoom>() != null || selected.GetComponent<BoxModelRepeat>() != null;

                if (isPiece && selected.GetComponentInParent<BoxModel>() != null)
                    pieces.Add(selected.transform);
            }

            return pieces;
        }


        // Повтор, к которому относится выделенное: сама группа или ближайшая группа над выделенной коробкой.
        public static BoxModelRepeat SelectedRepeat()
        {
            var selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<BoxModelRepeat>() : null;
        }


        // Габарит выделенного в осях объекта, м — по нему считаются шаги «вдоль X» и «вдоль Z».
        public static Bounds SelectedBounds()
        {
            var bounds = new Bounds();
            var hasAny = false;

            foreach (var piece in SelectedPieces())
            {
                var model = piece.GetComponentInParent<BoxModel>();

                foreach (var box in BoxModelRebuilder.GetSolverBoxes(model))
                {
                    if (box.Owner == null || !box.Owner.transform.IsChildOf(piece))
                        continue;

                    var boxBounds = new Bounds();
                    boxBounds.SetMinMax(box.Box.MinInMeters, box.Box.MaxInMeters);

                    if (hasAny)
                        bounds.Encapsulate(boxBounds);
                    else
                        bounds = boxBounds;

                    hasAny = true;
                }
            }

            return bounds;
        }
    }
}
