using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Режим Room инструмента Box: пустая комната на высоту этажа — прямоугольником, протянув по полу (<see cref="LiveBuilderRoomGesture"/>).
    /// Комната по контуру — это Wall (замкнуть) или Building в один этаж: подсказка говорит об этом, а не рисует.
    /// </summary>
    public sealed class LiveBuilderRoomBoxMode : LiveBuilderBoxToolMode
    {
        private readonly LiveBuilderRoomGesture rectangleGesture = new();

        // Что встанет прямоугольником под мышью — для подсказки; ещё не искали — null.
        private string placeUnderMouse;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => rectangleGesture.IsActive;

        public override LiveBuilderDrawingReport DrawingReport => rectangleGesture.Report();

        public override bool DrawsSurfaceHover => !rectangleGesture.IsActive;

        public override string Hint => rectangleGesture.IsActive
            ? rectangleGesture.Hint
            : "Drag on the floor — a room " + (placeUnderMouse ?? "with its own walls") + " · by outline — Wall (closed) or Building, 1 storey";

        public override string[] KeyHints => new[]
        {
            "While dragging: 4 " + LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.Times) + " 3 — exact sides",
            "Release — done " + LiveBuilderHintKeys.Esc + " — cancel"
        };


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "thicknesses, partition, materials";

        public override bool AreMoreSettingsChanged =>
            !Mathf.Approximately(LiveBuilderRoomSettings.WallThickness, LiveBuilderRoomSettings.FactoryWallThickness)
            || !Mathf.Approximately(LiveBuilderRoomSettings.SlabThickness, LiveBuilderRoomSettings.FactorySlabThickness)
            || LiveBuilderToolSettingsFields.IsPartitionChanged()
            || LiveBuilderRoomSettings.FloorMaterial != null || LiveBuilderRoomSettings.WallMaterial != null || LiveBuilderRoomSettings.CeilingMaterial != null;


        // ── Окно ─────────────────────────────────────────────────────────

        // Куда встают стены и есть ли пол и потолок — меняют каждый раз.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Room"))
            {
                LiveBuilderRoomSettings.WallSide = WallSideOf(LiveBuilderToolSettingsFields.DrawLineSideSwitch(LineSideOf(LiveBuilderRoomSettings.WallSide)));

                using (new EditorGUILayout.HorizontalScope())
                {
                    LiveBuilderWindowControls.PrefixLabel(new GUIContent("Has", "У комнаты в пустоте: нажата — есть, бледная — нет. В доме и в здании пол и потолок — их"));
                    LiveBuilderRoomSettings.HasFloor = LiveBuilderWindowControls.IconToggle(LiveBuilderRoomSettings.HasFloor, LiveBuilderOptionIcon.Floor,
                        "Floor", "Плита пола под комнатой. Нет — комната стоит прямо на том, что под ней");
                    LiveBuilderRoomSettings.HasCeiling = LiveBuilderWindowControls.IconToggle(LiveBuilderRoomSettings.HasCeiling, LiveBuilderOptionIcon.Roof,
                        "Ceiling", "Плита потолка над комнатой. Нет — сверху открыто: двор, крыша без парапета");
                }
            }
        }

        // Куда встают стены ↔ где линия относительно них, как у здания: стены внутрь — линия снаружи, стены наружу — линия изнутри.
        private static int LineSideOf(LiveBuilderRoomWallSide wallSide)
        {
            return wallSide switch
            {
                LiveBuilderRoomWallSide.Inward => 0,
                LiveBuilderRoomWallSide.Outward => 1,
                _ => 2
            };
        }

        private static LiveBuilderRoomWallSide WallSideOf(int lineSide)
        {
            return lineSide switch
            {
                0 => LiveBuilderRoomWallSide.Inward,
                1 => LiveBuilderRoomWallSide.Outward,
                _ => LiveBuilderRoomWallSide.Centered
            };
        }


        // Толщины, перегородка и материалы.
        public override void DrawMoreSettings()
        {
            using (LiveBuilderWindowControls.Card("Size"))
            {
                LiveBuilderRoomSettings.WallThickness = EditorGUILayout.FloatField(
                    new GUIContent("Wall thickness, m", "Стены вокруг комнаты, нарисованной в пустоте. Внутри дома стены — то, что осталось от его объёма"),
                    LiveBuilderRoomSettings.WallThickness);

                LiveBuilderRoomSettings.SlabThickness = EditorGUILayout.FloatField(
                    new GUIContent("Slab thickness, m", "Пол и потолок. Высота комнаты — этаж минус перекрытие"), LiveBuilderRoomSettings.SlabThickness);

                LiveBuilderWindowControls.Computed("Room height", LiveBuilderSceneText.FormatMeters(LiveBuilderRoomSettings.ClearHeight) + " m", "Этаж минус перекрытие");
                LiveBuilderToolSettingsFields.DrawPartitionThickness();
            }

            using (LiveBuilderWindowControls.Card("Materials"))
            {
                LiveBuilderRoomSettings.FloorMaterial = LiveBuilderWindowControls.MaterialField("Floor", "Пусто — материал объекта", LiveBuilderRoomSettings.FloorMaterial);
                LiveBuilderRoomSettings.WallMaterial = LiveBuilderWindowControls.MaterialField("Walls", "Пусто — материал объекта", LiveBuilderRoomSettings.WallMaterial);
                LiveBuilderRoomSettings.CeilingMaterial = LiveBuilderWindowControls.MaterialField("Ceiling", "Пусто — материал объекта", LiveBuilderRoomSettings.CeilingMaterial);
            }
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Пока не начат — что встанет под мышью: комната в здании, из коробок или пустота в доме. Подсказка говорит заранее.
        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            placeUnderMouse = rectangleGesture.IsActive ? null : LiveBuilderFloorRectangle.DescribeTargetAt(worldRay);
            return false;
        }


        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            if (canBeginGesture && !rectangleGesture.IsActive)
                rectangleGesture.TryBegin(downRay);

            if (rectangleGesture.IsActive)
                rectangleGesture.Update(worldRay);
        }


        // Отпустил прямоугольник — комната готова.
        public override bool OnMouseUp(Event current)
        {
            if (!rectangleGesture.IsActive)
                return false;

            rectangleGesture.Commit();
            return true;
        }


        // ── Окно сцены: клавиши, отмена, вид ─────────────────────────────

        public override bool OnKeyDown(Event current)
        {
            return rectangleGesture.IsActive && rectangleGesture.HandleKey(current.keyCode);
        }


        public override void Cancel()
        {
            rectangleGesture.Cancel();
        }


        public override void DrawPreview()
        {
            rectangleGesture.DrawPreview();
        }
    }
}
