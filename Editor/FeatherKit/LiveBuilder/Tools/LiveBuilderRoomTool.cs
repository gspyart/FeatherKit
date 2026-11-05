using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Комната»: пустая комната на высоту этажа — прямоугольником, протянув по полу (<see cref="LiveBuilderRoomGesture"/>),
    /// или по контуру, кликами точки (<see cref="LiveBuilderPathGesture"/>). Правит комнаты обоих видов:
    /// в пустоте по контуру — здание в один этаж, в здании и в доме из коробок — замкнутая стена по линии.
    /// </summary>
    public sealed class LiveBuilderRoomTool : LiveBuilderTool
    {
        private static readonly string[] RectangleKeyHints = { "протянуть по полу — комната, отпустил — готово", "что встанет — в подсказке внизу", "Esc — отмена" };

        private readonly LiveBuilderRoomGesture rectangleGesture = new();
        private readonly LiveBuilderPathGesture contourGesture = new(LiveBuilderPathKind.Room);

        // Что встанет прямоугольником под мышью — для подсказки; ещё не искали — null.
        private string placeUnderMouse;


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Room";

        public override string DisplayName => "Комната";

        public override string Tooltip => "Пустая комната на высоту этажа: стены, пол, потолок. Прямоугольник — протяни по полу, по контуру — кликами точки. " +
                                          "В здании — только недостающие перегородки, внутри дома из коробок — пустота или стены";

        public override int PaletteOrder => 2;

        // План комнаты с дверью: проём в стене и дуга створки.
        public override List<Vector2[]> IconLines => new()
        {
            Points(13, 4, 20, 4, 20, 20, 4, 20, 4, 4, 8, 4),
            Points(8, 4, 8, 9),
            Arc(new Vector2(8, 4), 5f, 90f, 0f)
        };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool PlacesIntoBuilding => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => rectangleGesture.IsActive || base.IsBusy;

        // По контуру — линия кликами; прямоугольником — своё рисование.
        public override LiveBuilderPathGesture PathGesture => LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour ? contourGesture : null;

        public override bool DrawsSurfaceHover => PathGesture == null && !rectangleGesture.IsActive;

        public override bool DrawsSnapCursor => true;

        public override string Hint => PathGesture != null ? PathHint(PathGesture)
            : rectangleGesture.IsActive ? rectangleGesture.Hint
            : "Протяни по полу — комната встанет " + (placeUnderMouse ?? "со своими стенами") + ". Клик — выделить";

        public override string[] KeyHints => PathGesture != null
            ? PathKeyHints
            : RectangleKeyHints;


        // ── Окно: редкие настройки ───────────────────────────────────────

        public override string MoreSettingsSummary => "толщины, перегородка, материалы";

        public override bool AreMoreSettingsChanged =>
            (LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour && LiveBuilderWallSettings.IsSmooth)
            || !Mathf.Approximately(LiveBuilderRoomSettings.WallThickness, LiveBuilderRoomSettings.FactoryWallThickness)
            || !Mathf.Approximately(LiveBuilderRoomSettings.SlabThickness, LiveBuilderRoomSettings.FactorySlabThickness)
            || LiveBuilderToolSettingsFields.IsPartitionChanged()
            || LiveBuilderRoomSettings.FloorMaterial != null || LiveBuilderRoomSettings.WallMaterial != null || LiveBuilderRoomSettings.CeilingMaterial != null;


        // ── Что правит: проверка ─────────────────────────────────────────

        // Комнаты обоих видов: прямоугольником и по контуру.
        protected override bool CanEditTarget(Component target)
        {
            return LiveBuilderRooms.IsRoom(target);
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Форма новой комнаты и куда встают стены — меняют каждый раз.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Форма"))
            {
                LiveBuilderRoomSettings.Shape = (LiveBuilderRoomShape)LiveBuilderWindowControls.Switch(
                    new GUIContent("Рисовать", "Прямоугольник — протянул по полу. По контуру — кликами точки, любая форма; " +
                                               "в доме из коробок по контуру встают стены до его потолка, а не пустота"),
                    (int)LiveBuilderRoomSettings.Shape, ("Прямоугольник", LiveBuilderOptionIcon.Rectangle), ("По контуру", LiveBuilderOptionIcon.Contour));

                LiveBuilderRoomSettings.WallSide = WallSideOf(LiveBuilderToolSettingsFields.DrawLineSideSwitch(LineSideOf(LiveBuilderRoomSettings.WallSide)));

                using (new EditorGUILayout.HorizontalScope())
                {
                    LiveBuilderWindowControls.PrefixLabel(new GUIContent("Есть", "У комнаты в пустоте: нажата — есть, бледная — нет. В доме и в здании пол и потолок — их"));
                    LiveBuilderRoomSettings.HasFloor = LiveBuilderWindowControls.IconToggle(LiveBuilderRoomSettings.HasFloor, LiveBuilderOptionIcon.Floor,
                        "Пол", "Плита пола под комнатой. Нет — комната стоит прямо на том, что под ней");
                    LiveBuilderRoomSettings.HasCeiling = LiveBuilderWindowControls.IconToggle(LiveBuilderRoomSettings.HasCeiling, LiveBuilderOptionIcon.Roof,
                        "Потолок", "Плита потолка над комнатой. Нет — сверху открыто: двор, крыша без парапета");
                }

                LiveBuilderWindowControls.Explanation("Прямоугольник в доме из коробок — пустота в нём: своих стен у неё нет.");
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


        // Угол или плавно у контура, толщины, перегородка и материалы.
        public override void DrawMoreSettings()
        {
            if (LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour)
                using (LiveBuilderWindowControls.Card("Контур"))
                    LiveBuilderToolSettingsFields.DrawKnotModeSwitch();

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderRoomSettings.WallThickness = EditorGUILayout.FloatField(
                    new GUIContent("Толщина стен, м", "Стены вокруг комнаты, нарисованной в пустоте. Внутри дома стены — то, что осталось от его объёма"),
                    LiveBuilderRoomSettings.WallThickness);

                LiveBuilderRoomSettings.SlabThickness = EditorGUILayout.FloatField(
                    new GUIContent("Толщина перекрытия, м", "Пол и потолок. Высота комнаты — этаж минус перекрытие"), LiveBuilderRoomSettings.SlabThickness);

                LiveBuilderWindowControls.Computed("Высота комнаты", LiveBuilderSceneText.FormatMeters(LiveBuilderRoomSettings.ClearHeight) + " м", "Этаж минус перекрытие");
                LiveBuilderToolSettingsFields.DrawPartitionThickness();
            }

            using (LiveBuilderWindowControls.Card("Материалы"))
            {
                LiveBuilderRoomSettings.FloorMaterial = LiveBuilderWindowControls.MaterialField("Пол", "Пусто — материал объекта", LiveBuilderRoomSettings.FloorMaterial);
                LiveBuilderRoomSettings.WallMaterial = LiveBuilderWindowControls.MaterialField("Стены", "Пусто — материал объекта", LiveBuilderRoomSettings.WallMaterial);
                LiveBuilderRoomSettings.CeilingMaterial = LiveBuilderWindowControls.MaterialField("Потолок", "Пусто — материал объекта", LiveBuilderRoomSettings.CeilingMaterial);
            }
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Прямоугольником, пока не начат, — что встанет под мышью: перегородки здания, комната из коробок или вырез в доме.
        // Сюрпризов «нарисовал комнату, а встали перегородки» нет: подсказка говорит заранее.
        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            if (PathGesture != null)
                return base.OnMouseMove(worldRay, current);

            placeUnderMouse = rectangleGesture.IsActive ? null : LiveBuilderFloorRectangle.DescribeTargetAt(worldRay);
            return false;
        }


        // По контуру мышь и клавиши ведут линию — крючки основы; здесь — прямоугольник.
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


        // ── Окно сцены: отмена и вид ─────────────────────────────────────

        // Обе формы: форму могли переключить, пока рисуешь.
        public override void Cancel()
        {
            rectangleGesture.Cancel();
            contourGesture.Finish();
        }


        // Ctrl+Z: прямоугольник бросаем, а в контуре он убирает последнюю точку — его не трогаем.
        public override void CancelAfterUndo()
        {
            rectangleGesture.Cancel();
        }


        // Как встанет рисуемая комната.
        public override void DrawPreview()
        {
            if (PathGesture != null)
                base.DrawPreview();
            else
                rectangleGesture.DrawPreview();
        }
    }
}
