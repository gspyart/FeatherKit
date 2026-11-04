using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Рисование коробки в два шага, как фигуры в ProBuilder: зажал и протянул прямоугольник на поверхности, отпустил —
    /// двигаешь мышь, задаёшь глубину, кликнул — готово. Всё прилипает к сетке, размеры подписаны.
    /// Добавит коробка объём или вырежет дыру — по режиму <see cref="BoxModelDrawModeSetting"/>: в «Авто» наружу добавляет, внутрь вырезает.
    /// Рисуешь на объекте из коробок — коробка в нём; на прочей геометрии или в пустоте — в выделенном объекте или в новом.
    /// Вырезаешь в стене по линии или здании — встаёт вырез (<see cref="GeometryCutter"/>) в них, как у «Проёма»: едет за их точками и этажами.
    /// </summary>
    public sealed class BoxModelDrawGesture
    {
        private enum DrawStage
        {
            Idle,
            Rectangle,
            Depth
        }


        private DrawStage stage = DrawStage.Idle;
        private BoxModelDrawPlane plane;

        // Рисуем новый объект: коробка всегда добавляет, в какую сторону ни тяни.
        private bool isCreatingNewModel;

        // Стена по линии или здание, на котором начат прямоугольник: вырез в них — вырез-объект, а не коробка объекта вокруг. Иначе — null.
        private Component pathOwner;

        private Vector3 rectangleStart;
        private Vector3 rectangleEnd;
        private Vector2 depthMouseStart;
        private float depth;

        // Готовый вид объекта с новой коробкой, пока задаёшь глубину.
        private readonly BoxModelPreview preview = new();


        public bool IsActive => stage != DrawStage.Idle;
        public bool IsChoosingDepth => stage == DrawStage.Depth;

        // Что делать дальше — для строки-подсказки внизу окна сцены.
        public string Hint => stage switch
        {
            DrawStage.Rectangle => "Тяни — размер прямоугольника. Отпусти — дальше глубина. Esc — отмена",
            DrawStage.Depth => BoxModelDrawModeSetting.Current == BoxModelDrawMode.Auto && !isCreatingNewModel
                ? "Веди мышь — глубина: наружу добавить, внутрь вырезать. Клик — готово, Esc — отмена"
                : "Веди мышь — глубина. Клик — готово, Esc — отмена",
            _ => string.Empty
        };


        // ── Шаги жеста ───────────────────────────────────────────────────

        // Начать прямоугольник там, куда нажали: на поверхности объекта из коробок, на прочей геометрии или в пустоте.
        // Для нового объекта коробка не встаёт в тот, на котором рисуешь, а станет отдельным объектом.
        public bool TryBegin(Ray worldRay, bool isForNewModel)
        {
            if (!BoxModelDrawPlaneFinder.TryFind(worldRay, isForNewModel, out plane, out rectangleStart))
                return false;

            isCreatingNewModel = isForNewModel;
            pathOwner = isForNewModel ? null : PathOwnerUnder(worldRay);
            rectangleEnd = rectangleStart;
            stage = DrawStage.Rectangle;
            return true;
        }

        // Ближайшее под мышью — стена по линии или здание, а не объект из коробок.
        private static Component PathOwnerUnder(Ray worldRay)
        {
            var hit = LiveBuilderSceneHit.Find(worldRay);
            return hit.IsPathOwnerCloser ? hit.PathOwner : null;
        }


        public void UpdateRectangle(Ray worldRay)
        {
            if (stage == DrawStage.Rectangle && plane.TryIntersect(worldRay, out var localPoint))
                rectangleEnd = plane.SnapOnPlane(localPoint);
        }


        // Прямоугольник готов — дальше глубина. Пустой прямоугольник — жест отменяется.
        public bool TryFinishRectangle(Vector2 mousePosition)
        {
            if (stage != DrawStage.Rectangle || !HasArea())
            {
                Cancel();
                return false;
            }

            depthMouseStart = mousePosition;
            depth = 0f;
            stage = DrawStage.Depth;
            return true;
        }


        public void UpdateDepth(Vector2 mousePosition)
        {
            if (stage != DrawStage.Depth)
                return;

            var worldCenter = plane.FrameToWorld.MultiplyPoint3x4((rectangleStart + rectangleEnd) / 2f);
            var worldOutward = plane.FrameToWorld.MultiplyVector(plane.OutwardDirection).normalized;
            var translation = HandleUtility.CalcLineTranslation(depthMouseStart, mousePosition, worldCenter, worldOutward);
            var newDepth = LiveBuilderGrid.Snap(translation, plane.Axis);

            if (Mathf.Approximately(newDepth, depth))
                return;

            depth = newDepth;
            RebuildPreview();
        }


        // Кладёт коробку или вырез и выделяет. Глубина ноль или вырезать нечего — ничего не делает и возвращает false.
        public bool Commit()
        {
            var mode = CurrentPartMode();
            var model = plane.Model;
            var isCuttingPathWall = mode == BoxModelPartMode.Cut && pathOwner != null;

            if (stage != DrawStage.Depth || Mathf.Approximately(depth, 0f) || (mode == BoxModelPartMode.Cut && model == null && !isCuttingPathWall))
            {
                Cancel();
                return false;
            }

            GetBox(out var min, out var max);

            if (isCuttingPathWall)
            {
                Selection.activeGameObject = CreateCutterInPathWall(min, max).gameObject;
                Cancel();
                return true;
            }

            if (model == null)
            {
                model = BoxModelFactory.CreateModel(min);
                max -= min;
                min = Vector3.zero;
            }

            var part = BoxModelFactory.CreatePart(model, min, max, mode);
            AttachToBuilding(model, part);
            Selection.activeGameObject = part.gameObject;

            // Сразу, а не после паузы пересборки: иначе после предпросмотра на миг мелькнёт старый вид.
            BoxModelRebuilder.Rebuild(model);
            Cancel();
            return true;
        }

        // Коробка жеста — вырез той же формы в стене или здании: лицом — прямоугольник, глубина — по нормали поверхности, опора — низ посередине.
        private GeometryCutter CreateCutterInPathWall(Vector3 min, Vector3 max)
        {
            var frame = plane.FrameToWorld;
            var depthAxis = plane.Axis;
            var upAxis = depthAxis == 1 ? 2 : 1;
            var widthAxis = 3 - depthAxis - upAxis;
            var forward = ((Vector3)frame.GetColumn(depthAxis)).normalized;
            var up = ((Vector3)frame.GetColumn(upAxis)).normalized;

            var size = max - min;
            var width = size[widthAxis] * ((Vector3)frame.GetColumn(widthAxis)).magnitude;
            var height = size[upAxis] * ((Vector3)frame.GetColumn(upAxis)).magnitude;
            var cutDepth = size[depthAxis] * ((Vector3)frame.GetColumn(depthAxis)).magnitude;
            var bottom = frame.MultiplyPoint3x4((min + max) / 2f) - up * (height / 2f);

            var cutter = BoxModelFactory.CreateCutter(pathOwner.transform, bottom, Quaternion.LookRotation(forward, up), new Vector2(width, height), cutDepth);
            LiveBuilderCutterGesture.AttachToOwner(cutter, forward);

            if (pathOwner is SplineBuilding building)
                SplineBuildingRebuilder.Rebuild(building);
            else
                SplineWallRebuilder.Rebuild((SplineWall)pathOwner);

            return cutter;
        }


        // Коробка в здании — его часть, как перегородка: в его интерьер и к его этажу. Вырез в перекрытии с «И все выше» —
        // сразу на всех этажах выше; потом повтор по этажам — в «Этажи в здании».
        private static void AttachToBuilding(BoxModel model, BoxModelPart part)
        {
            var center = model.transform.TransformPoint(BoxModelShapeCollector.LocalMinOf(model, part.transform) + part.Size / 2f);
            var building = LiveBuilderBuildingContext.BuildingContaining(model, center);

            if (building == null)
                return;

            Undo.SetTransformParent(part.transform, BoxModelFactory.InteriorOf(building), true, "Create Box");
            SplineBuildingLinkSync.Attach(part.gameObject, building, LiveBuilderBuildingContext.IsOnAllStoreys);
        }


        public void Cancel()
        {
            preview.Clear();
            stage = DrawStage.Idle;
            plane = default;
            isCreatingNewModel = false;
            pathOwner = null;
        }


        // ── Предпросмотр ─────────────────────────────────────────────────

        // Рамка жеста и размеры; сам объект с новой коробкой показывает BoxModelPreview.
        public void DrawPreview()
        {
            if (stage == DrawStage.Idle)
                return;

            using (new Handles.DrawingScope(plane.FrameToWorld))
            {
                if (stage == DrawStage.Rectangle)
                {
                    Handles.color = Color.cyan;
                    var size = rectangleEnd - rectangleStart;
                    Handles.DrawWireCube((rectangleStart + rectangleEnd) / 2f, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
                    LiveBuilderSceneText.DrawLabel(rectangleEnd, RectangleSizeText() + " м");
                    return;
                }

                GetBox(out var min, out var max);
                var mode = CurrentPartMode();
                Handles.color = mode == BoxModelPartMode.Add ? BoxModelPartHandles.AddColor : BoxModelPartHandles.CutColor;
                Handles.DrawWireCube((min + max) / 2f, max - min);
                LiveBuilderSceneText.DrawLabel(max, DepthText(mode));
            }
        }

        // «2 × 3» — стороны прямоугольника по осям плоскости.
        private string RectangleSizeText()
        {
            var axisU = (plane.Axis + 1) % 3;
            var axisV = (plane.Axis + 2) % 3;
            var size = rectangleEnd - rectangleStart;
            return LiveBuilderSceneText.FormatMeters(size[axisU]) + " × " + LiveBuilderSceneText.FormatMeters(size[axisV]);
        }

        // «2 × 3 м, высота 1,5 м — добавить»: у пола и потолка — высота, у стен — глубина.
        private string DepthText(BoxModelPartMode mode)
        {
            var depthName = plane.Axis == 1 ? "высота" : "глубина";
            var action = mode == BoxModelPartMode.Add ? "добавить" : plane.Model == null && pathOwner == null ? "вырезать нечего" : "вырезать";
            return RectangleSizeText() + " м, " + depthName + " " + LiveBuilderSceneText.FormatMeters(depth) + " м — " + action;
        }


        // Показывает объект с коробкой нужной глубины; глубина ноль — объект как есть. Вырез в стене по линии или здании — только рамкой.
        private void RebuildPreview()
        {
            var mode = CurrentPartMode();

            if (Mathf.Approximately(depth, 0f) || (mode == BoxModelPartMode.Cut && pathOwner != null))
            {
                preview.Hide();
                return;
            }

            GetBox(out var min, out var max);
            var box = MillimeterBox.FromMeters(min, max - min);
            preview.Show(plane.Model, mesh => BoxModelRebuilder.FillPreviewMesh(mesh, plane.Model, box, mode));
        }


        // ── Помощники ────────────────────────────────────────────────────

        // Новый объект — всегда добавить; иначе — по режиму в окне Live Builder и направлению глубины.
        private BoxModelPartMode CurrentPartMode()
        {
            return isCreatingNewModel ? BoxModelPartMode.Add : BoxModelDrawModeSetting.PartModeFor(depth);
        }

        // Коробка жеста в осях рамки: прямоугольник плюс глубина по нормали.
        private void GetBox(out Vector3 min, out Vector3 max)
        {
            var depthEnd = rectangleStart;
            depthEnd[plane.Axis] = plane.Coordinate + depth * plane.OutwardSign;
            min = Vector3.Min(Vector3.Min(rectangleStart, rectangleEnd), depthEnd);
            max = Vector3.Max(Vector3.Max(rectangleStart, rectangleEnd), depthEnd);
        }

        private bool HasArea()
        {
            for (var axis = 0; axis < 3; axis++)
                if (axis != plane.Axis && Mathf.Approximately(rectangleStart[axis], rectangleEnd[axis]))
                    return false;

            return true;
        }
    }
}
