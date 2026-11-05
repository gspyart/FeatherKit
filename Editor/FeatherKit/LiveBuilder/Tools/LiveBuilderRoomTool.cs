using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
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
        private readonly LiveBuilderRoomGesture rectangleGesture = new();
        private readonly LiveBuilderPathGesture contourGesture = new(LiveBuilderPathKind.Room);


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Room";

        public override string DisplayName => "Комната";

        public override string Tooltip => "Пустая комната на высоту этажа: стены, пол, потолок. Прямоугольник — протяни по полу, по контуру — кликами точки. " +
                                          "В здании — только недостающие перегородки, внутри дома из коробок — вырез или стены";

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
            : "Протяни по полу — комната. Клик — выделить";


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
            return target is BoxModelRoom || IsContourRoom(target);
        }

        // Комната по контуру: в пустоте — здание в один этаж, в здании и в доме из коробок — замкнутая стена по линии.
        private static bool IsContourRoom(Component target)
        {
            if (target is SplineBuilding building)
                return building.StoreyCount == 1;

            if (target is not SplineWall wall || !wall.TryGetComponent<SplineContainer>(out var container))
                return false;

            foreach (var spline in container.Splines)
                if (spline.Closed)
                    return true;

            return false;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Форма новой комнаты и куда встают стены — меняют каждый раз.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Форма"))
            {
                LiveBuilderRoomSettings.Shape = (LiveBuilderRoomShape)LiveBuilderWindowControls.Switch(
                    new GUIContent("Рисовать", "Прямоугольник — протянул по полу. По контуру — кликами точки, любая форма; " +
                                               "в доме из коробок по контуру встают стены до его потолка, а не вырез"),
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

                LiveBuilderWindowControls.Explanation("Прямоугольник в доме из коробок вырезается — своих стен у него нет.");
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
