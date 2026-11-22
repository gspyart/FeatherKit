using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.LiveBuilder
{
    /// <summary>Места подгонки текстуры у коробки и выреза — те же восемь, что их материалы.</summary>
    public enum BoxModelSideSlot
    {
        [InspectorName("All")] All = 0,
        [InspectorName("Bottom")] Bottom = 1,
        [InspectorName("Top")] Top = 2,
        [InspectorName("Sides")] Sides = 3,
        [InspectorName("Right (+X)")] Right = 4,
        [InspectorName("Left (−X)")] Left = 5,
        [InspectorName("Front (+Z)")] Front = 6,
        [InspectorName("Back (−Z)")] Back = 7
    }


    /// <summary>Места подгонки текстуры у стены по линии: стороны, торцы (верх, откосы проёмов), пол и потолок.</summary>
    public enum SplineWallTextureSurface
    {
        [InspectorName("Sides")] Sides = 0,
        [InspectorName("Ends")] Ends = 1,
        [InspectorName("Floor")] Floor = 2,
        [InspectorName("Ceiling")] Ceiling = 3
    }


    /// <summary>Места подгонки текстуры у здания: стены снаружи и изнутри, торцы (верх стен, откосы, края дыр), пол, потолок, крыша.</summary>
    public enum SplineBuildingTextureSurface
    {
        [InspectorName("Outer Walls")] OuterWalls = 0,
        [InspectorName("Inner Walls")] InnerWalls = 1,
        [InspectorName("Ends")] Ends = 2,
        [InspectorName("Floor")] Floor = 3,
        [InspectorName("Ceiling")] Ceiling = 4,
        [InspectorName("Roof")] Roof = 5
    }


    /// <summary>Места подгонки текстуры у комнаты изнутри: стены, пол, потолок.</summary>
    public enum BoxModelRoomTextureSurface
    {
        [InspectorName("Walls")] Walls = 0,
        [InspectorName("Floor")] Floor = 1,
        [InspectorName("Ceiling")] Ceiling = 2
    }


    /// <summary>
    /// Подгонки текстуры постройки по местам — только те, что отличаются от «как есть»: пустой список — текстура как без подгонки.
    /// Место — перечисление своей постройки (<see cref="BoxModelSideSlot"/>, <see cref="SplineWallTextureSurface"/> …).
    /// </summary>
    [Serializable]
    public sealed class LiveBuilderTextureFitList<TPlace> where TPlace : struct, Enum
    {
        [SerializeField] private List<LiveBuilderTextureFitOnPlace<TPlace>> fits = new();


        public bool IsEmpty => fits.Count == 0;

        public IReadOnlyList<LiveBuilderTextureFitOnPlace<TPlace>> Entries => fits;


        // Своя подгонка места; нет — null (у места нет своей, берётся запасная или «как есть»).
        public LiveBuilderTextureFit Get(TPlace place)
        {
            foreach (var entry in fits)
                if (EqualityComparer<TPlace>.Default.Equals(entry.Place, place))
                    return entry.Fit;

            return null;
        }


        // Подгонка «как есть» убирает место из списка.
        public void Set(TPlace place, LiveBuilderTextureFit fit)
        {
            Clear(place);

            if (fit != null && !fit.IsDefault)
                fits.Add(new LiveBuilderTextureFitOnPlace<TPlace>(place, fit.Copy()));
        }


        public void Clear(TPlace place)
        {
            fits.RemoveAll(entry => EqualityComparer<TPlace>.Default.Equals(entry.Place, place));
        }


        public void ClearAll()
        {
            fits.Clear();
        }
    }


    /// <summary>Строка списка подгонок: место и его подгонка.</summary>
    [Serializable]
    public sealed class LiveBuilderTextureFitOnPlace<TPlace> where TPlace : struct, Enum
    {
        [Tooltip("Какое место постройки.")]
        [SerializeField] private TPlace place;

        [SerializeField] private LiveBuilderTextureFit fit = new();


        public LiveBuilderTextureFitOnPlace(TPlace place, LiveBuilderTextureFit fit)
        {
            this.place = place;
            this.fit = fit;
        }


        public TPlace Place => place;

        // Строка, добавленная плюсом в инспекторе, может прийти без подгонки — тогда «как есть».
        public LiveBuilderTextureFit Fit => fit ??= new LiveBuilderTextureFit();
    }
}
