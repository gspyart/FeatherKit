using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Материалы сторон одной коробки: свой у низа, верха, каждого бока или всех боков, иначе общий; всё пусто — материал объекта.
    /// Сторона — у самой коробки: у вырезающей низ — это пол дыры, верх — потолок, бока — стены.
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

        // Для подписи меша: поменялся любой материал — пересборка.
        public string Key => KeyOf(All) + "," + KeyOf(Bottom) + "," + KeyOf(Top) + "," + KeyOf(Side)
                             + "," + KeyOf(Right) + "," + KeyOf(Left) + "," + KeyOf(Front) + "," + KeyOf(Back);


        public static BoxModelSideMaterials Of(BoxModelPart part)
        {
            return new BoxModelSideMaterials(part.Material, part.BottomMaterial, part.TopMaterial, part.SideMaterial,
                part.RightMaterial, part.LeftMaterial, part.FrontMaterial, part.BackMaterial);
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
    /// Коробка расчёта — одна коробка так, как её видит решатель: с учётом повторов, ступень лестницы или вырез над ней.
    /// Помнит, откуда взялась (коробка или лестница на сцене): по ней грань выделяется кликом и берёт материал,
    /// а вырезающая — решает, какие стены и здания объекта режет: что и стоят ли они выше неё в иерархии.
    /// Скосы рёбер коробки (<see cref="BoxModelEdgeChamfer"/>) едут с ней и в копии повтора: их срезает <see cref="BoxModelChamferCutter"/>.
    /// </summary>
    public sealed class BoxModelSolverBox
    {
        private static readonly BoxModelEdgeChamfer[] NoChamfers = new BoxModelEdgeChamfer[0];


        public BoxModelSolverBox(MillimeterBox box, BoxModelPartMode mode, Component owner, BoxModelSideMaterials materials,
            IReadOnlyList<BoxModelEdgeChamfer> chamfers = null)
        {
            Box = box;
            Mode = mode;
            Owner = owner;
            Materials = materials;
            Chamfers = chamfers ?? NoChamfers;
        }


        public MillimeterBox Box { get; }
        public BoxModelPartMode Mode { get; }

        // Коробка (BoxModelPart) или лестница (BoxModelStairs), которая её дала.
        public Component Owner { get; }
        public BoxModelSideMaterials Materials { get; }
        public IReadOnlyList<BoxModelEdgeChamfer> Chamfers { get; }

        public BoxModelPartShape Shape => new(Box, Mode);

        public BoxModelPart OwnerPart => Owner as BoxModelPart;

        // Что вырезающая коробка режет из стен и зданий: у коробки — её поле, вырез над лестницей — только перекрытия, комната — всё.
        public LiveBuilderCutTargets CutTargets => Owner switch
        {
            BoxModelPart part => part.CutTargets,
            BoxModelStairs => LiveBuilderCutTargets.SlabsOnly,
            _ => LiveBuilderCutTargets.Everything
        };

        public bool CutsWalls => Mode == BoxModelPartMode.Cut && CutTargets != LiveBuilderCutTargets.SlabsOnly;

        public bool CutsSlabs => Mode == BoxModelPartMode.Cut && CutTargets != LiveBuilderCutTargets.WallsOnly;


        // Стена или здание стоит в иерархии выше коробки, которая её дала, — только такое она и режет. Копии повтора — по своему оригиналу.
        public bool IsBelow(Component target)
        {
            return Owner != null && LiveBuilderHierarchyOrder.IsBefore(target.transform, Owner.transform);
        }


        // Материал поверхности с нормалью face: у добавляющей — та же сторона коробки, у вырезающей — противоположная
        // (пол дыры смотрит вверх, а создал его низ вырезающей коробки).
        public Material MaterialForSurface(int face)
        {
            var side = Mode == BoxModelPartMode.Add ? face : BoxFaces.Opposite(face);
            return Materials.ForSide(side);
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
