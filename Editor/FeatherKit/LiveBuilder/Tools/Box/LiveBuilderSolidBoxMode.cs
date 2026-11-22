using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Режим Solid инструмента Box: протянул прямоугольник, мышью глубина, клик — коробка (<see cref="BoxModelDrawGesture"/>);
    /// на косой поверхности — коробка по её нормали (<see cref="LiveBuilderSurfaceBoxGesture"/>). Наружу добавляет, внутрь вырезает (Auto · Add · Cut).
    /// Наследники — тот же жест другой формы: пандус и колонна.
    /// </summary>
    public class LiveBuilderSolidBoxMode : LiveBuilderBoxToolMode
    {
        private readonly BoxModelDrawGesture drawGesture = new();
        private readonly LiveBuilderSurfaceBoxGesture surfaceBoxGesture = new();


        // ── Форма ────────────────────────────────────────────────────────

        protected virtual LiveBuilderBoxShape Shape => LiveBuilderBoxShape.Box;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => drawGesture.IsActive || surfaceBoxGesture.IsActive;

        public override LiveBuilderDrawingReport DrawingReport => drawGesture.Report() ?? surfaceBoxGesture.Report();

        public override bool DrawsSurfaceHover => !drawGesture.IsActive;

        public override bool ShowsDepthClickHint => true;

        // Что рисуешь сейчас, иначе — про грани или выделенную коробку.
        public override string Hint
        {
            get
            {
                if (drawGesture.IsActive)
                    return drawGesture.Hint;

                if (surfaceBoxGesture.IsActive)
                    return surfaceBoxGesture.Hint;

                if (LiveBuilderBoxTool.IsNewModelRequested)
                    return "New object: drag a rectangle on a surface or the ground " + LiveBuilderHintKeys.Esc + " cancel";

                if (BoxModelSelectionModeSetting.IsSelectingFaces)
                    return LiveBuilderTool.FaceSelectionHint;

                return LiveBuilderTool.IsBoxSelected ? BoxModelPartExtrusion.Hint
                    : "Drag on a surface — rectangle " + LiveBuilderHintKeys.Key("4 X 3") + " exact " + LiveBuilderHintKeys.Esc + " Select tool";
            }
        }

        public override string[] KeyHints => new[]
        {
            "While dragging: 4 " + LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.Times) + " 3 — sides",
            "Then digits " + LiveBuilderHintKeys.Enter + " — depth; 4 X 3 X 2 — all three",
            "New in the tool line — a separate object"
        };


        // ── Окно ─────────────────────────────────────────────────────────

        // Что делает коробка, что выделяет клик и куда встанет коробка.
        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Drawing"))
            {
                DrawShapeSettings();
                LiveBuilderToolSettingsFields.DrawSelectionModeSwitch();
                DrawTargetModel();
            }
        }

        // Наружу добавляет, внутрь вырезает — только у коробки: пандус и колонна всегда добавляют.
        protected virtual void DrawShapeSettings()
        {
            var drawMode = (BoxModelDrawMode)LiveBuilderWindowControls.Switch(
                new GUIContent("Mode", "Что делает нарисованная коробка. Auto — наружу добавляет, внутрь вырезает: встаёт вырез-коробка (Geometry Cutter)"),
                (int)BoxModelDrawModeSetting.Current,
                ("Auto", LiveBuilderOptionIcon.BoxAuto), ("Add", LiveBuilderOptionIcon.BoxAdd), ("Cut", LiveBuilderOptionIcon.BoxCut));

            if (drawMode != BoxModelDrawModeSetting.Current)
                BoxModelDrawModeSetting.Current = drawMode;
        }


        // Куда встанет коробка на пустом месте: в выделенный объект, иначе — новый. По чужой поверхности — всегда в тот объект.
        // «Новый объект» нажат — следующая коробка точно отдельным объектом; нажать ещё раз — отмена.
        private static void DrawTargetModel()
        {
            var selectedModel = BoxModelActions.SelectedModel();
            var isNewRequested = LiveBuilderBoxTool.IsNewModelRequested;
            var targetName = isNewRequested ? "new, separate" : selectedModel != null ? selectedModel.name : "new (nothing selected)";

            using (new EditorGUILayout.HorizontalScope())
            {
                LiveBuilderWindowControls.PrefixLabel(new GUIContent("Object", "Куда встанет коробка, нарисованная на пустом месте. " +
                                                                     "Рисуешь по поверхности другого объекта — она встанет в него"));
                LiveBuilderWindowControls.ValueChip(new GUIContent(targetName, targetName), GUILayout.MinWidth(40f));

                var isPressed = LiveBuilderWindowControls.IconToggle(isNewRequested, LiveBuilderOptionIcon.NewObject, "New",
                    "Следующая коробка — отдельный объект, даже поверх другого. На полу здания — вложенный объект в нём, привязанный к этажу " +
                    "(шахта, лестничная клетка). Нажать ещё раз — отмена");

                if (isPressed == isNewRequested)
                    return;

                if (isPressed)
                    BoxModelActions.BeginNewModelDrawing();
                else
                    LiveBuilderBoxTool.IsNewModelRequested = false;

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
                    LiveBuilderBoxTool.IsNewModelRequested = false;

                return true;
            }

            if (!surfaceBoxGesture.IsChoosingDepth)
                return false;

            surfaceBoxGesture.Commit();
            LiveBuilderBoxTool.IsNewModelRequested = false;
            return true;
        }


        // Косая поверхность — коробка или вырез по её нормали; иначе — по осям объекта.
        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            if (canBeginGesture && !drawGesture.IsActive && !surfaceBoxGesture.IsActive)
            {
                var isNewModel = LiveBuilderBoxTool.IsNewModelRequested;

                if (Shape == LiveBuilderBoxShape.Box && LiveBuilderSurfaceBoxGesture.IsAngledSurface(downRay, isNewModel))
                    surfaceBoxGesture.TryBegin(downRay);
                else
                    drawGesture.TryBegin(downRay, isNewModel, Shape);
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


        // ── Окно сцены: клавиши, отмена, вид ─────────────────────────────

        // Глубина: цифры — точно, Enter — готово; Tab — повернуть пандус.
        public override bool OnKeyDown(Event current)
        {
            if (current.keyCode == KeyCode.Tab && drawGesture.TurnRamp())
                return true;

            if (!drawGesture.HandleKey(current.keyCode, out var isCommitted))
                return false;

            if (isCommitted)
                LiveBuilderBoxTool.IsNewModelRequested = false;

            return true;
        }


        public override void Cancel()
        {
            drawGesture.Cancel();
            surfaceBoxGesture.Cancel();
        }


        public override void DrawPreview()
        {
            drawGesture.DrawPreview();
            surfaceBoxGesture.DrawPreview();
        }
    }


    /// <summary>Режим Ramp инструмента Box: коробка со скатом во всю длину — поднимается к концу протягивания, Tab поворачивает подъём.</summary>
    public sealed class LiveBuilderRampBoxMode : LiveBuilderSolidBoxMode
    {
        protected override LiveBuilderBoxShape Shape => LiveBuilderBoxShape.Ramp;

        public override string[] KeyHints => new[]
        {
            "Rises toward the end of the drag",
            LiveBuilderHintKeys.Bound(LiveBuilderShortcutIds.Rotate) + " — turn the rise, also while drawing",
            "Digits " + LiveBuilderHintKeys.Enter + " — exact height"
        };


        protected override void DrawShapeSettings()
        {
        }
    }


    /// <summary>Режим Column инструмента Box: колонна-цилиндр в протянутом прямоугольнике — отдельный объект (<see cref="LiveBuilderColumn"/>), вырезы её не режут.</summary>
    public sealed class LiveBuilderColumnBoxMode : LiveBuilderSolidBoxMode
    {
        protected override LiveBuilderBoxShape Shape => LiveBuilderBoxShape.Column;

        public override string[] KeyHints => new[]
        {
            "Drag — base rectangle, then mouse — height",
            "Digits " + LiveBuilderHintKeys.Enter + " — exact height"
        };


        protected override void DrawShapeSettings()
        {
            LiveBuilderBoxModeSettings.ColumnSides = EditorGUILayout.IntSlider(new GUIContent("Sides", "Сколько сторон у новой колонны: 4 — столб, 16 — круглая"),
                LiveBuilderBoxModeSettings.ColumnSides, LiveBuilderColumn.MinSides, LiveBuilderColumn.MaxSides);
        }
    }
}
