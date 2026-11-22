using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Одно место на правило выреза — для солвера, стен, зданий и проёмов: кого вырез может резать, что из этого и свой ли это кусок.
    /// - «Own object» — ближайшая постройка над вырезом и всё в ней, кроме вложенных объектов из коробок; «Everything it touches» — всё, чего касается
    /// - «Cuts» — всё, только перекрытия или только стены; коробки своего объекта режутся всегда; свой кусок — никогда (лестница — свои ступени)
    /// - правило объекта из коробок — по версии его данных: 0 — старое, по порядку в списке; 2 — сначала все добавления, потом все вырезы
    /// - каким способом режет (<see cref="LiveBuilderCutMethod"/>): вырез-коробка своего объекта — точно, остальное — вычитанием из меша
    /// </summary>
    public static class LiveBuilderCutRules
    {
        private static readonly Transform[] NoConstructions = new Transform[0];


        // ── Правило объекта ──────────────────────────────────────────────

        // Старые данные: вырезы по порядку в списке и прежние исключения у стен и зданий.
        public static bool IsLegacy(BoxModel model)
        {
            return model.DataVersion < BoxModel.CurrentDataVersion;
        }


        public static BoxModelCutRule RuleOf(BoxModel model)
        {
            return IsLegacy(model) ? BoxModelCutRule.LaterBoxesCut : BoxModelCutRule.CutsAfterAllAdds;
        }


        // ── Вырез ────────────────────────────────────────────────────────

        public static LiveBuilderCutSource SourceOf(GeometryCutter cutter)
        {
            return SourceOf(cutter, 0);
        }


        // pieceKey — номер куска, если вырез собран солвером как коробка своего объекта (<see cref="GeometryCutterSolverBoxes"/>); иначе 0.
        public static LiveBuilderCutSource SourceOf(GeometryCutter cutter, int pieceKey)
        {
            var parent = cutter.transform.parent;
            var ownModel = parent != null ? LiveBuilderGroups.GroupOf(parent) : null;
            return new LiveBuilderCutSource(cutter, OwnConstructionOf(cutter.transform), ownModel, ScopeOf(cutter), cutter.CutTargets, pieceKey);
        }

        private static GeometryCutterScope ScopeOf(GeometryCutter cutter)
        {
            return cutter.Scope == GeometryCutterScope.EverythingItTouches ? GeometryCutterScope.EverythingItTouches : GeometryCutterScope.OwnObject;
        }


        // Вырез — кусок объекта из коробок: вырезающая коробка, пустота комнаты, вырез над лестницей. Где режет — всегда «Own object».
        public static LiveBuilderCutSource SourceOfPiece(Component piece, IReadOnlyList<Transform> constructionsAbove, int pieceKey)
        {
            var hasConstructions = constructionsAbove != null && constructionsAbove.Count > 0;
            var ownConstruction = hasConstructions ? constructionsAbove[constructionsAbove.Count - 1] : null;
            var ownModel = hasConstructions ? constructionsAbove[0].GetComponent<BoxModel>() : null;
            return new LiveBuilderCutSource(piece, ownConstruction, ownModel, GeometryCutterScope.OwnObject, TargetsOf(piece), pieceKey);
        }

        // Коробка — своё поле «Режет», вырез над лестницей — только перекрытия, пустота комнаты — всё.
        private static LiveBuilderCutTargets TargetsOf(Component piece)
        {
            return piece switch
            {
                BoxModelPart part => part.CutTargets,
                BoxModelStairs => LiveBuilderCutTargets.SlabsOnly,
                _ => LiveBuilderCutTargets.Everything
            };
        }


        // Ближайшая постройка над вырезом: объект из коробок, стена по линии или здание. Нет такой — null.
        public static Transform OwnConstructionOf(Transform cut)
        {
            for (var node = cut.parent; node != null; node = node.parent)
                if (IsConstruction(node))
                    return node;

            return null;
        }


        public static bool IsConstruction(Transform node)
        {
            return node.GetComponent<BoxModel>() != null || node.GetComponent<SplineWall>() != null || node.GetComponent<SplineBuilding>() != null;
        }


        // ── Кого достаёт ─────────────────────────────────────────────────

        // Свои дочерние объекты вырез не режет никогда: дверь-префаб — свою раму. «Own object» без постройки над ним не режет ничего.
        public static bool Reaches(LiveBuilderCutSource cut, Component target)
        {
            if (cut.Owner != null && target.transform.IsChildOf(cut.Owner.transform))
                return false;

            var construction = ConstructionOf(target);

            if (cut.Scope == GeometryCutterScope.EverythingItTouches)
                return ReachesEverythingItTouches(cut, target, construction);

            if (cut.OwnConstruction == null)
                return false;

            return construction.IsChildOf(cut.OwnConstruction) && LiveBuilderGroups.GroupOf(construction) == cut.OwnModel;
        }

        // Внутри самой постройки — всегда; чужое из того же префаба и всё в открытом префабе — никогда.
        private static bool ReachesEverythingItTouches(LiveBuilderCutSource cut, Component target, Transform construction)
        {
            if (cut.Owner == null || cut.Owner.transform.IsChildOf(construction))
                return true;

            var cutObject = cut.Owner.gameObject;

            if (PrefabStageUtility.GetPrefabStage(cutObject) != null || BoxModelMeshStorage.IsLoadedPrefabContents(cutObject.scene))
                return false;

            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(cutObject);
            return prefabRoot == null || !target.transform.IsChildOf(prefabRoot.transform);
        }

        // Постройка, чей это меш: у части здания — само здание.
        private static Transform ConstructionOf(Component target)
        {
            var building = target is SplineBuildingPart ? target.GetComponentInParent<SplineBuilding>() : null;
            return building != null ? building.transform : target.transform;
        }


        // ── Что режет ────────────────────────────────────────────────────

        // Вид меша: стена по линии и стены здания — стены, пол, перекрытия и крыша здания — перекрытия, объект из коробок — только «всё».
        // Здание целиком (его спрашивают, когда ищут вырезы снаружи префаба) — и стены, и перекрытия.
        public static bool CutsKind(LiveBuilderCutTargets targets, Component target)
        {
            if (target is SplineBuilding)
                return true;

            var buildingPart = target as SplineBuildingPart;

            return targets switch
            {
                LiveBuilderCutTargets.SlabsOnly => buildingPart != null && buildingPart.Kind != SplineBuildingPartKind.Walls,
                LiveBuilderCutTargets.WallsOnly => target is SplineWall || buildingPart != null && buildingPart.Kind == SplineBuildingPartKind.Walls,
                _ => true
            };
        }


        // ── Проёмы ───────────────────────────────────────────────────────

        public static bool CanCutterCut(GeometryCutter cutter, Component target)
        {
            return cutter.isActiveAndEnabled && CutsKind(cutter.CutTargets, target) && Reaches(SourceOf(cutter), target);
        }


        // ── Каким способом ───────────────────────────────────────────────

        // Коробки своего объекта вырез-коробка режет всегда, что бы ни стояло в «Cuts»: оно — про стены и здания.
        public static LiveBuilderCutMethod MethodOf(GeometryCutter cutter, Component target)
        {
            if (target is BoxModel model && GeometryCutterSolverBoxes.IsSolverPieceOf(cutter, model))
                return LiveBuilderCutMethod.SolverCells;

            return CanCutterCut(cutter, target) ? PreciseMethodOf(cutter, target) : LiveBuilderCutMethod.None;
        }


        // Точно режет только вырез-коробка, которую собирает объект этой постройки; остальное — вычитанием. Сначала спросить CanCutterCut.
        public static LiveBuilderCutMethod PreciseMethodOf(GeometryCutter cutter, Component target)
        {
            if (target is SplineWall parentWall && IsWallChildBox(cutter, parentWall))
                return CutsChildWallPrecisely(cutter, parentWall) ? LiveBuilderCutMethod.WallHoles : LiveBuilderCutMethod.MeshSubtraction;

            var model = target as BoxModel ?? SplineWallRebuilder.OuterModelOf(target);

            if (!GeometryCutterSolverBoxes.IsSolverPieceOf(cutter, model))
                return LiveBuilderCutMethod.MeshSubtraction;

            switch (target)
            {
                case BoxModel:
                    return LiveBuilderCutMethod.SolverCells;

                case SplineWall wall:
                    return CutsWallsPrecisely(cutter, model, wall, SplineWallRebuilder.PathsOf(wall))
                        ? LiveBuilderCutMethod.WallHoles
                        : LiveBuilderCutMethod.MeshSubtraction;

                case SplineBuildingPart part when part.Kind == SplineBuildingPartKind.Walls:
                    var building = part.GetComponentInParent<SplineBuilding>();
                    return building != null && CutsWallsPrecisely(cutter, model, building, SplineWallRebuilder.PathsOf(building))
                        ? LiveBuilderCutMethod.WallHoles
                        : LiveBuilderCutMethod.MeshSubtraction;

                case SplineBuildingPart slabPart:
                    return CutsSlabsPrecisely(cutter, model, slabPart.GetComponentInParent<SplineBuilding>())
                        ? LiveBuilderCutMethod.SlabHoles
                        : LiveBuilderCutMethod.MeshSubtraction;

                default:
                    return LiveBuilderCutMethod.MeshSubtraction;
            }
        }


        // Стены (по линии или здания) — проёмом по линии: без своих материалов стенок и насквозь по толщине всюду, где задевает; иначе ниша вычитанием.
        // Стена по линии решает один раз за стены и свои пол с потолком.
        public static bool CutsWallsPrecisely(GeometryCutter cutter, BoxModel model, Component wallOwner, IReadOnlyList<SplineWallPath> paths)
        {
            return !GeometryCutterSolverBoxes.HasOwnMaterials(cutter)
                   && (cutter.CutTargets == LiveBuilderCutTargets.SlabsOnly || SplineWallRebuilder.IsThroughThickness(wallOwner, model, cutter, paths));
        }


        // Перекрытия — дырой в плане, если у выреза нет своих материалов стенок: у дыры в плане они общие с торцами здания.
        public static bool CutsSlabsPrecisely(GeometryCutter cutter, BoxModel model, Component slabOwner)
        {
            if (slabOwner is SplineWall wall)
                return CutsWallsPrecisely(cutter, model, wall, SplineWallRebuilder.PathsOf(wall));

            return slabOwner != null && !GeometryCutterSolverBoxes.HasOwnMaterials(cutter);
        }


        // ── Дверь в стене по линии ───────────────────────────────────────

        // Вырез — ребёнок стены (дверь в перегородке), коробка по её осям без своих копий: режет её проёмом по линии, как вырез-коробка объекта.
        // Стена в объекте старых данных — по-старому, вычитанием.
        public static bool IsWallChildBox(GeometryCutter cutter, SplineWall wall)
        {
            if (!cutter.isActiveAndEnabled || OwnConstructionOf(cutter.transform) != wall.transform)
                return false;

            var outerModel = SplineWallRebuilder.OuterModelOf(wall);

            if (outerModel != null && IsLegacy(outerModel) || HasOwnRepeat(cutter, wall))
                return false;

            return GeometryCutterSolverBoxes.IsBoxAlongAxesOf(cutter, wall.transform);
        }

        // Повтор между вырезом и стеной даёт двери свои копии, а проём по линии строится один на копию стены.
        private static bool HasOwnRepeat(GeometryCutter cutter, SplineWall wall)
        {
            for (var node = cutter.transform; node != null && node != wall.transform; node = node.parent)
                if (node.GetComponent<LiveBuilderArray>() != null)
                    return true;

            return false;
        }

        private static bool CutsChildWallPrecisely(GeometryCutter cutter, SplineWall wall)
        {
            return !GeometryCutterSolverBoxes.HasOwnMaterials(cutter) && SplineWallRebuilder.IsThroughThickness(wall, Matrix4x4.identity,
                GeometryCutterSolverBoxes.BoxesOf(cutter, wall.transform, Vector3.zero), SplineWallRebuilder.PathsOf(wall));
        }


        // Двери стены, которые режут её проёмом по линии.
        public static List<GeometryCutter> PreciseChildCuttersOf(SplineWall wall)
        {
            var cutters = new List<GeometryCutter>();

            foreach (var cutter in wall.GetComponentsInChildren<GeometryCutter>())
                if (IsWallChildBox(cutter, wall) && MethodOf(cutter, wall) == LiveBuilderCutMethod.WallHoles)
                    cutters.Add(cutter);

            return cutters;
        }


        // ── Вырезы объекта из коробок: стены и здания в нём ─────────────

        // Старое правило — всегда, кроме пустоты комнаты: та режет только здания, перегородки внутри неё целы.
        public static bool CutBoxCutsConstruction(BoxModel model, BoxModelSolverBox cutBox, Component target)
        {
            if (IsLegacy(model))
                return cutBox.Owner is not BoxModelRoom || target is SplineBuilding;

            return Reaches(cutBox.CutSource, target);
        }


        // ── Солвер ───────────────────────────────────────────────────────

        // Старому правилу фильтр не нужен: там режет всё, что ниже в списке.
        public static BoxModelCutFilter SolverFilterOf(BoxModel model, IReadOnlyList<BoxModelSolverBox> boxes)
        {
            if (IsLegacy(model))
                return null;

            return (cutIndex, addIndex) => CanCutInSolver(boxes[cutIndex], boxes[addIndex]);
        }


        // То же правило, что Reaches, но по постройкам, собранным заранее: его спрашивают на каждую пару пересекающихся коробок.
        // Коробка без построек над ней — предпросмотр рисования, она прямо в объекте.
        public static bool CanCutInSolver(BoxModelSolverBox cutBox, BoxModelSolverBox addBox)
        {
            var cut = cutBox.CutSource;

            if (IsOwnPiece(cut, addBox) || IsInsideCutter(cut, addBox))
                return false;

            if (cut.Scope == GeometryCutterScope.EverythingItTouches || cut.OwnConstruction == null || addBox.ConstructionsAbove.Count == 0)
                return true;

            foreach (var construction in addBox.ConstructionsAbove)
                if (construction == cut.OwnConstruction)
                    return true;

            return false;
        }


        public static bool IsOwnPiece(LiveBuilderCutSource cut, BoxModelSolverBox addBox)
        {
            return cut.PieceKey != 0 && cut.PieceKey == addBox.PieceKey;
        }

        // Свои дочерние объекты вырез не режет — как в Reaches.
        private static bool IsInsideCutter(LiveBuilderCutSource cut, BoxModelSolverBox addBox)
        {
            return cut.Owner is GeometryCutter && addBox.Owner != null && addBox.Owner.transform.IsChildOf(cut.Owner.transform);
        }


        // Постройки над узлом внутри объекта, от самого объекта внутрь: объект и здания, через которые идёт сборщик.
        public static IReadOnlyList<Transform> ConstructionsDownTo(BoxModel model, Transform node)
        {
            if (node == null || !node.IsChildOf(model.transform))
                return NoConstructions;

            var constructions = new List<Transform>();

            for (var current = node; current != null && current != model.transform; current = current.parent)
                if (current.GetComponent<SplineBuilding>() != null)
                    constructions.Add(current);

            constructions.Add(model.transform);
            constructions.Reverse();
            return constructions;
        }
    }
}
