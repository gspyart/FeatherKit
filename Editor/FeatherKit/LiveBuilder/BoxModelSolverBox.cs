using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Материалы сторон одной коробки: свой у низа, верха и боков, иначе общий; всё пусто — материал объекта.
    /// Сторона — у самой коробки: у вырезающей низ — это пол дыры, верх — потолок, бока — стены.
    /// </summary>
    public readonly struct BoxModelSideMaterials
    {
        public BoxModelSideMaterials(Material all, Material bottom = null, Material top = null, Material side = null)
        {
            All = all;
            Bottom = bottom;
            Top = top;
            Side = side;
        }


        public Material All { get; }
        public Material Bottom { get; }
        public Material Top { get; }
        public Material Side { get; }

        // Для подписи меша: поменялся любой материал — пересборка.
        public string Key => KeyOf(All) + "," + KeyOf(Bottom) + "," + KeyOf(Top) + "," + KeyOf(Side);


        public static BoxModelSideMaterials Of(BoxModelPart part)
        {
            return new BoxModelSideMaterials(part.Material, part.BottomMaterial, part.TopMaterial, part.SideMaterial);
        }


        // Материал стороны коробки (номер по BoxFaces); null — материал объекта.
        public Material ForSide(int face)
        {
            var own = BoxFaces.Axis(face) != 1 ? Side : BoxFaces.IsPositive(face) ? Top : Bottom;
            return own != null ? own : All;
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
    /// Помнит, откуда взялась (коробка или лестница на сцене): по ней грань выделяется кликом и берёт материал.
    /// </summary>
    public sealed class BoxModelSolverBox
    {
        public BoxModelSolverBox(MillimeterBox box, BoxModelPartMode mode, Component owner, BoxModelSideMaterials materials)
        {
            Box = box;
            Mode = mode;
            Owner = owner;
            Materials = materials;
        }


        public MillimeterBox Box { get; }
        public BoxModelPartMode Mode { get; }

        // Коробка (BoxModelPart) или лестница (BoxModelStairs), которая её дала.
        public Component Owner { get; }
        public BoxModelSideMaterials Materials { get; }

        public BoxModelPartShape Shape => new(Box, Mode);

        public BoxModelPart OwnerPart => Owner as BoxModelPart;


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
