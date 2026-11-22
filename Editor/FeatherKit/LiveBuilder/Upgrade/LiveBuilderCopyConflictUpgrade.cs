using System.Collections.Generic;
using FeatherKit.LevelDesign;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Повтор объекта из коробок, чьи копии по новому правилу режут друг друга (пустота комнаты копии этажом ниже — стены копии выше):
    /// старое правило шло по списку копий, и следующая копия оставалась целой. Такой повтор разворачивается в отдельные объекты — каждый в свой этаж здания.
    /// Повтор группы внутри объекта так не развернуть — строка в итог.
    /// </summary>
    public sealed class LiveBuilderCopyConflictUpgrade
    {
        private readonly HashSet<BoxModel> modelsToUnroll = new();
        private readonly Dictionary<BoxModel, List<BoxModel>> unrolledCopies = new();


        // По старым коробкам: копии какого объекта режут друг друга.
        public static LiveBuilderCopyConflictUpgrade Plan(LiveBuilderUpgradeSnapshot snapshot, LiveBuilderUpgradeReport report)
        {
            var plan = new LiveBuilderCopyConflictUpgrade();

            foreach (var model in snapshot.LegacyModels)
            {
                var boxes = snapshot.LegacyBoxesOf(model);
                var owners = snapshot.LegacyOwnersOf(model);
                var conflictOwner = FirstCopyConflictOf(boxes, owners);

                if (conflictOwner == null)
                    continue;

                if (IsRepeatedItself(model))
                    plan.modelsToUnroll.Add(model);
                else
                    report.AddMayLookDifferent(conflictOwner, "its copies now cut each other; the repeat is on a group inside the object, unroll it by hand");
            }

            return plan;
        }

        private static GameObject FirstCopyConflictOf(IReadOnlyList<BoxModelSolverBox> boxes, IReadOnlyList<GameObject> owners)
        {
            for (var add = 0; add < boxes.Count; add++)
                if (boxes[add].Mode == BoxModelPartMode.Add && owners[add] != null)
                    for (var cut = 0; cut < add; cut++)
                        if (owners[cut] == owners[add] && LiveBuilderCutOrderUpgrade.IsNewCutOf(boxes[cut], boxes[add]))
                            return owners[add];

            return null;
        }

        private static bool IsRepeatedItself(BoxModel model)
        {
            return model.GetComponent<SplineBuildingStoreyLink>() != null || model.GetComponent<BoxModelRepeat>() != null
                   || model.GetComponent<LiveBuilderArray>() != null || model.GetComponent<ArrayModifier>() != null;
        }


        // Копии, на которые развернулся объект: тело «как стало» — он вместе с ними.
        public IReadOnlyList<BoxModel> UnrolledCopiesOf(BoxModel model)
        {
            return unrolledCopies.TryGetValue(model, out var copies) ? copies : new List<BoxModel>();
        }


        // После перевода повторов: объект — копия 1, остальные — отдельные объекты на местах копий, в здании — в своём этаже.
        public void Apply(LiveBuilderUpgradeReport report)
        {
            foreach (var model in modelsToUnroll)
            {
                if (model == null || !model.TryGetComponent<LiveBuilderArray>(out var array))
                {
                    report.AddMayLookDifferent(model, "its copies now cut each other; the repeat could not be unrolled");
                    continue;
                }

                var offsets = LiveBuilderArrayCopyPoses.OwnCopiesOf(array, 1);
                Object.DestroyImmediate(array);
                var copies = new List<BoxModel>();
                var building = SplineBuildingStoreys.BuildingOf(model.transform);

                for (var index = 1; index < offsets.Count; index++)
                {
                    var copy = Object.Instantiate(model.gameObject, model.transform.parent, true);
                    copy.name = model.name + " " + (index + 1);
                    copy.transform.position += offsets[index];

                    if (building != null && SplineBuildingStoreys.HasStoreyContainers(building) && SplineBuildingStoreys.StoreyOf(model.transform) != null)
                        copy.transform.SetParent(SplineBuildingStoreys.ContainerAtHeight(building, copy.transform.position.y).transform, true);

                    copies.Add(copy.GetComponent<BoxModel>());
                }

                unrolledCopies[model] = copies;
                report.AddRestructured(model, "repeat unrolled into " + offsets.Count + " objects: by the new rule its copies would cut each other's walls");
            }
        }
    }
}
