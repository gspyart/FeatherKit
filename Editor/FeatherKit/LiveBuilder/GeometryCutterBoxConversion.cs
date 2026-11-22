using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Коробка ⇄ вырез-коробка на том же объекте сцены, с Undo: «Add ⇄ Cut» у выделенных и вырезание жестом «тянуть грань».
    /// - коробка → вырез (<see cref="GeometryCutter"/>) той же формы, места, материалов сторон и «Cuts»; опора остаётся в углу
    /// - вырез-коробка по осям объекта → коробка; старая вырезающая коробка → добавляющая, как раньше
    /// </summary>
    public static class GeometryCutterBoxConversion
    {
        // Коробка становится вырезом; скосы рёбер у выреза не бывает — пропадают. Объекта над коробкой нет — null.
        public static GeometryCutter ToCutter(BoxModelPart part)
        {
            var model = ModelAbove(part.transform);

            if (model == null)
                return null;

            var gameObject = part.gameObject;
            var size = part.Size;
            var materials = BoxModelSideMaterials.Of(part);
            var cutTargets = part.CutTargets;

            Undo.RecordObject(gameObject.transform, "Toggle Add / Cut");
            gameObject.transform.rotation = model.transform.rotation;
            Undo.DestroyObjectImmediate(part);

            var cutter = Undo.AddComponent<GeometryCutter>(gameObject);
            BoxModelFactory.ShapeAsBox(cutter, size);
            cutter.CutTargets = cutTargets;
            GeometryCutterDefaults.Apply(cutter);
            CopyMaterials(materials, cutter);
            RenameIfDefault(gameObject, BoxModelPieceTidying.AddPartName, BoxModelPieceTidying.CutPartName);
            return cutter;
        }

        internal static void CopyMaterials(BoxModelSideMaterials materials, GeometryCutter cutter)
        {
            var sides = cutter.SideMaterials;
            cutter.RevealMaterial = materials.All;
            sides.Bottom = materials.Bottom;
            sides.Top = materials.Top;
            sides.Sides = materials.Side;
            sides.Right = materials.Right;
            sides.Left = materials.Left;
            sides.Front = materials.Front;
            sides.Back = materials.Back;
            CopyFits(materials.Fits, cutter.TextureFits);
        }


        // Вырез-коробка становится коробкой на том же месте. Вырез не коробка по осям своего объекта — null.
        public static BoxModelPart ToPart(GeometryCutter cutter)
        {
            var model = GeometryCutterSolverBoxes.CollectingModelOf(cutter);

            if (!GeometryCutterSolverBoxes.IsBoxAlongAxesOf(cutter, model))
                return null;

            var gameObject = cutter.gameObject;
            var box = GeometryCutterSolverBoxes.BoxesOf(cutter, model, Vector3.zero)[0];
            var materials = GeometryCutterSolverBoxes.SideMaterialsIn(cutter, model);
            var cutTargets = cutter.CutTargets;

            Undo.RecordObject(gameObject.transform, "Toggle Add / Cut");
            gameObject.transform.SetPositionAndRotation(model.transform.TransformPoint(box.MinInMeters), model.transform.rotation);
            Undo.DestroyObjectImmediate(cutter);

            var part = Undo.AddComponent<BoxModelPart>(gameObject);
            part.Size = box.MaxInMeters - box.MinInMeters;
            part.CutTargets = cutTargets;
            CopyMaterials(materials, part);
            RenameIfDefault(gameObject, BoxModelPieceTidying.CutPartName, BoxModelPieceTidying.AddPartName);
            return part;
        }

        internal static void CopyMaterials(BoxModelSideMaterials materials, BoxModelPart part)
        {
            part.Material = materials.All;
            part.BottomMaterial = materials.Bottom;
            part.TopMaterial = materials.Top;
            part.SideMaterial = materials.Side;
            part.RightMaterial = materials.Right;
            part.LeftMaterial = materials.Left;
            part.FrontMaterial = materials.Front;
            part.BackMaterial = materials.Back;
            CopyFits(materials.Fits, part.TextureFits);
        }

        // Подгонки текстуры сторон едут вместе с материалами: места у коробки и выреза те же.
        private static void CopyFits(BoxModelSideFits fits, LiveBuilderTextureFitList<BoxModelSideSlot> list)
        {
            list.ClearAll();
            list.Set(BoxModelSideSlot.All, fits.All);
            list.Set(BoxModelSideSlot.Bottom, fits.Bottom);
            list.Set(BoxModelSideSlot.Top, fits.Top);
            list.Set(BoxModelSideSlot.Sides, fits.Side);
            list.Set(BoxModelSideSlot.Right, fits.Right);
            list.Set(BoxModelSideSlot.Left, fits.Left);
            list.Set(BoxModelSideSlot.Front, fits.Front);
            list.Set(BoxModelSideSlot.Back, fits.Back);
        }


        // Старая вырезающая коробка — просто добавляющая: так «Add ⇄ Cut» работал и раньше.
        public static void ToAddingPart(BoxModelPart legacyCutPart)
        {
            Undo.RecordObject(legacyCutPart, "Toggle Add / Cut");
            legacyCutPart.Mode = BoxModelPartMode.Add;
        }


        // ── Помощники ────────────────────────────────────────────────────

        private static BoxModel ModelAbove(Transform node)
        {
            return node.parent != null ? node.parent.GetComponentInParent<BoxModel>() : null;
        }

        // Имя по умолчанию следует за видом, своё имя («Opening», «Shaft») остаётся.
        private static void RenameIfDefault(GameObject gameObject, string oldDefaultName, string newDefaultName)
        {
            if (gameObject.name != oldDefaultName)
                return;

            Undo.RecordObject(gameObject, "Toggle Add / Cut");
            gameObject.name = newDefaultName;
        }
    }
}
