using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Инструмент Live Builder — что делает мышь в окне сцены, пока включён Live Builder. Один класс — всё про один инструмент:
    /// - плитка в палитре: имя, подсказка, значок из линий, место (номер места — клавиша)
    /// - что он правит: «Выделение» — всё, остальные — только своё; чужое выделенное видно каркасом, без ручек
    /// - свои настройки на вкладке «Инструмент» окна (<see cref="LiveBuilderToolTab"/>): частые — сверху, редкие — под свёрнутым «Ещё»
    /// - свои жесты в окне сцены: окно сцены (<see cref="LiveBuilderSceneTool"/>) решает, клик это или протягивание, и зовёт крючки ниже
    /// Новый инструмент — новый наследник: палитра находит его сама (<see cref="LiveBuilderToolPalette"/>).
    /// У инструмента, который ставит точки линии кликами (<see cref="PathGesture"/>), крючки по умолчанию уже ведут эту линию.
    /// </summary>
    public abstract class LiveBuilderTool
    {
        // Подсказка про грани — общая у «Выделения» и «Коробки»: обе правят грани.
        protected const string FaceSelectionHint = "Клик — выделить кусок грани, двойной клик — грань целиком, Shift — добавить или убрать. " +
                                                   "Стрелки — двигать выделенное: кусок тянешь — новая коробка, толкаешь — вырез; грань с Ctrl — выдавить";


        // ── Выделенное ───────────────────────────────────────────────────

        // Выделена коробка — подсказка про её выдавливание.
        protected static bool IsBoxSelected => Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent<BoxModelPart>(out _);


        // ── Описание ─────────────────────────────────────────────────────

        // Постоянное имя для настроек редактора: под ним помнится выбранный инструмент. Не переводить и не менять.
        public abstract string Id { get; }

        public abstract string DisplayName { get; }

        // Что делает инструмент — во всплывающей подсказке плитки и внизу окна, пока сцена не дала свою.
        public abstract string Tooltip { get; }

        // Место в палитре: меньше — раньше. Номер места — клавиша 1, 2, 3… в окне сцены.
        public abstract int PaletteOrder { get; }

        // Линии значка в клетке 24×24 (<see cref="LiveBuilderIconRasterizer"/>). Каждый раз новый список: его можно дополнять.
        public abstract List<Vector2[]> IconLines { get; }


        // ── Что правит ───────────────────────────────────────────────────

        // Режим «Грани» и стрелки выделенных граней.
        public virtual bool CanEditFaces => false;

        // Ручка группы повтора.
        public virtual bool CanEditRepeats => false;

        // Наведённый на здание по контуру, ставит своё в него, на его этажи (<see cref="LiveBuilderBuildingContext"/>): на вкладке — карточка «В здании».
        public virtual bool PlacesIntoBuilding => false;

        // Виден ли контур выделенной стены или здания.
        public virtual bool ShowsContour => true;

        // Объект без постройки на нём — объект из коробок целиком, группа повтора.
        protected virtual bool CanEditOtherObjects => false;


        // ── Окно сцены ───────────────────────────────────────────────────

        // Что-то начато: цифры — не смена инструмента, Esc — отмена начатого, ручки выделенного спрятаны. У инструмента линии — пока линия рисуется.
        public virtual bool IsBusy => PathGesture is { IsActive: true };

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


        // ── Окно: редкие настройки ───────────────────────────────────────

        // Что лежит под свёрнутым «Ещё» — серым в его строке, например «толщины, материалы». null — строки «Ещё» нет.
        public virtual string MoreSettingsSummary => null;

        // Что-то под «Ещё» не как по умолчанию — «Ещё» раскрыто всегда: спрятанное не удивит.
        public virtual bool AreMoreSettingsChanged => false;


        // ── Подсказка ────────────────────────────────────────────────────

        // Подсказка инструмента линии: что сейчас — и про точки своего выделенного: их правят тут же.
        protected static string PathHint(LiveBuilderPathGesture pathGesture)
        {
            return pathGesture.Hint + (!pathGesture.IsActive && LiveBuilderContourHandles.IsShown
                ? " · Точки выделенного — тяни, «+» — новая, двойной клик — вид"
                : " · Клик по готовому — выделить");
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

            if (gameObject.TryGetComponent<SplineWall>(out var wall))
                return CanEdit(wall);

            if (gameObject.TryGetComponent<SplineBuilding>(out var building))
                return CanEdit(building);

            if (gameObject.TryGetComponent<SplineBuildingPart>(out var buildingPart))
                return CanEdit(buildingPart);

            return CanEditOtherObjects;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        // Свои карточки настроек на вкладке — то, что меняют каждый раз. Карточку «В здании» и строку «Ещё» рисует сама вкладка.
        public virtual void DrawSettings()
        {
        }


        // Редкие настройки под раскрытым «Ещё» (<see cref="MoreSettingsSummary"/>).
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

            if (PathGesture.IsActive || current.shift || !LiveBuilderClickSelection.TrySelectOwn(current, this))
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

        // Своя клавиша — раньше общих: шага сетки, этажа, цифр инструментов. true — забрал её.
        public virtual bool OnKeyDown(Event current)
        {
            return PathGesture != null && PathGesture.HandleKey(current.keyCode);
        }


        // Бросить начатое. Начатая линия остаётся такой, как нарисована.
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


        // Начало подсказки над зданием по контуру — его этаж; null — этаж уровня, как в пустоте.
        public virtual string BuildingStoreyHint(LiveBuilderBuildingContext context)
        {
            return context.DescriptionOf(false);
        }
    }
}
