using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Режим Room «Коробки»: протянул прямоугольник на полу — комната (<see cref="BoxModelRoom"/>) на высоту этажа (этаж минус перекрытие).
    /// - в пустоте — со стенами, полом и потолком вокруг; куда встают стены от нарисованного — по настройке «Линия по стенам»
    /// - в доме из коробок — только пустота: стены — то, что осталось от его объёма
    /// - в здании — в его этаж: стены толщиной перегородки, на его стенах не строятся; пол и потолок — его перекрытия
    /// Пока тянешь, объект уже показан с комнатой; толщины и материалы — из <see cref="LiveBuilderRoomSettings"/>.
    /// </summary>
    public sealed class LiveBuilderRoomGesture
    {
        // Уже этого комната не станет, сколько бы стены ни отступали внутрь, м.
        private const float MinRoomWidth = 0.1f;

        private readonly LiveBuilderFloorRectangle rectangle = new();
        private readonly BoxModelPreview preview = new();


        public bool IsActive => rectangle.IsActive;

        public string Hint => rectangle.TypedText.Length > 0
            ? "Typed " + rectangle.TypedText + " m " + LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.Times) + " other side " + LiveBuilderHintKeys.Key("release") +
              " exactly this " + LiveBuilderHintKeys.Esc + " clear typing"
            : (rectangle.BuildingContext != null
                  ? "Drag — a room, walls only where the building has none, release — done "
                  : rectangle.IsInsideModel
                      ? "Drag — a room cut into the house, release — done "
                      : "Drag — a room, walls go " + WallSideText + ", release — done ") +
              LiveBuilderHintKeys.Key("4 X 3") + " exact " + LiveBuilderHintKeys.Esc + " cancel";

        private static string WallSideText => LiveBuilderRoomSettings.WallSide switch
        {
            LiveBuilderRoomWallSide.Inward => "inward",
            LiveBuilderRoomWallSide.Centered => "centered on the line",
            _ => "outside"
        };

        // Стены комнаты в здании — как перегородка: тип или своя толщина.
        private static float PartitionThickness => LiveBuilderWallSettings.PartitionWallType != null
            ? LiveBuilderWallSettings.PartitionWallType.Thickness
            : LiveBuilderWallSettings.PartitionThickness;


        // ── Жест ─────────────────────────────────────────────────────────

        // Пока тянешь: цифры и X — точные стороны (<see cref="LiveBuilderFloorRectangle.HandleKey"/>).
        public bool HandleKey(KeyCode key)
        {
            return rectangle.HandleKey(key);
        }


        public bool TryBegin(Ray worldRay)
        {
            return rectangle.TryBegin(worldRay);
        }


        public void Update(Ray worldRay)
        {
            rectangle.Update(worldRay);

            if (!rectangle.HasArea || rectangle.BuildingContext != null)
            {
                preview.Hide();
                return;
            }

            var boxes = BuildPreviewBoxes();
            preview.Show(rectangle.TargetModel, mesh => BoxModelRebuilder.FillPreviewMesh(mesh, rectangle.TargetModel, boxes));
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
            var min = RoomMin();
            var size = RoomSize();

            if (model == null)
            {
                var shell = ShellThickness();
                model = BoxModelFactory.CreateModel(rectangle.ToWorld(min - shell));
                min = shell;
            }

            var room = BoxModelFactory.CreateRoom(model, min, size, !rectangle.IsInsideModel);
            Selection.activeGameObject = room.gameObject;
            BoxModelRebuilder.Rebuild(model, LiveBuilderBuildQuality.Draft);
            Cancel();
        }


        public void Cancel()
        {
            preview.Clear();
            rectangle.Cancel();
        }


        // ── Комната в пустоте и в доме ───────────────────────────────────

        // Для предпросмотра, в осях рамки: те же пустота и оболочка, что даст комната (<see cref="BoxModelRoomShell"/>).
        private List<BoxModelSolverBox> BuildPreviewBoxes()
        {
            var hasShell = !rectangle.IsInsideModel;
            var sides = BoxModelRoomShell.SidesFor(hasShell, hasShell && LiveBuilderRoomSettings.HasFloor, hasShell && LiveBuilderRoomSettings.HasCeiling);
            var inside = new BoxModelSideMaterials(null, LiveBuilderRoomSettings.FloorMaterial, LiveBuilderRoomSettings.CeilingMaterial,
                LiveBuilderRoomSettings.WallMaterial);
            return BoxModelRoomShell.PreviewBoxesOf(RoomMin(), RoomSize(), LiveBuilderRoomSettings.WallThickness, LiveBuilderRoomSettings.SlabThickness, sides, inside);
        }

        // Угол комнаты изнутри: стены внутрь или по центру — отступ от нарисованного.
        private Vector3 RoomMin()
        {
            var inset = WallInset();
            return rectangle.Min + new Vector3(inset.x, 0f, inset.y);
        }

        // Внутренний размер: нарисованное без отступа стен, на высоту этажа минус перекрытие.
        private Vector3 RoomSize()
        {
            var inset = WallInset();
            var size = rectangle.Max - rectangle.Min;
            size.x -= inset.x * 2f;
            size.z -= inset.y * 2f;
            size.y = LiveBuilderRoomSettings.ClearHeight;
            return size;
        }

        // Отступ комнаты от нарисованного по X и Z, м. В доме своих стен нет — и отступа нет; комната не уже 0,1 м.
        private Vector2 WallInset()
        {
            if (rectangle.IsInsideModel)
                return Vector2.zero;

            var inset = LiveBuilderRoomWalls.InsetOf(LiveBuilderRoomSettings.WallSide, LiveBuilderRoomSettings.WallThickness);
            var drawn = rectangle.Max - rectangle.Min;
            return new Vector2(Mathf.Clamp(inset, 0f, (drawn.x - MinRoomWidth) / 2f), Mathf.Clamp(inset, 0f, (drawn.z - MinRoomWidth) / 2f));
        }

        // Толщина стен и перекрытий вокруг комнаты в пустоте.
        private static Vector3 ShellThickness()
        {
            return new Vector3(LiveBuilderRoomSettings.WallThickness, LiveBuilderRoomSettings.SlabThickness, LiveBuilderRoomSettings.WallThickness);
        }


        // ── Комната в здании ─────────────────────────────────────────────

        // Сторона на стене здания — комната до неё; свободная — отступ стен по настройке «Линия по стенам», как у прямоугольника в пустоте.
        private void CommitInBuilding(LiveBuilderBuildingContext context)
        {
            GetRoomOutline(context, out var corners, out var isSideFree);
            var inset = LiveBuilderRoomWalls.InsetOf(LiveBuilderRoomSettings.WallSide, PartitionThickness);
            var min = corners[0];
            var max = corners[2];

            // Стороны по кругу: −Z, +X, +Z, −X.
            min.z += isSideFree[0] ? inset : 0f;
            max.x -= isSideFree[1] ? inset : 0f;
            max.z -= isSideFree[2] ? inset : 0f;
            min.x += isSideFree[3] ? inset : 0f;

            var planSize = new Vector2(Mathf.Max(MinRoomWidth, max.x - min.x), Mathf.Max(MinRoomWidth, max.z - min.z));
            var room = BoxModelFactory.CreateRoomInBuilding(context, min, planSize);

            if (room == null)
                return;

            Selection.activeGameObject = room.gameObject;
            BoxModelRebuilder.Rebuild(room.GetComponentInParent<BoxModel>(), LiveBuilderBuildQuality.Draft);
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


        // ── Отчёт для панели ─────────────────────────────────────────────

        // Нарисованные стороны и высота комнаты для секции Drawing (<see cref="LiveBuilderDrawingReport"/>); жест в один шаг. Не рисуешь — null.
        public LiveBuilderDrawingReport Report()
        {
            if (!rectangle.IsActive)
                return null;

            var drawn = rectangle.Max - rectangle.Min;
            var isTyped = rectangle.AreSidesTyped;

            return new LiveBuilderDrawingReport("Room", 1, 1, "Rectangle")
                .WithSize("Width", drawn.x, isTyped)
                .WithSize("Length", drawn.z, isTyped)
                .WithSize("Height", LiveBuilderRoomSettings.ClearHeight)
                .WithTyped(rectangle.TypedText)
                .WithKeys(null, rectangle.TypedText.Length > 0 ? "clear typing" : "drop the room");
        }


        // ── Рамка ────────────────────────────────────────────────────────

        // Прямоугольник на полу и размеры комнаты; в здании — будущие стены сплошной линией, стороны на его стенах — пунктиром.
        public void DrawPreview()
        {
            if (!rectangle.IsActive)
                return;

            if (rectangle.BuildingContext != null)
            {
                DrawBuildingRoomPreview(rectangle.BuildingContext);
                return;
            }

            using (new Handles.DrawingScope(rectangle.Plane.FrameToWorld))
            {
                var min = RoomMin();
                var size = RoomSize();
                var max = min + size;
                Handles.color = BoxModelPartHandles.CutColor;
                Handles.DrawWireCube(min + size / 2f, size);
                LiveBuilderSceneText.DrawLabel(max, "room " + LiveBuilderSceneText.FormatMeters(size.x) + " × " + LiveBuilderSceneText.FormatMeters(size.z) +
                                                    " m, height " + LiveBuilderSceneText.FormatMeters(size.y) + " m");
            }
        }

        private void DrawBuildingRoomPreview(LiveBuilderBuildingContext context)
        {
            if (!rectangle.HasArea)
                return;

            GetRoomOutline(context, out var corners, out var isSideFree);

            using (new Handles.DrawingScope(context.Building.transform.localToWorldMatrix))
            {
                var height = Vector3.up * context.Building.ClearHeight;
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
                LiveBuilderSceneText.DrawLabel(corners[2] + height, LiveBuilderSceneText.FormatMeters(size.x) + " × " + LiveBuilderSceneText.FormatMeters(size.z) +
                                                                    " m, walls: " + wallCount + (context.CopyCount > 1 ? ", × " + context.CopyCount + " storeys" : ""));
            }
        }
    }
}
