using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент «Комната»: протянул прямоугольник на полу — комната (<see cref="BoxModelRoom"/>) на высоту этажа (этаж минус перекрытие).
    /// Внутри дома (пол под потолком того же объекта или сплошной объём) — без стен вокруг: комната вырезается в его объёме.
    /// В пустоте — со стенами, полом и потолком вокруг. Толщины и материалы — из <see cref="LiveBuilderRoomSettings"/>;
    /// потом всё это — числа самой комнаты. Пока тянешь, объект уже показан с комнатой.
    /// В здании по контуру — не коробки, а одна замкнутая перегородка-комната (стена по линии): стороны, что легли на его стены, не строятся,
    /// но остаются в контуре — поменяешь потом форму комнаты или здания, они пересчитаются. Высота — до потолка его этажа, на этом этаже или на всех выше.
    /// </summary>
    public sealed class LiveBuilderRoomGesture
    {
        private readonly LiveBuilderFloorRectangle rectangle = new();
        private readonly BoxModelPreview preview = new();


        public bool IsActive => rectangle.IsActive;

        public string Hint => rectangle.BuildingContext != null
            ? "Тяни — комната, встанут только недостающие перегородки. Отпусти — готово"
            : rectangle.IsInsideModel
                ? "Тяни — размер комнаты, она вырежется в доме. Отпусти — готово. Esc — отмена"
                : "Тяни — размер комнаты, стены вокруг добавятся. Отпусти — готово. Esc — отмена";


        // ── Жест ─────────────────────────────────────────────────────────

        public bool TryBegin(Ray worldRay)
        {
            return rectangle.TryBegin(worldRay);
        }


        public void Update(Ray worldRay)
        {
            rectangle.Update(worldRay);

            if (!rectangle.HasArea || rectangle.BuildingContext != null)
            {
                preview.Clear();
                return;
            }

            var mesh = BoxModelRebuilder.BuildPreviewMesh(rectangle.TargetModel, BuildBoxes(), out var materials);
            preview.Show(rectangle.TargetModel, mesh, materials);
        }


        // Кладёт комнату и выделяет её. Нет площади — ничего.
        public void Commit()
        {
            if (!rectangle.HasArea)
            {
                Cancel();
                return;
            }

            Undo.SetCurrentGroupName("Create Room");

            if (rectangle.BuildingContext != null)
            {
                CommitInBuilding(rectangle.BuildingContext);
                Cancel();
                return;
            }

            var model = rectangle.TargetModel;
            var min = rectangle.Min;
            var size = RoomSize();
            var hasWalls = !rectangle.IsInsideModel;

            if (model == null)
            {
                var shell = ShellThickness();
                model = BoxModelFactory.CreateModel(rectangle.ToWorld(min - shell));
                min = shell;
            }

            var room = BoxModelFactory.CreateRoom(model, min, size, hasWalls);
            Selection.activeGameObject = room.gameObject;
            BoxModelRebuilder.Rebuild(model);
            Cancel();
        }


        public void Cancel()
        {
            preview.Clear();
            rectangle.Cancel();
        }


        // ── Коробки комнаты ──────────────────────────────────────────────

        // Для предпросмотра, в осях рамки: стены вокруг (в пустоте) и сама комната — вырезающая коробка от пола до потолка.
        private List<BoxModelSolverBox> BuildBoxes()
        {
            var boxes = new List<BoxModelSolverBox>();
            var min = rectangle.Min;
            var size = RoomSize();
            var materials = new BoxModelSideMaterials(null, LiveBuilderRoomSettings.FloorMaterial, LiveBuilderRoomSettings.CeilingMaterial,
                LiveBuilderRoomSettings.WallMaterial);

            if (!rectangle.IsInsideModel)
            {
                var shell = ShellThickness();
                boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(min - shell, size + shell * 2f), BoxModelPartMode.Add, null, default));
            }

            boxes.Add(new BoxModelSolverBox(MillimeterBox.FromMeters(min, size), BoxModelPartMode.Cut, null, materials));
            return boxes;
        }

        // Внутренний размер: прямоугольник на высоту этажа минус перекрытие.
        private Vector3 RoomSize()
        {
            var size = rectangle.Max - rectangle.Min;
            size.y = LiveBuilderRoomSettings.ClearHeight;
            return size;
        }

        // Толщина стен и перекрытий вокруг комнаты в пустоте.
        private static Vector3 ShellThickness()
        {
            return new Vector3(LiveBuilderRoomSettings.WallThickness, LiveBuilderRoomSettings.SlabThickness, LiveBuilderRoomSettings.WallThickness);
        }


        // ── Комната в здании ─────────────────────────────────────────────

        // Одна замкнутая перегородка-комната по углам прямоугольника: стороны вдоль стен здания не строятся, но остаются в контуре.
        private void CommitInBuilding(LiveBuilderBuildingContext context)
        {
            GetRoomOutline(context, out var corners, out _);
            var buildingTransform = context.Building.transform;
            var room = BoxModelFactory.CreatePartition(context, buildingTransform.TransformPoint(corners[0]), "Room");
            room.SkipsSidesAlongBuildingWalls = true;

            var spline = room.GetComponent<SplineContainer>().Spline;

            foreach (var corner in corners)
                spline.Add(new BezierKnot(room.transform.InverseTransformPoint(buildingTransform.TransformPoint(corner))), TangentMode.Linear);

            spline.Closed = true;
            SplineWallRebuilder.Rebuild(room);
            Selection.activeGameObject = room.gameObject;
        }

        // Углы прямоугольника по кругу, в осях здания, и какие стороны свободны — не лежат на стенах здания.
        private void GetRoomOutline(LiveBuilderBuildingContext context, out Vector3[] corners, out bool[] isSideFree)
        {
            var min = rectangle.Min;
            var max = rectangle.Max;
            var contours = context.InnerContours();
            corners = new[] { new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z) };
            isSideFree = new bool[4];

            for (var side = 0; side < 4; side++)
                isSideFree[side] = !SplineWallBuildingSides.IsAlongContours(corners[side], corners[(side + 1) % 4], contours);
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Прямоугольник на полу и размеры комнаты; в здании — будущие перегородки сплошной линией, стороны вдоль его стен — пунктиром.
        public void DrawPreview()
        {
            if (!rectangle.IsActive)
                return;

            if (rectangle.BuildingContext != null)
            {
                DrawPartitionsPreview(rectangle.BuildingContext);
                return;
            }

            using (new Handles.DrawingScope(rectangle.Plane.FrameToWorld))
            {
                var min = rectangle.Min;
                var max = rectangle.Max;
                var size = max - min;
                Handles.color = BoxModelPartHandles.CutColor;
                Handles.DrawWireCube(new Vector3((min.x + max.x) / 2f, min.y + LiveBuilderRoomSettings.ClearHeight / 2f, (min.z + max.z) / 2f),
                    new Vector3(size.x, LiveBuilderRoomSettings.ClearHeight, size.z));
                BoxModelSceneText.DrawLabel(max, BoxModelSceneText.FormatMeters(size.x) + " × " + BoxModelSceneText.FormatMeters(size.z) +
                                                 " м, высота " + BoxModelSceneText.FormatMeters(LiveBuilderRoomSettings.ClearHeight) + " м");
            }
        }

        private void DrawPartitionsPreview(LiveBuilderBuildingContext context)
        {
            if (!rectangle.HasArea)
                return;

            GetRoomOutline(context, out var corners, out var isSideFree);

            using (new Handles.DrawingScope(context.Building.transform.localToWorldMatrix))
            {
                var height = Vector3.up * context.ClearHeight;
                var wallCount = 0;

                for (var side = 0; side < 4; side++)
                {
                    var start = corners[side];
                    var end = corners[(side + 1) % 4];

                    if (isSideFree[side])
                    {
                        Handles.color = BoxModelPartHandles.AddColor;
                        Handles.DrawLine(start, end, 3f);
                        Handles.DrawLine(start + height, end + height, 2f);
                        Handles.DrawLine(start, start + height, 2f);
                        Handles.DrawLine(end, end + height, 2f);
                        wallCount++;
                    }
                    else
                    {
                        Handles.color = new Color(1f, 1f, 1f, 0.6f);
                        Handles.DrawDottedLine(start, end, 4f);
                    }
                }

                var size = rectangle.Max - rectangle.Min;
                BoxModelSceneText.DrawLabel(corners[2] + height, BoxModelSceneText.FormatMeters(size.x) + " × " + BoxModelSceneText.FormatMeters(size.z) +
                                                                 " м, перегородок: " + wallCount + (context.CopyCount > 1 ? ", × " + context.CopyCount + " этажа" : ""));
            }
        }
    }
}
