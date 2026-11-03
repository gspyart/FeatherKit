using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Выделение кликом в инструменте «Live Builder» — что ближе к камере под мышью (<see cref="LiveBuilderSceneHit"/>):
    /// - грань объекта из коробок — коробка, комната или лестница, которая её создала; в режиме «Грани» — сама грань
    /// - стена по линии или здание по контуру — они сами
    /// - дыра от выреза (<see cref="GeometryCutter"/>) или вырезающей коробки в здании — сам резчик:
    ///   он невидим, но его коробка ближе того, что за дырой, — и откос дыры тоже
    /// - мимо всего — снять выделение. Shift — добавить к выделению или убрать
    /// Спрятанное разрезом над этажом клик не ловит.
    /// </summary>
    public static class LiveBuilderClickSelection
    {
        public static void Select(Event current)
        {
            var hit = LiveBuilderSceneHit.Find(HandleUtility.GUIPointToWorldRay(current.mousePosition));

            if (hit.IsHoleCutterInFront)
            {
                Apply(hit.HoleCutter.gameObject, current.shift);
                return;
            }

            if (hit.IsPathOwnerCloser)
            {
                Apply(hit.PathOwner.gameObject, current.shift);
                return;
            }

            if (!hit.HasBoxSurface)
            {
                if (current.shift)
                    return;

                BoxModelFaceSelection.Clear();
                Selection.activeObject = null;
                return;
            }

            if (BoxModelSelectionModeSetting.IsSelectingFaces)
            {
                if (BoxModelPartFace.TryFindUnder(hit.BoxSurface, out var partFace))
                    BoxModelFaceSelection.Select(partFace, current.shift);

                return;
            }

            var clicked = BoxModelPicker.SourceBoxOf(hit.BoxSurface)?.Owner;
            Apply(clicked != null ? clicked.gameObject : hit.BoxSurface.Model.gameObject, current.shift);
        }

        // Без Shift — только этот объект; с Shift — добавить или убрать.
        private static void Apply(GameObject clickedObject, bool isAdditive)
        {
            if (!isAdditive)
            {
                Selection.activeGameObject = clickedObject;
                return;
            }

            var selection = new List<Object>(Selection.objects);

            if (!selection.Remove(clickedObject))
                selection.Add(clickedObject);

            Selection.objects = selection.ToArray();
        }
    }
}
