using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Секция Cut — что и где режет выделенный вырез: Where it cuts, Cuts и строкой — что режет сейчас (<see cref="GeometryCutterEditor.DrawCutFields"/>).
    /// У старой вырезающей коробки — её «Cuts» и стены и здания, которые она режет сейчас по правилу (<see cref="LiveBuilderCutRules"/>).
    /// </summary>
    public sealed class LiveBuilderCutCard : LiveBuilderSelectionCard
    {
        // Что режет выделенная старая вырезающая коробка — до правки сцены.
        private static readonly List<Component> CutTargetsNow = new();
        private static BoxModelPart cutTargetsNowPart;
        private static int cutTargetsNowSceneVersion = -1;

        private Editor cutterEditor;


        public override int Order => CutOrder;

        public override string Title => "Cut";


        public override bool ShowsFor(IReadOnlyList<GameObject> selected)
        {
            return AreAllOfKind(selected, LiveBuilderSelectionKind.Cut, LiveBuilderSelectionKind.LegacyCutBox);
        }


        public override void Draw(IReadOnlyList<GameObject> selected)
        {
            if (LiveBuilderSelectionKinds.KindOf(selected[0]) == LiveBuilderSelectionKind.Cut)
            {
                if (CachedInspectorOf(OwnComponentsOf(selected), ref cutterEditor) is GeometryCutterEditor editor)
                    editor.DrawCutFields();

                return;
            }

            if (selected.Count == 1 && selected[0].TryGetComponent<BoxModelPart>(out var part))
                DrawLegacyCutBox(part);
        }


        // IDisposable: отпустить готовый инспектор.
        public override void Dispose()
        {
            DestroyInspector(ref cutterEditor);
        }


        // ── Старая вырезающая коробка ────────────────────────────────────

        private static void DrawLegacyCutBox(BoxModelPart part)
        {
            var model = part.GetComponentInParent<BoxModel>();

            if (model == null)
                return;

            DrawCutTargets(part);
            var targets = CutTargetsNowOf(part, model);

            LiveBuilderWindowControls.Note(targets.Count > 0
                ? "Cuts now: " + string.Join(", ", targets.ConvertAll(target => "«" + target.name + "»")) + "."
                : "Touches no walls or buildings — cuts only the object's boxes.");
        }

        // Что старая вырезающая коробка режет из стен по линии и зданий объекта. Свои коробки объекта режет всегда.
        private static void DrawCutTargets(BoxModelPart part)
        {
            var targets = (LiveBuilderCutTargets)LiveBuilderWindowControls.Switch(new GUIContent("Cuts",
                    "Что режет из стен по линии и зданий этого объекта; свои коробки режет всегда. " +
                    "All — стены и перекрытия. Slabs — дыра под лестницу или шахту: стены целы. Walls — проём: пол и потолок целы"),
                (int)part.CutTargets, "All", "Slabs", "Walls");

            if (targets == part.CutTargets)
                return;

            Undo.RecordObject(part, "Change Cut Targets");
            part.CutTargets = targets;
        }


        // Стены и здания, которые коробка режет по правилу и чей собранный меш задевает. Раз на правку сцены — панель рисуют часто.
        private static List<Component> CutTargetsNowOf(BoxModelPart part, BoxModel model)
        {
            if (part == cutTargetsNowPart && BoxModelRebuilder.SceneVersion == cutTargetsNowSceneVersion)
                return CutTargetsNow;

            cutTargetsNowPart = part;
            cutTargetsNowSceneVersion = BoxModelRebuilder.SceneVersion;
            CutTargetsNow.Clear();

            var boxes = BoxModelRebuilder.GetSolverBoxes(model);
            var stage = UnityEditor.SceneManagement.StageUtility.GetStageHandle(part.gameObject);

            foreach (var wall in stage.FindComponentsOfType<SplineWall>())
                if (IsCutNow(wall, wall.HasSlabs, part, model, boxes))
                    CutTargetsNow.Add(wall);

            foreach (var building in stage.FindComponentsOfType<SplineBuilding>())
                if (IsCutNow(building, true, part, model, boxes))
                    CutTargetsNow.Add(building);

            return CutTargetsNow;
        }

        // Стена или здание в том же объекте, правило пускает к ним хоть одну копию коробки, и их меш задевает её габарит.
        private static bool IsCutNow(Component target, bool hasSlabs, BoxModelPart part, BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes)
        {
            if (SplineWallRebuilder.OuterModelOf(target) != model || !target.gameObject.activeInHierarchy)
                return false;

            foreach (var box in boxes)
            {
                var cut = box.CutSource;

                if (box.Owner != part || !(cut.CutsWalls || hasSlabs && cut.CutsSlabs) || !LiveBuilderCutRules.CutBoxCutsConstruction(model, box, target))
                    continue;

                var min = box.Box.MinInMeters;
                var max = box.Box.MaxInMeters;
                var boxBounds = GeometryCutterLookup.WorldBoundsOf(new Bounds((min + max) / 2f, max - min), model.transform);

                foreach (var renderer in target.GetComponentsInChildren<Renderer>())
                    if (renderer.bounds.Intersects(boxBounds))
                        return true;
            }

            return false;
        }
    }
}
