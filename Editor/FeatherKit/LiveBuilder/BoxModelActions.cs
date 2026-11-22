using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Действия над объектами из коробок: новый объект, «Add ⇄ Cut» у выделенных коробок и вырез-коробок, заморозка, что выделено.
    /// Зовут их окно Live Builder и меню — одно место на каждое действие. Включить сам инструмент — <see cref="LiveBuilderActivation"/>.
    /// </summary>
    public static class BoxModelActions
    {
        // Без ProBuilder в проекте замораживать не во что — кнопки нет.
        public static bool CanFreezeToProBuilder
        {
            get
            {
#if FEATHERKIT_PROBUILDER
                return true;
#else
                return false;
#endif
            }
        }


        // ── Объекты и коробки ────────────────────────────────────────────

        // Новый объект рисованием, как в ProBuilder: выделение снимается, включается «Коробка», следующая нарисованная коробка — новый объект.
        // Сначала инструмент: смена инструмента снимает «Новый».
        public static void BeginNewModelDrawing()
        {
            Selection.activeObject = null;
            LiveBuilderToolPalette.Current = LiveBuilderToolPalette.Get<LiveBuilderBoxTool>();
            LiveBuilderBoxTool.IsNewModelRequested = true;
            LiveBuilderActivation.SetToolActive(true);
        }


        // «Add ⇄ Cut» у выделенных одним шагом Undo: коробка — в вырез-коробку, вырез-коробка — в коробку (<see cref="GeometryCutterBoxConversion"/>).
        public static void ToggleAddCutOfSelected()
        {
            var parts = SelectedParts();
            var cutters = SelectedCutterBoxes();

            if (parts.Count == 0 && cutters.Count == 0)
                return;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Toggle Add / Cut");

            foreach (var part in parts)
                if (part.Mode == BoxModelPartMode.Cut)
                    GeometryCutterBoxConversion.ToAddingPart(part);
                else
                    GeometryCutterBoxConversion.ToCutter(part);

            foreach (var cutter in cutters)
                GeometryCutterBoxConversion.ToPart(cutter);

            Undo.CollapseUndoOperations(undoGroup);
        }


        public static void FreezeSelectedModel()
        {
#if FEATHERKIT_PROBUILDER
            var model = SelectedModel();

            if (model != null)
                BoxModelProBuilderFreezer.Freeze(model);
#endif
        }


        // ── Выделение ────────────────────────────────────────────────────

        public static List<BoxModelPart> SelectedParts()
        {
            var parts = new List<BoxModelPart>();

            foreach (var selected in Selection.gameObjects)
                if (selected.TryGetComponent<BoxModelPart>(out var part))
                    parts.Add(part);

            return parts;
        }


        // Выделенные вырезы, которые можно превратить в коробку: коробка по осям своего объекта.
        public static List<GeometryCutter> SelectedCutterBoxes()
        {
            var cutters = new List<GeometryCutter>();

            foreach (var selected in Selection.gameObjects)
                if (selected.TryGetComponent<GeometryCutter>(out var cutter)
                    && GeometryCutterSolverBoxes.IsBoxAlongAxesOf(cutter, GeometryCutterSolverBoxes.CollectingModelOf(cutter)))
                    cutters.Add(cutter);

            return cutters;
        }


        // Объект из коробок, в котором лежит выделенное: сам объект или любая его коробка.
        public static BoxModel SelectedModel()
        {
            var selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<BoxModel>() : null;
        }
    }
}
