using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Подгонки текстуры сторон одной коробки расчёта (<see cref="LiveBuilderTextureFit"/>) — с тем же запасным порядком, что материалы
    /// (<see cref="BoxModelSideMaterials"/>): своя у стороны → у всех боков → у всей коробки → «как есть».
    /// Сторона — у самой коробки: у вырезающей низ — пол дыры.
    /// </summary>
    public readonly struct BoxModelSideFits
    {
        public BoxModelSideFits(LiveBuilderTextureFit all, LiveBuilderTextureFit bottom = null, LiveBuilderTextureFit top = null, LiveBuilderTextureFit side = null,
            LiveBuilderTextureFit right = null, LiveBuilderTextureFit left = null, LiveBuilderTextureFit front = null, LiveBuilderTextureFit back = null)
        {
            All = all;
            Bottom = bottom;
            Top = top;
            Side = side;
            Right = right;
            Left = left;
            Front = front;
            Back = back;
        }


        public LiveBuilderTextureFit All { get; }
        public LiveBuilderTextureFit Bottom { get; }
        public LiveBuilderTextureFit Top { get; }
        public LiveBuilderTextureFit Side { get; }
        public LiveBuilderTextureFit Right { get; }
        public LiveBuilderTextureFit Left { get; }
        public LiveBuilderTextureFit Front { get; }
        public LiveBuilderTextureFit Back { get; }

        // Ни одной своей подгонки — подписи прежние.
        public bool IsEmpty => All == null && Bottom == null && Top == null && Side == null && Right == null && Left == null && Front == null && Back == null;


        // ── Откуда ───────────────────────────────────────────────────────

        public static BoxModelSideFits Of(LiveBuilderTextureFitList<BoxModelSideSlot> list)
        {
            if (list == null || list.IsEmpty)
                return default;

            return new BoxModelSideFits(list.Get(BoxModelSideSlot.All), list.Get(BoxModelSideSlot.Bottom), list.Get(BoxModelSideSlot.Top),
                list.Get(BoxModelSideSlot.Sides), list.Get(BoxModelSideSlot.Right), list.Get(BoxModelSideSlot.Left), list.Get(BoxModelSideSlot.Front),
                list.Get(BoxModelSideSlot.Back));
        }


        // Место подгонки одной стороны коробки (BoxFaces): +X — Right, верх — Top.
        public static BoxModelSideSlot SlotOfSide(int face)
        {
            return face switch
            {
                0 => BoxModelSideSlot.Right,
                1 => BoxModelSideSlot.Left,
                2 => BoxModelSideSlot.Top,
                3 => BoxModelSideSlot.Bottom,
                4 => BoxModelSideSlot.Front,
                _ => BoxModelSideSlot.Back
            };
        }


        // Подгонка стороны коробки или выреза по их списку; null — «как есть».
        public static LiveBuilderTextureFit ForSide(LiveBuilderTextureFitList<BoxModelSideSlot> list, int face)
        {
            return Of(list).ForSide(face);
        }


        // ── Сторона ──────────────────────────────────────────────────────

        // Подгонка стороны коробки (номер по BoxFaces); null — «как есть».
        public LiveBuilderTextureFit ForSide(int face)
        {
            if (BoxFaces.Axis(face) == 1)
                return (BoxFaces.IsPositive(face) ? Top : Bottom) ?? All;

            return OwnOfSide(face) ?? Side ?? All;
        }

        private LiveBuilderTextureFit OwnOfSide(int face)
        {
            return face switch
            {
                0 => Right,
                1 => Left,
                4 => Front,
                5 => Back,
                _ => null
            };
        }


        // Только одна сторона со своей подгонкой — у коробок оболочки комнаты.
        public static BoxModelSideFits OnSide(int face, LiveBuilderTextureFit fit)
        {
            return face switch
            {
                0 => new BoxModelSideFits(null, right: fit),
                1 => new BoxModelSideFits(null, left: fit),
                2 => new BoxModelSideFits(null, top: fit),
                3 => new BoxModelSideFits(null, bottom: fit),
                4 => new BoxModelSideFits(null, front: fit),
                _ => new BoxModelSideFits(null, back: fit)
            };
        }


        // ── Подпись ──────────────────────────────────────────────────────

        // Для подписи меша; пусто — пустая строка, подпись прежняя.
        public string Key
        {
            get
            {
                if (IsEmpty)
                    return "";

                var hash = new Hash128();

                foreach (var fit in new[] { All, Bottom, Top, Side, Right, Left, Front, Back })
                    LiveBuilderTextureSignature.AppendFit(ref hash, fit);

                return "|f" + hash;
            }
        }
    }
}
