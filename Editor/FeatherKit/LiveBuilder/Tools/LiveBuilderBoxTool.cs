using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Коробка»: протянул прямоугольник, мышью глубина, клик — коробка (<see cref="BoxModelDrawGesture"/>);
    /// на косой поверхности — коробка по её нормали (<see cref="LiveBuilderSurfaceBoxGesture"/>). Правит только коробки и их грани.
    /// Здесь же — «новый объект»: следующая коробка отдельным объектом, даже поверх другого; на полу здания — вложенным объектом в нём.
    /// </summary>
    public sealed class LiveBuilderBoxTool : LiveBuilderTool
    {
        private const string IdleHint = "Протяни по поверхности — новая коробка. Клик по грани — выделить коробку";
        private const string NewModelHint = "Новый объект: протяни прямоугольник на поверхности или земле; на полу здания — встанет в здание. Esc — отмена";

        private readonly BoxModelDrawGesture drawGesture = new();
        private readonly LiveBuilderSurfaceBoxGesture surfaceBoxGesture = new();


        // Кнопка «Новый»: следующая нарисованная коробка — отдельный новый объект, даже если рисуешь на другом объекте из коробок.
        public static bool IsNewModelRequested { get; set; }


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Box";

        public override string DisplayName => "Коробка";

        public override string Tooltip => "Протяни по поверхности, мышью глубина, клик — коробка. Клик по грани — выделить коробку; правит только коробки";

        public override int PaletteOrder => 1;

        // Кубик в изометрии.
        public override List<Vector2[]> IconLines => new()
        {
            Points(12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f, 12, 3),
            Points(4, 7.5f, 12, 12, 20, 7.5f),
            Points(12, 12, 12, 21)
        };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool CanEditFaces => true;

        public override bool CanEditRepeats => true;

        public override bool PlacesIntoBuilding => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => drawGesture.IsActive || surfaceBoxGesture.IsActive || IsNewModelRequested;

        public override bool DrawsSurfaceHover => !drawGesture.IsActive;

        public override bool DrawsSnapCursor => true;

        // Что рисуешь сейчас, иначе — про грани или выделенную коробку.
        public override string Hint
        {
            get
            {
                if (drawGesture.IsActive)
                    return drawGesture.Hint;

                if (surfaceBoxGesture.IsActive)
                    return surfaceBoxGesture.Hint;

                if (IsNewModelRequested)
                    return NewModelHint;

                if (BoxModelSelectionModeSetting.IsSelectingFaces)
                    return FaceSelectionHint + ". Протяни — новая коробка";

                return IsBoxSelected ? BoxModelPartExtrusion.Hint : IdleHint;
            }
        }

        public override bool ShowsDepthClickHint => true;


        // ── Что правит: проверка ─────────────────────────────────────────

        // Только коробки.
        protected override bool CanEditTarget(Component target)
        {
            return target is BoxModelPart;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Режим рисования, что выделяет клик и куда встанет коробка.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Рисование"))
            {
                var drawMode = (BoxModelDrawMode)LiveBuilderWindowControls.Switch(
                    new GUIContent("Режим", "Что делает нарисованная коробка. Авто — наружу добавляет, внутрь вырезает"),
                    (int)BoxModelDrawModeSetting.Current,
                    ("Авто", LiveBuilderOptionIcon.BoxAuto), ("Добавить", LiveBuilderOptionIcon.BoxAdd), ("Вырезать", LiveBuilderOptionIcon.BoxCut));

                if (drawMode != BoxModelDrawModeSetting.Current)
                    BoxModelDrawModeSetting.Current = drawMode;

                LiveBuilderToolSettingsFields.DrawSelectionModeSwitch();
                DrawTargetModel();
            }

            LiveBuilderWindowControls.Explanation("Box", "Протяни по поверхности, мышью глубина, клик — готово. Клик по грани — выделить коробку или кусок грани, " +
                                           "смотря что выбрано в «Клик выделяет». Здесь правятся только коробки; остальное — в своих инструментах или в «Выделении».");
        }

        // Куда встанет коробка на пустом месте: в выделенный объект, иначе — новый. По чужой поверхности — всегда в тот объект.
        // «Новый объект» нажат — следующая коробка точно отдельным объектом; нажать ещё раз — отмена.
        private static void DrawTargetModel()
        {
            var selectedModel = BoxModelActions.SelectedModel();
            var isNewRequested = IsNewModelRequested;
            var targetName = isNewRequested ? "новый, отдельный" : selectedModel != null ? selectedModel.name : "новый (ничего не выделено)";

            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.PrefixLabel(new GUIContent("Объект", "Куда встанет коробка, нарисованная на пустом месте. " +
                                                                     "Рисуешь по поверхности другого объекта — она встанет в него"));
                LiveBuilderWindowControls.ValueChip(new GUIContent(targetName, targetName), GUILayout.MinWidth(40f));

                var isPressed = LiveBuilderWindowControls.IconToggle(isNewRequested, LiveBuilderOptionIcon.NewObject, "Новый",
                    "Следующая коробка — отдельный объект, даже поверх другого. На полу здания — вложенный объект в нём, привязанный к этажу " +
                    "(шахта, лестничная клетка). Нажать ещё раз — отмена");

                if (isPressed == isNewRequested)
                    return;

                if (isPressed)
                    BoxModelActions.BeginNewModelDrawing();
                else
                    IsNewModelRequested = false;

                SceneView.RepaintAll();
            }
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Выбираешь глубину — мышь её и задаёт.
        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            if (drawGesture.IsChoosingDepth)
                drawGesture.UpdateDepth(current.mousePosition);
            else if (surfaceBoxGesture.IsChoosingDepth)
                surfaceBoxGesture.UpdateDepth(current.mousePosition);
            else
                return false;

            return true;
        }


        // Клик, пока выбираешь глубину, — коробка готова.
        public override bool OnMouseDown(Ray worldRay, Event current)
        {
            if (drawGesture.IsChoosingDepth)
            {
                if (drawGesture.Commit())
                    IsNewModelRequested = false;

                return true;
            }

            if (!surfaceBoxGesture.IsChoosingDepth)
                return false;

            surfaceBoxGesture.Commit();
            return true;
        }


        // Косая поверхность — коробка или вырез по её нормали; иначе — обычная коробка по осям объекта.
        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            if (canBeginGesture && !drawGesture.IsActive && !surfaceBoxGesture.IsActive)
            {
                if (LiveBuilderSurfaceBoxGesture.IsAngledSurface(downRay, IsNewModelRequested))
                    surfaceBoxGesture.TryBegin(downRay);
                else
                    drawGesture.TryBegin(downRay, IsNewModelRequested);
            }

            if (surfaceBoxGesture.IsActive)
                surfaceBoxGesture.UpdateRectangle(worldRay);
            else
                drawGesture.UpdateRectangle(worldRay);
        }


        // Отпустил прямоугольник — дальше мышью глубина.
        public override bool OnMouseUp(Event current)
        {
            if (drawGesture.IsActive)
                drawGesture.TryFinishRectangle(current.mousePosition);
            else if (surfaceBoxGesture.IsActive)
                surfaceBoxGesture.FinishRectangle(current.mousePosition);
            else
                return false;

            return true;
        }


        // ── Окно сцены: отмена и вид ─────────────────────────────────────

        // Бросить начатую коробку.
        public override void Cancel()
        {
            drawGesture.Cancel();
            surfaceBoxGesture.Cancel();
        }


        // Как встанет рисуемая коробка.
        public override void DrawPreview()
        {
            drawGesture.DrawPreview();
            surfaceBoxGesture.DrawPreview();
        }
    }
}
