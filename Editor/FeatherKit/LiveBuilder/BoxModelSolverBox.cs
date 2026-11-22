using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Материалы сторон одной коробки: свой у низа, верха, каждого бока или всех боков, иначе общий; всё пусто — материал объекта.
    /// Сторона — у самой коробки: у вырезающей низ — это пол дыры, верх — потолок, бока — стены.
    /// С ними едут подгонки текстуры тех же сторон (<see cref="BoxModelSideFits"/>): кто дал материал, тот дал и подгонку.
    /// </summary>
    public readonly struct BoxModelSideMaterials
    {
        public BoxModelSideMaterials(Material all, Material bottom = null, Material top = null, Material side = null,
            Material right = null, Material left = null, Material front = null, Material back = null)
        {
            All = all;
            Bottom = bottom;
            Top = top;
            Side = side;
            Right = right;
            Left = left;
            Front = front;
            Back = back;
            Fits = default;
        }

        private BoxModelSideMaterials(BoxModelSideMaterials materials, BoxModelSideFits fits)
            : this(materials.All, materials.Bottom, materials.Top, materials.Side, materials.Right, materials.Left, materials.Front, materials.Back)
        {
            Fits = fits;
        }


        public Material All { get; }
        public Material Bottom { get; }
        public Material Top { get; }
        public Material Side { get; }

        // Свой у каждого бока: +X, −X, +Z, −Z.
        public Material Right { get; }
        public Material Left { get; }
        public Material Front { get; }
        public Material Back { get; }

        // Подгонки текстуры тех же сторон; пусто — «как есть».
        public BoxModelSideFits Fits { get; }

        // Для подписи меша: поменялся любой материал или подгонка — пересборка. Без подгонок — прежняя строка.
        public string Key => KeyOf(All) + "," + KeyOf(Bottom) + "," + KeyOf(Top) + "," + KeyOf(Side)
                             + "," + KeyOf(Right) + "," + KeyOf(Left) + "," + KeyOf(Front) + "," + KeyOf(Back) + Fits.Key;


        public static BoxModelSideMaterials Of(BoxModelPart part)
        {
            return new BoxModelSideMaterials(part.Material, part.BottomMaterial, part.TopMaterial, part.SideMaterial,
                part.RightMaterial, part.LeftMaterial, part.FrontMaterial, part.BackMaterial).WithFits(BoxModelSideFits.Of(part.TextureFits));
        }


        public BoxModelSideMaterials WithFits(BoxModelSideFits fits)
        {
            return new BoxModelSideMaterials(this, fits);
        }


        // Материал стороны коробки (номер по BoxFaces); null — материал объекта.
        public Material ForSide(int face)
        {
            if (BoxFaces.Axis(face) == 1)
                return Either(BoxFaces.IsPositive(face) ? Top : Bottom, All);

            return Either(Either(OwnOfSide(face), Side), All);
        }

        // Свой материал одного бока, без запасных.
        private Material OwnOfSide(int face)
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

        // Свой, а нет — запасной.
        private static Material Either(Material own, Material fallback)
        {
            return own != null ? own : fallback;
        }


        // Адрес файла, а не номер объекта в памяти: номер меняется при каждом запуске Unity, и все объекты считались бы изменёнными.
        public static string KeyOf(Material material)
        {
            if (material == null)
                return "-";

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out var guid, out long localId)
                ? guid + "/" + localId
                : material.GetEntityId().ToString();
        }
    }


    /// <summary>
    /// Коробка расчёта — одна коробка так, как её видит решатель: с учётом повторов, ступень лестницы, вырез над ней или вырез-коробка.
    /// - помнит, откуда взялась (коробка, вырез или лестница на сцене): по ней грань выделяется кликом и берёт материал
    /// - помнит свой кусок и постройки над ним, а вырезающая — себя глазами правила (<see cref="LiveBuilderCutRules"/>): кого режет, решает правило
    /// - скосы рёбер (<see cref="BoxModelEdgeChamfer"/>) едут с ней и в копии повтора: их срезает <see cref="BoxModelChamferCutter"/>
    /// </summary>
    public sealed class BoxModelSolverBox
    {
        private static readonly BoxModelEdgeChamfer[] NoChamfers = new BoxModelEdgeChamfer[0];
        private static readonly Transform[] NoConstructions = new Transform[0];


        public BoxModelSolverBox(MillimeterBox box, BoxModelPartMode mode, Component owner, BoxModelSideMaterials materials,
            IReadOnlyList<BoxModelEdgeChamfer> chamfers = null, int pieceKey = 0, IReadOnlyList<Transform> constructionsAbove = null)
            : this(box, mode, owner, materials, chamfers, pieceKey, constructionsAbove ?? NoConstructions,
                mode == BoxModelPartMode.Cut ? LiveBuilderCutRules.SourceOfPiece(owner, constructionsAbove ?? NoConstructions, pieceKey) : default)
        {
        }

        private BoxModelSolverBox(MillimeterBox box, BoxModelPartMode mode, Component owner, BoxModelSideMaterials materials,
            IReadOnlyList<BoxModelEdgeChamfer> chamfers, int pieceKey, IReadOnlyList<Transform> constructionsAbove, LiveBuilderCutSource cutSource)
        {
            Box = box;
            Mode = mode;
            Owner = owner;
            Materials = materials;
            Chamfers = chamfers ?? NoChamfers;
            PieceKey = pieceKey;
            ConstructionsAbove = constructionsAbove;
            CutSource = cutSource;
        }


        // Вырез-коробка (<see cref="GeometryCutterSolverBoxes"/>): где и что режет — его собственные поля, а не поля куска.
        public static BoxModelSolverBox ForCutter(MillimeterBox box, GeometryCutter cutter, BoxModelSideMaterials materials, int pieceKey,
            IReadOnlyList<Transform> constructionsAbove)
        {
            return new BoxModelSolverBox(box, BoxModelPartMode.Cut, cutter, materials, null, pieceKey, constructionsAbove ?? NoConstructions,
                LiveBuilderCutRules.SourceOf(cutter, pieceKey));
        }


        public MillimeterBox Box { get; }
        public BoxModelPartMode Mode { get; }

        // Коробка (BoxModelPart), вырез (GeometryCutter), комната (BoxModelRoom) или лестница (BoxModelStairs), которая её дала. Null — предпросмотр рисования.
        public Component Owner { get; }
        public BoxModelSideMaterials Materials { get; }
        public IReadOnlyList<BoxModelEdgeChamfer> Chamfers { get; }

        // Номер куска в одном сборе объекта — копия лестницы с её вырезом и ступенями: свой кусок вырез не режет. 0 — ничей.
        public int PieceKey { get; }

        // Постройки над куском в его объекте, от объекта внутрь: сам объект и здания. Пусто — предпросмотр.
        public IReadOnlyList<Transform> ConstructionsAbove { get; }

        // Вырезающая — она глазами правила; добавляющая — пусто.
        public LiveBuilderCutSource CutSource { get; }

        public BoxModelPartShape Shape => new(Box, Mode);

        public BoxModelPart OwnerPart => Owner as BoxModelPart;


        // Материал поверхности с нормалью face: у добавляющей — та же сторона коробки, у вырезающей — противоположная
        // (пол дыры смотрит вверх, а создал его низ вырезающей коробки).
        public Material MaterialForSurface(int face)
        {
            return Materials.ForSide(SideOfSurface(face));
        }


        // Подгонка текстуры поверхности с нормалью face — той же стороны, что материал; null — «как есть».
        public LiveBuilderTextureFit FitForSurface(int face)
        {
            return Materials.Fits.ForSide(SideOfSurface(face));
        }

        private int SideOfSurface(int face)
        {
            return Mode == BoxModelPartMode.Add ? face : BoxFaces.Opposite(face);
        }
    }


    /// <summary>
    /// Невидимый скат лестницы — только для столкновений: призма, профиль из четырёх точек, вытянутый на ширину марша.
    /// Corners — восемь углов в осях объекта: четыре точки профиля с одного края марша, потом те же с другого.
    /// </summary>
    public sealed class BoxModelRampShape
    {
        public BoxModelRampShape(Vector3[] corners)
        {
            Corners = corners;
        }


        public Vector3[] Corners { get; }
    }
}
