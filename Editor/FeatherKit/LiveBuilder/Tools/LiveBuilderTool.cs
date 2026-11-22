using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент Live Builder — что делает мышь в окне сцены, пока включён Live Builder. Один класс — всё про один инструмент:
    /// - плитка в палитре: имя, подсказка, значок из линий, место; клавиша выбора — свой ярлык в файле инструмента (<see cref="LiveBuilderShortcutIds.ToolOf"/>)
    /// - что он правит: «Выделение» — всё, остальные — только своё; чужое выделенное видно каркасом, без ручек
    /// - режимы (<see cref="Modes"/>) и свои настройки в строке инструмента панели (<see cref="LiveBuilderPanelView"/>): частые — в строке, редкие — под «⋯»
    /// - свои жесты в окне сцены: окно сцены (<see cref="LiveBuilderSceneTool"/>) решает, клик это или протягивание, и зовёт крючки ниже
    /// Новый инструмент — новый наследник: палитра находит его сама (<see cref="LiveBuilderToolPalette"/>).
    /// У инструмента, который ставит точки линии кликами (<see cref="PathGesture"/>), крючки по умолчанию уже ведут эту линию.
    /// </summary>
    public abstract class LiveBuilderTool
    {
        // Подсказка про грани — общая у Select и Box: оба правят грани.
        internal static string FaceSelectionHint => "Click — select a face piece " + LiveBuilderHintKeys.Key("double-click") + " whole face " +
                                                    LiveBuilderHintKeys.Shift + " add / remove " + LiveBuilderHintKeys.Key("drag handle") + " new box or cut";


        // ── Выделенное ───────────────────────────────────────────────────

        // Выделена коробка — подсказка про её выдавливание.
        internal static bool IsBoxSelected => Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent<BoxModelPart>(out _);


        // ── Описание ─────────────────────────────────────────────────────

        // Постоянное имя для настроек редактора: под ним помнится выбранный инструмент. Не переводить и не менять.
        public abstract string Id { get; }

        public abstract string DisplayName { get; }

        // Что делает инструмент — во всплывающей подсказке плитки и внизу окна, пока сцена не дала свою.
        public abstract string Tooltip { get; }

        // Место в палитре: меньше — раньше.
        public abstract int PaletteOrder { get; }

        // Линии значка в клетке 24×24 (<see cref="LiveBuilderIconRasterizer"/>). Каждый раз новый список: его можно дополнять.
        public abstract List<Vector2[]> IconLines { get; }

        // Режимы — виды одного рисования: клавиша инструмента ещё раз — следующий, с Shift — предыдущий. Без режимов — null.
        public virtual LiveBuilderToolModes Modes => null;


        // ── Что правит ───────────────────────────────────────────────────

        // Режим «Грани» и стрелки выделенных граней.
        public virtual bool CanEditFaces => false;

        // Ручка группы повтора.
        public virtual bool CanEditRepeats => false;

        // Наведённый на здание по контуру, ставит своё в него, на его этажи (<see cref="LiveBuilderBuildingContext"/>): в строке инструмента — To top.
        public virtual bool PlacesIntoBuilding => false;

        // Виден ли контур выделенной стены или здания.
        public virtual bool ShowsContour => true;

        // Объект без постройки на нём — объект из коробок целиком, группа повтора.
        protected virtual bool CanEditOtherObjects => false;


        // ── Куда встанет новое ───────────────────────────────────────────

        // Строка «Into: …» (<see cref="LiveBuilderIntoTarget"/>); инструмент ничего не ставит — null.
        public virtual string IntoTarget => null;


        // ── Окно сцены ───────────────────────────────────────────────────

        // Что-то начато: цифры — не смена инструмента, Esc — отмена начатого, ручки выделенного спрятаны. У инструмента линии — пока линия рисуется.
        public virtual bool IsBusy => PathGesture is { IsActive: true };

        // Что рисуется сейчас — секция Drawing панели вместо карточки (<see cref="LiveBuilderDrawingSection"/>). Ничего не рисует — null.
        public virtual LiveBuilderDrawingReport DrawingReport => PathGesture?.Report();

        // Линия, которую ставят кликами: стена, здание, комната по контуру. Иначе — null.
        public virtual LiveBuilderPathGesture PathGesture => null;

        // Подсветка грани под мышью — куда придётся клик.
        public virtual bool DrawsSurfaceHover => false;

        // Прицел на сетке — где начнётся рисование. Только вместе с подсветкой грани.
        public virtual bool DrawsSnapCursor => false;

        // Что делать сейчас — внизу окна сцены, после этажа. Читается и вне отрисовки сцены: про мышь здесь не спрашивают.
        public abstract string Hint { get; }

        // Дописать к подсказке «клик ещё раз — глубже» (<see cref="LiveBuilderDepthClick"/>): мышь знает окно сцены, оно и дописывает.
        public virtual bool ShowsDepthClickHint => false;

        // Клавиши и жесты инструмента — строки шпаргалки в окне сцены (<see cref="LiveBuilderSceneKeys"/>): «что нажать — что будет», плашки — <see cref="LiveBuilderHintKeys"/>.
        // У инструмента линии — клавиши линии. Каждый раз новый массив: подписи клавиш — из нынешних привязок.
        public virtual string[] KeyHints => PathGesture != null ? PathKeyHints() : System.Array.Empty<string>();


        // ── Панель: редкие настройки ─────────────────────────────────────

        // Что лежит под «⋯» строки инструмента — в его подсказке названиями секций: «thicknesses, materials». null — «⋯» нет.
        public virtual string MoreSettingsSummary => null;

        // Что-то под «⋯» не как по умолчанию — на «⋯» точка: спрятанное не удивит.
        public virtual bool AreMoreSettingsChanged => false;


        // ── Подсказка ────────────────────────────────────────────────────

        // Подсказка инструмента линии: что сейчас — и про точки своего выделенного: их правят тут же.
        protected static string PathHint(LiveBuilderPathGesture pathGesture)
        {
            if (pathGesture.IsActive)
                return pathGesture.Hint;

            return pathGesture.Hint + (LiveBuilderContourHandles.IsShown
                ? " " + LiveBuilderHintKeys.Key("drag a point") + " edit outline"
                : " " + LiveBuilderHintKeys.Key("click a finished one") + " select");
        }


        // Клавиши инструмента линии — для шпаргалки в окне сцены.
        private static string[] PathKeyHints()
        {
            return new[]
            {
                LiveBuilderHintKeys.Shift + " + click — the other point kind",
                "digits " + LiveBuilderHintKeys.Enter + " — exact segment length",
                LiveBuilderHintKeys.Ctrl + " — angle in 15° steps",
                LiveBuilderHintKeys.Undo + " — remove the last point"
            };
        }


        // ── Что правит: проверка ─────────────────────────────────────────

        // Может ли инструмент выделить и править эту постройку или её часть. Часть здания — как само здание.
        public bool CanEdit(Component target)
        {
            if (target == null)
                return false;

            if (target is SplineBuildingPart part)
                target = part.GetComponentInParent<SplineBuilding>();

            return target != null && CanEditTarget(target);
        }

        // Постройка — уже не часть здания и не пустая.
        protected abstract bool CanEditTarget(Component target);


        // То же для объекта: смотрит постройку или её часть на нём.
        public bool CanEdit(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

            if (gameObject.TryGetComponent<BoxModelPart>(out var part))
                return CanEdit(part);

            if (gameObject.TryGetComponent<BoxModelRoom>(out var room))
                return CanEdit(room);

            if (gameObject.TryGetComponent<BoxModelStairs>(out var stairs))
                return CanEdit(stairs);

            if (gameObject.TryGetComponent<GeometryCutter>(out var cutter))
                return CanEdit(cutter);

            if (gameObject.TryGetComponent<LiveBuilderColumn>(out var column))
                return CanEdit(column);

            if (gameObject.TryGetComponent<SplineWall>(out var wall))
                return CanEdit(wall);

            if (gameObject.TryGetComponent<SplineBuilding>(out var building))
                return CanEdit(building);

            if (gameObject.TryGetComponent<SplineBuildingPart>(out var buildingPart))
                return CanEdit(buildingPart);

            return CanEditOtherObjects;
        }


        // ── Панель ───────────────────────────────────────────────────────

        // Свои частые настройки нового в строке инструмента; режимы, «Into», To top и «⋯» рисует сама строка.
        public virtual void DrawSettings()
        {
        }


        // Редкие настройки во всплывающем «⋯» (<see cref="MoreSettingsSummary"/>).
        public virtual void DrawMoreSettings()
        {
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Мышь двинулась. true — инструмент занят ей сам (глубина, высота, линия): подсветку поверхности окно сцены не ищет.
        public virtual bool OnMouseMove(Ray worldRay, Event current)
        {
            if (PathGesture == null)
                return false;

            PathGesture.UpdateHover(worldRay);
            return true;
        }


        // Нажатие. true — забрал его (готово по клику, точка линии); false — окно сцены ждёт, клик это или протягивание.
        // В инструменте линии клик по пустому — новая точка, по своему готовому — выделить его.
        public virtual bool OnMouseDown(Ray worldRay, Event current)
        {
            if (PathGesture == null)
                return false;

            if (PathGesture.IsActive || current.shift || !LiveBuilderClickSelection.TrySelectOwn(worldRay, this))
                PathGesture.Click(worldRay, current.shift);

            return true;
        }


        // Протягивание с зажатой кнопкой. canBeginGesture — мышь ушла от нажатия достаточно далеко и Esc его не отменил.
        public virtual void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
        }


        // Отпустили. true — отпускание закончило жест; false — это клик: окно сцены выделит своё под мышью.
        public virtual bool OnMouseUp(Event current)
        {
            return false;
        }


        // ── Окно сцены: клавиши, отмена, вид ─────────────────────────────

        // Клавиша от окна сцены (<see cref="LiveBuilderSceneKeyboard"/>): цифры и X при рисовании, запятая, Backspace, Enter, Esc, Tab. true — забрал её.
        public virtual bool OnKeyDown(Event current)
        {
            return PathGesture != null && PathGesture.HandleKey(current.keyCode);
        }


        // Смена инструмента посреди жеста: начатое снимается. Начатая линия остаётся такой, как нарисована; бросает её только Esc.
        public virtual void Cancel()
        {
            PathGesture?.Finish();
        }


        // Ctrl+Z посреди жеста: объект, на котором рисуешь, мог исчезнуть — начатую фигуру бросаем.
        // Линию точками не трогаем: в ней Ctrl+Z убирает последнюю точку, а рисование идёт дальше.
        public virtual void CancelAfterUndo()
        {
            if (PathGesture == null)
                Cancel();
        }


        // Как встанет то, что рисуешь.
        public virtual void DrawPreview()
        {
            PathGesture?.DrawPreview();
        }
    }
}
