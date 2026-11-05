using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Вкладка «Выделено» окна Live Builder: числа и действия выделенного вместо прицеливания мышью — карточками (<see cref="LiveBuilderSelectionCard"/>):
    /// коробка, «повторить», повтор, свои поля постройки, её точки, этажи уровня и здания, что внутри объекта, группа, часть здания.
    /// Карточки находит сама и рисует по их порядку; ничего не выделено — короткая строка. На корешке — сколько выделено.
    /// Выделил постройку Live Builder в сцене или иерархии — открывается сама.
    /// </summary>
    public sealed class LiveBuilderSelectionTab : LiveBuilderWindowTab
    {
        private readonly List<LiveBuilderSelectionCard> cards;


        public LiveBuilderSelectionTab()
        {
            cards = LiveBuilderTypeCatalog.CreateAll<LiveBuilderSelectionCard>();
            cards.Sort((first, second) => first.Order.CompareTo(second.Order));
        }


        // ── Корешок ──────────────────────────────────────────────────────

        public override string Title => "Выделено";

        public override Texture2D Icon => LiveBuilderToolIcons.White(LiveBuilderToolPalette.Get<LiveBuilderSelectTool>());

        public override LiveBuilderTabColor TabColor => LiveBuilderTabColor.Selection;

        // Сколько объектов выделено; ничего своего — без пометки.
        public override string Badge => HasEditableSelection() ? Selection.gameObjects.Length.ToString() : null;

        // Ничего не выделено — показывать нечего: корешок бледный.
        public override bool IsAvailable => HasEditableSelection();

        public override string UnavailableReason => "Сначала выдели постройку в окне сцены или в иерархии";


        // ── Место ────────────────────────────────────────────────────────

        public override int Order => 1;

        public override string PreferenceKey => "Selected";


        // ── Открывается сама ─────────────────────────────────────────────

        // Только у построек Live Builder: выделил свет или камеру — вкладка не перескакивает ради одной карточки «Группа».
        public override bool ShouldOpenWhenSelectionChanges => IsLiveBuilderObjectSelected();


        // ── Выделенное ───────────────────────────────────────────────────

        // Выделен объект сцены — есть что показать: у построек Live Builder — их карточки, у любого объекта — «Группа».
        public static bool HasEditableSelection()
        {
            var selected = Selection.activeGameObject;
            return selected != null && (selected.scene.IsValid() || IsLiveBuilderObject(selected));
        }


        // Выделена постройка Live Builder или что-то внутри неё.
        public static bool IsLiveBuilderObjectSelected()
        {
            var selected = Selection.activeGameObject;
            return selected != null && IsLiveBuilderObject(selected);
        }

        private static bool IsLiveBuilderObject(GameObject selected)
        {
            return selected.GetComponentInParent<BoxModel>() != null || selected.GetComponentInParent<SplineWall>() != null
                || selected.GetComponentInParent<SplineBuilding>() != null || selected.GetComponent<BoxModelRepeat>() != null
                || selected.GetComponent<GeometryCutter>() != null;
        }


        // ── Содержимое ───────────────────────────────────────────────────

        // Все карточки, которым есть что показать; ни одной — короткая строка, как выделить.
        public override void Draw()
        {
            var selected = HasEditableSelection() ? Selection.activeGameObject : null;
            var hasAnything = false;

            if (selected != null)
                foreach (var card in cards)
                    if (card.Draw(selected))
                        hasAnything = true;

            if (hasAnything)
                return;

            LiveBuilderWindowControls.Note("Ничего не выделено. Кликни по постройке в окне сцены или выдели её в иерархии.");
            LiveBuilderWindowControls.Explanation("Выделяет клик в «Выделении» — что угодно; в «Коробке», «Комнате», «Лестнице» и других — только своё. " +
                                                  "Здесь появятся числа и действия выделенного: размер коробки, точки стены, этажи здания, повтор, группа.");
        }


        // IDisposable: отпустить готовые инспекторы карточек.
        public override void Dispose()
        {
            foreach (var card in cards)
                card.Dispose();
        }
    }
}
