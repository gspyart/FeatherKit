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

        public override bool IsBusy => rectangleGesture.IsActive;

        // По контуру — линия кликами; прямоугольником — своё рисование.
        public override LiveBuilderPathGesture PathGesture => LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour ? contourGesture : null;

        public override bool DrawsSurfaceHover => PathGesture == null && !rectangleGesture.IsActive;

        public override bool DrawsSnapCursor => true;

        public override string Hint => PathGesture != null ? PathHint(PathGesture)
            : rectangleGesture.IsActive ? rectangleGesture.Hint
            : "Протяни по полу — комната. Клик — выделить";


        // ── Что правит ───────────────────────────────────────────────────

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

        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Форма"))
            {
                LiveBuilderRoomSettings.Shape = (LiveBuilderRoomShape)LiveBuilderWindowControls.Switch(
                    new GUIContent("Рисовать", "Прямоугольник — протянул по полу. По контуру — кликами точки, любая форма; " +
                                               "в доме из коробок по контуру встают стены до его потолка, а не вырез"),
                    (int)LiveBuilderRoomSettings.Shape, ("Прямоугольник", LiveBuilderOptionIcon.Rectangle), ("По контуру", LiveBuilderOptionIcon.Contour));

                if (LiveBuilderRoomSettings.Shape == LiveBuilderRoomShape.Contour)
                    LiveBuilderToolSettingsFields.DrawKnotModeSwitch();

                LiveBuilderRoomSettings.WallSide = (LiveBuilderRoomWallSide)LiveBuilderWindowControls.Switch(
                    new GUIContent("Стены", "Куда встают стены от того, что рисуешь. Наружу — нарисованное и есть комната изнутри. " +
                                            "Внутрь — нарисованное — габарит вместе со стенами. По центру — линия идёт по середине стен. " +
                                            "Прямоугольник в доме из коробок вырезается — своих стен у него нет"),
                    (int)LiveBuilderRoomSettings.WallSide, "Наружу", "Внутрь", "По центру");
            }

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderRoomSettings.WallThickness = EditorGUILayout.FloatField(
                    new GUIContent("Толщина стен, м", "Стены вокруг комнаты, нарисованной в пустоте. Внутри дома стены — то, что осталось от его объёма"),
                    LiveBuilderRoomSettings.WallThickness);

                LiveBuilderRoomSettings.SlabThickness = EditorGUILayout.FloatField(
                    new GUIContent("Толщина перекрытия, м", "Пол и потолок. Высота комнаты — этаж минус перекрытие"), LiveBuilderRoomSettings.SlabThickness);

                LiveBuilderWindowControls.Computed("Высота комнаты", BoxModelSceneText.FormatMeters(LiveBuilderRoomSettings.ClearHeight) + " м", "Этаж минус перекрытие");
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


        public override void DrawPreview()
        {
            if (PathGesture != null)
                base.DrawPreview();
            else
                rectangleGesture.DrawPreview();
        }
    }
}
