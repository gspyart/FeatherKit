using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки того, что выделено, в инструменте «Live Builder» (<see cref="LiveBuilderSceneTool"/>): каркасы коробок у объектов в выделении — всегда,
    /// а ручки — только у того, что правит выбранный инструмент (<see cref="LiveBuilderTool.CanEdit(UnityEngine.GameObject)"/>): у коробок, комнат, лестниц (<see cref="BoxModelPartHandles"/>),
    /// скосы рёбер коробок (<see cref="BoxModelChamferHandles"/>),
    /// вырезов (<see cref="GeometryCutterHandles"/>), стрелки переноса и копии (<see cref="LiveBuilderMoveHandle"/>),
    /// в режиме «Грани» — стрелки выделенных граней, ручка повтора у группы повтора.
    /// Контур стен и зданий — у <see cref="LiveBuilderContourHandles"/>.
    /// </summary>
    public static class LiveBuilderSelectionHandles
    {
        // areHandlesShown — false, пока инструмент рисует: видны только каркасы, ручки клик не перехватят.
        public static void Draw(LiveBuilderTool tool, bool areHandlesShown)
        {
            var models = new HashSet<BoxModel>();

            foreach (var selected in Selection.gameObjects)
            {
                var model = selected.GetComponentInParent<BoxModel>();

                if (model != null)
                    models.Add(model);
            }

            foreach (var model in models)
                BoxModelPartHandles.DrawWireframes(model);

            if (!areHandlesShown)
                return;

            foreach (var selected in Selection.gameObjects)
                if (selected.TryGetComponent<GeometryCutter>(out var cutter) && tool.CanEdit(cutter))
                    GeometryCutterHandles.Draw(cutter);

            if (BoxModelSelectionModeSetting.IsSelectingFaces && tool.CanEditFaces)
            {
                BoxModelFaceMoveHandle.DrawSelection();
                return;
            }

            DrawBoxHandles(tool);
            LiveBuilderMoveHandle.Draw(tool);

            if (tool.CanEditFaces)
                BoxModelFaceMoveHandle.DrawCollectedFaces();

            if (tool.CanEditRepeats)
                LiveBuilderRepeatHandle.DrawForSelection();
        }

        // Ручки на гранях у выделенных коробок, комнат и лестниц — если их правит инструмент.
        private static void DrawBoxHandles(LiveBuilderTool tool)
        {
            foreach (var selected in Selection.gameObjects)
            {
                var model = selected.GetComponentInParent<BoxModel>();

                if (model == null || !tool.CanEdit(selected))
                    continue;

                if (selected.TryGetComponent<BoxModelPart>(out var part))
                {
                    BoxModelChamferHandles.Draw(model, part);
                    BoxModelPartHandles.DrawHandles(model, part);
                }
                else if (selected.TryGetComponent<BoxModelRoom>(out var room))
                    BoxModelPartHandles.DrawHandles(model, room);
                else if (selected.TryGetComponent<BoxModelStairs>(out var stairs))
                    BoxModelPartHandles.DrawHandles(model, stairs);
            }
        }
    }
}
