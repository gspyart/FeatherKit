using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Действия над объектами из коробок: новый объект, «добавить ⇄ вырезать» у выделенных коробок, заморозка, что выделено.
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

        // Новый объект рисованием, как в ProBuilder: выделение снимается, включается инструмент, следующая нарисованная коробка — новый объект.
        public static void BeginNewModelDrawing()
        {
            Selection.activeObject = null;
            LiveBuilderBoxTool.IsNewModelRequested = true;
            LiveBuilderActivation.SetToolActive(true);
        }


        // Выделенные коробки: добавляющие — в вырезающие, и наоборот.
        public static void ToggleModeOfSelectedParts()
        {
            var parts = SelectedParts();

            if (parts.Count == 0)
                return;

            Undo.RecordObjects(parts.ToArray(), "Toggle Box Mode");

            foreach (var part in parts)
                part.Mode = part.Mode == BoxModelPartMode.Add ? BoxModelPartMode.Cut : BoxModelPartMode.Add;
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


        // Объект из коробок, в котором лежит выделенное: сам объект или любая его коробка.
        public static BoxModel SelectedModel()
        {
            var selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<BoxModel>() : null;
        }
    }
}
