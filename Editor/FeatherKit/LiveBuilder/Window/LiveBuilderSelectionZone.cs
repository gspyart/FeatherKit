using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Зона «Выделено» окна Live Builder: числа и действия выделенного вместо прицеливания мышью — карточками (<see cref="LiveBuilderSelectionCard"/>):
    /// коробка, «повторить», повтор, свои поля постройки, её точки, этажи уровня и здания, что внутри объекта, группа, часть здания.
    /// Карточки находит сама и рисует по их порядку. Зоны нет, пока не выделен объект сцены: у чужого объекта — только карточка «Группа».
    /// </summary>
    public sealed class LiveBuilderSelectionZone : LiveBuilderWindowZone
    {
        private readonly List<LiveBuilderSelectionCard> cards;


        public LiveBuilderSelectionZone()
        {
            cards = LiveBuilderTypeCatalog.CreateAll<LiveBuilderSelectionCard>();
            cards.Sort((first, second) => first.Order.CompareTo(second.Order));
        }


        // ── Зона ─────────────────────────────────────────────────────────

        public override string Title => "Выделено";

        // «Box Model» или «Box Model и ещё 2».
        public override string Detail
        {
            get
            {
                var count = Selection.gameObjects.Length;
                var name = Selection.activeGameObject.name;
                return count > 1 ? name + " и ещё " + (count - 1) : name;
            }
        }

        public override Texture2D Icon => LiveBuilderToolIcons.White(LiveBuilderToolPalette.Get<LiveBuilderSelectTool>());

        public override LiveBuilderZoneColor ZoneColor => LiveBuilderZoneColor.Selection;

        public override int Order => 2;

        public override string PreferenceKey => "Selected";

        public override bool IsVisible => HasEditableSelection();


        // ── Выделенное ───────────────────────────────────────────────────

        // Выделен объект сцены — есть что показать: у построек Live Builder — их карточки, у любого объекта — «Группа».
        public static bool HasEditableSelection()
        {
            var selected = Selection.activeGameObject;

            if (selected == null)
                return false;

            if (selected.scene.IsValid())
                return true;

            return selected.GetComponentInParent<BoxModel>() != null || selected.GetComponentInParent<SplineWall>() != null
                || selected.GetComponentInParent<SplineBuilding>() != null || selected.GetComponent<BoxModelRepeat>() != null
                || selected.GetComponent<GeometryCutter>() != null;
        }


        // Все карточки, которым есть что показать; ни одной — подсказка, как выделить.
        public override void Draw()
        {
            var selected = Selection.activeGameObject;
            var hasAnything = false;

            foreach (var card in cards)
                if (card.Draw(selected))
                    hasAnything = true;

            if (!hasAnything)
                EditorGUILayout.HelpBox("Ничего не выделено. Кликни в сцене по коробке, комнате, лестнице, зданию, стене или проёму " +
                                        "(инструменты «Коробка», «Комната», «Лестница») или выдели в иерархии.", MessageType.None);
        }


        public override void Dispose()
        {
            foreach (var card in cards)
                card.Dispose();
        }
    }
}
