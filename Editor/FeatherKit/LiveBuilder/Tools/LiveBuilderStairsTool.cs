using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;
using static FeatherKit.EditorTools.LiveBuilder.LiveBuilderIconRasterizer;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// «Лестница»: как кубик — протянул по полу длину и ширину, отпустил, мышью высота, клик (<see cref="LiveBuilderStairsGesture"/>).
    /// Tab — повернуть подъём, Enter — готово, пока выбираешь высоту. Правит только лестницы.
    /// </summary>
    public sealed class LiveBuilderStairsTool : LiveBuilderTool
    {
        private readonly LiveBuilderStairsGesture stairsGesture = new();


        // ── Описание ─────────────────────────────────────────────────────

        public override string Id => "Stairs";

        public override string DisplayName => "Лестница";

        public override string Tooltip => "Как кубик: протяни по полу длину и ширину, отпусти, мышью высоту, клик. Ступени подстраиваются сами";

        public override int PaletteOrder => 5;

        // Профиль лестницы.
        public override List<Vector2[]> IconLines => new() { Points(3, 20, 3, 16, 8, 16, 8, 12, 13, 12, 13, 8, 18, 8, 18, 4, 21, 4, 21, 20, 3, 20) };


        // ── Что правит ───────────────────────────────────────────────────

        public override bool PlacesIntoBuilding => true;


        // ── Окно сцены ───────────────────────────────────────────────────

        public override bool IsBusy => stairsGesture.IsActive;

        public override bool DrawsSurfaceHover => !stairsGesture.IsActive;

        public override bool DrawsSnapCursor => true;

        public override string Hint => stairsGesture.IsActive ? stairsGesture.Hint : "Протяни по полу прямоугольник лестницы, потом мышью высоту. Клик — выделить";


        // ── Что правит ───────────────────────────────────────────────────

        protected override bool CanEditTarget(Component target)
        {
            return target is BoxModelStairs;
        }


        // ── Окно ─────────────────────────────────────────────────────────

        public override void DrawSettings()
        {
            using (LiveBuilderWindowControls.Card("Вид"))
            {
                var isSolid = LiveBuilderWindowControls.Switch(
                    new GUIContent("Ступени", "Отдельные — плиты, под лестницей пусто. Сплошная — ступенчатый объём от пола"),
                    LiveBuilderStairsSettings.Style == BoxModelStairsStyle.Solid, ("Отдельные", LiveBuilderOptionIcon.SeparateSteps), ("Сплошная", LiveBuilderOptionIcon.SolidSteps));
                LiveBuilderStairsSettings.Style = isSolid ? BoxModelStairsStyle.Solid : BoxModelStairsStyle.SeparateSteps;

                if (LiveBuilderStairsSettings.Style == BoxModelStairsStyle.SeparateSteps)
                    LiveBuilderStairsSettings.StepThickness = EditorGUILayout.FloatField(
                        new GUIContent("Толщина ступени, м", "0,2 — ступени сходятся углами; меньше — висят отдельно"), LiveBuilderStairsSettings.StepThickness);
            }

            using (LiveBuilderWindowControls.Card("Размер"))
            {
                LiveBuilderStairsSettings.IsStoreyHigh = !LiveBuilderWindowControls.Switch(
                    new GUIContent("Высота сначала", "С какой высоты начинается выбор высоты мышью: с высоты этажа или со своей, например полэтажа 2 м. В здании — с его этажа"),
                    !LiveBuilderStairsSettings.IsStoreyHigh, "Этаж", "Своя");

                if (!LiveBuilderStairsSettings.IsStoreyHigh)
                    LiveBuilderStairsSettings.CustomHeight = EditorGUILayout.FloatField(new GUIContent("Своя высота, м", "Высота, с которой начинается выбор мышью"),
                        LiveBuilderStairsSettings.CustomHeight);

                LiveBuilderStairsSettings.DefaultWidth = EditorGUILayout.FloatField(
                    new GUIContent("Ширина по умолчанию, м", "Если протянул почти без ширины — просто в сторону подъёма. Иначе ширина — какую протянул"),
                    LiveBuilderStairsSettings.DefaultWidth);

                LiveBuilderStairsSettings.CutsAbove = LiveBuilderWindowControls.Switch(
                    new GUIContent("Перекрытие над", "Не пробивать — проём в полу ставишь сам вырезающей коробкой. Пробивать — режет над маршем от 2,1 м над полом лестницы до её верха"),
                    LiveBuilderStairsSettings.CutsAbove, "Не пробивать", "Пробивать");
            }

            LiveBuilderWindowControls.Explanation("Stairs", "Как кубик: протянул прямоугольник — длина и ширина, низ — где начал; отпустил — мышью высота, клик — готово. Tab — повернуть. " +
                                           "Готовую — ручки на гранях, Tab, «Повернуть» и «Развернуть»; на любой угол — поверни её объект.");
        }


        // ── Окно сцены: мышь ─────────────────────────────────────────────

        // Выбираешь высоту — мышь её и задаёт.
        public override bool OnMouseMove(Ray worldRay, Event current)
        {
            if (!stairsGesture.IsChoosingHeight)
                return false;

            stairsGesture.UpdateHeight(current.mousePosition);
            return true;
        }


        // Клик, пока выбираешь высоту, — лестница готова.
        public override bool OnMouseDown(Ray worldRay, Event current)
        {
            if (!stairsGesture.IsChoosingHeight)
                return false;

            stairsGesture.Commit();
            return true;
        }


        public override void OnMouseDrag(Ray downRay, Ray worldRay, bool canBeginGesture)
        {
            if (canBeginGesture && !stairsGesture.IsActive)
                stairsGesture.TryBegin(downRay);

            if (stairsGesture.IsActive)
                stairsGesture.Update(worldRay);
        }


        // Отпустил прямоугольник — дальше мышью высота.
        public override bool OnMouseUp(Event current)
        {
            if (!stairsGesture.IsActive)
                return false;

            stairsGesture.FinishFootprint(current.mousePosition);
            return true;
        }


        // ── Окно сцены: клавиши, отмена, вид ─────────────────────────────

        // Tab — повернуть подъём, Enter — готово, пока выбираешь высоту.
        public override bool OnKeyDown(Event current)
        {
            if (current.keyCode == KeyCode.Tab && stairsGesture.IsActive)
                stairsGesture.RotateDirection();
            else if (current.keyCode is KeyCode.Return or KeyCode.KeypadEnter && stairsGesture.IsChoosingHeight)
                stairsGesture.Commit();
            else
                return false;

            return true;
        }


        public override void Cancel()
        {
            stairsGesture.Cancel();
        }


        public override void DrawPreview()
        {
            stairsGesture.DrawPreview();
        }


        // С верхнего этажа лестнице идти некуда — копий выше на одну меньше.
        public override string BuildingStoreyHint(LiveBuilderBuildingContext context)
        {
            return context.DescriptionOf(true);
        }
    }
}
