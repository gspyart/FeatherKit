using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Какие секции карточки показать для выделенного и в каком порядке — правило одно на все виды (<see cref="LiveBuilderSelectionCard"/>):
    /// - секции всегда по порядку Construction → Cut → Material → Chamfer → Array → Move
    /// - выделены разные виды — только общие секции (Array, Move)
    /// Секции находит сама, каталогом типов; держит их готовые инспекторы до <see cref="Dispose"/>.
    /// </summary>
    public sealed class LiveBuilderSelectionCardSet : System.IDisposable
    {
        private readonly List<LiveBuilderSelectionCard> cards;


        public LiveBuilderSelectionCardSet()
        {
            cards = LiveBuilderTypeCatalog.CreateAll<LiveBuilderSelectionCard>();
            cards.Sort((first, second) => first.Order.CompareTo(second.Order));
        }


        public IReadOnlyList<LiveBuilderSelectionCard> Cards => cards;


        // Секции для выделенного по порядку; пустые (<see cref="LiveBuilderSelectionCard.IsEmptyFor"/>) — тоже, панель рисует их строкой «+ Add».
        public List<LiveBuilderSelectionCard> ShownFor(IReadOnlyList<GameObject> selected)
        {
            var shown = new List<LiveBuilderSelectionCard>();

            if (selected.Count == 0)
                return shown;

            var isOneKind = LiveBuilderSelectionKinds.IsOneKind(selected);

            foreach (var card in cards)
                if ((isOneKind || card.IsSharedByAllKinds) && card.ShowsFor(selected))
                    shown.Add(card);

            return shown;
        }


        public T Get<T>() where T : LiveBuilderSelectionCard
        {
            return (T)cards.Find(card => card is T);
        }


        // IDisposable: панель закрывается — секции отпускают готовые инспекторы.
        public void Dispose()
        {
            foreach (var card in cards)
                card.Dispose();
        }
    }
}
