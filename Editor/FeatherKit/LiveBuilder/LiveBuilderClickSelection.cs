using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Выделение кликом в Live Builder — что ближе к камере под мышью (<see cref="LiveBuilderSceneHit"/>), если это правит выбранный инструмент
    /// (<see cref="LiveBuilderToolScope"/>): в «Выделении» — что угодно, в «Коробке» — только коробки и так далее:
    /// - грань объекта из коробок — коробка, комната или лестница, которая её создала;
    ///   в режиме «Грани» — видимый кусок грани под мышью, двойной клик — грань коробки целиком
    /// - стена по линии или здание по контуру — они сами
    /// - дыра от выреза (<see cref="GeometryCutter"/>) или вырезающей коробки в здании — сам резчик:
    ///   он невидим, но его коробка ближе того, что за дырой, — и откос дыры тоже
    /// - мимо всего или не своё — снять выделение. Shift — добавить к выделению или убрать
    /// Спрятанное разрезом над этажом клик не ловит.
    /// </summary>
    public static class LiveBuilderClickSelection
    {
        public static void Select(Event current, LiveBuilderTool tool, bool isDoubleClick)
        {
            var hit = LiveBuilderSceneHit.Find(HandleUtility.GUIPointToWorldRay(current.mousePosition));

            if (hit.HasBoxSurface && !hit.IsHoleCutterInFront && !hit.IsPathOwnerCloser && BoxModelSelectionModeSetting.IsSelectingFaces
                && LiveBuilderToolScope.CanEditFaces(tool))
            {
                if (!BoxModelPartFace.TryFindUnder(hit.BoxSurface, out var piece))
                    return;

                if (isDoubleClick)
                    BoxModelFaceSelection.ReplaceWithWholeFace(piece);
                else
                    BoxModelFaceSelection.Select(piece, current.shift);

                return;
            }

            var clicked = ClickedObject(hit);

            if (clicked != null && LiveBuilderToolScope.CanEdit(tool, clicked))
            {
                Apply(clicked, current.shift);
                return;
            }

            if (current.shift)
                return;

            BoxModelFaceSelection.Clear();
            Selection.activeObject = null;
        }


        // В инструментах линии клик по своему готовому (стене в «Стене», зданию в «Здании») выделяет его, а не начинает новую линию.
        // Не своё или пусто — false: клик достанется рисованию.
        public static bool TrySelectOwn(Event current, LiveBuilderTool tool)
        {
            var clicked = ClickedObject(LiveBuilderSceneHit.Find(HandleUtility.GUIPointToWorldRay(current.mousePosition)));

            if (clicked == null || !LiveBuilderToolScope.CanEdit(tool, clicked))
                return false;

            Apply(clicked, false);
            return true;
        }

        // Что под мышью выделилось бы: резчик дыры, стена или здание, коробка или комната с лестницей, создавшая грань, иначе объект из коробок.
        private static GameObject ClickedObject(LiveBuilderSceneHit hit)
        {
            if (hit.IsHoleCutterInFront)
                return hit.HoleCutter.gameObject;

            if (hit.IsPathOwnerCloser)
                return hit.PathOwner.gameObject;

            if (!hit.HasBoxSurface)
                return null;

            var owner = BoxModelPicker.SourceBoxOf(hit.BoxSurface)?.Owner;
            return owner != null ? owner.gameObject : hit.BoxSurface.Model.gameObject;
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
