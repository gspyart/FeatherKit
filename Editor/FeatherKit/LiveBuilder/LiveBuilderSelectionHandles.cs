using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Ручки того, что выделено, в инструменте «Live Builder» (<see cref="BoxModelSceneTool"/>): каркасы коробок у объектов в выделении — всегда,
    /// а ручки — только у того, что правит выбранный инструмент (<see cref="LiveBuilderToolScope"/>): у коробок, комнат, лестниц (<see cref="BoxModelPartHandles"/>),
    /// вырезов (<see cref="GeometryCutterHandles"/>), в режиме «Грани» — стрелки выделенных граней, ручка повтора у группы повтора.
    /// Контур стен и зданий — у <see cref="LiveBuilderContourHandles"/>.
    /// </summary>
    public static class LiveBuilderSelectionHandles
    {
        public static void Draw(LiveBuilderTool tool)
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

            foreach (var selected in Selection.gameObjects)
                if (selected.TryGetComponent<GeometryCutter>(out var cutter) && LiveBuilderToolScope.CanEdit(tool, cutter))
                    GeometryCutterHandles.Draw(cutter);

            if (BoxModelSelectionModeSetting.IsSelectingFaces && LiveBuilderToolScope.CanEditFaces(tool))
            {
                BoxModelFaceMoveHandle.DrawSelection();
                return;
            }

            DrawBoxHandles(tool);

            if (LiveBuilderToolScope.CanEditFaces(tool))
                BoxModelFaceMoveHandle.DrawCollectedFaces();

            if (LiveBuilderToolScope.CanEditRepeats(tool))
                LiveBuilderRepeatHandle.DrawForSelection();
        }

        // Ручки на гранях у выделенных коробок, комнат и лестниц — если их правит инструмент.
        private static void DrawBoxHandles(LiveBuilderTool tool)
        {
            foreach (var selected in Selection.gameObjects)
            {
                var model = selected.GetComponentInParent<BoxModel>();

                if (model == null || !LiveBuilderToolScope.CanEdit(tool, selected))
                    continue;

                if (selected.TryGetComponent<BoxModelPart>(out var part))
                    BoxModelPartHandles.DrawHandles(model, part);
                else if (selected.TryGetComponent<BoxModelRoom>(out var room))
                    BoxModelPartHandles.DrawHandles(model, room);
                else if (selected.TryGetComponent<BoxModelStairs>(out var stairs))
                    BoxModelPartHandles.DrawHandles(model, stairs);
            }
        }
    }
}
