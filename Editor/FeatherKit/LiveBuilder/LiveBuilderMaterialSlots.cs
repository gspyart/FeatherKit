using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Находит, куда положить материал в данных Live Builder (<see cref="LiveBuilderMaterialSlot"/>):
    /// - по грани под мышью — для материала, перетащенного в окно сцены (<see cref="LiveBuilderMaterialDrop"/>):
    ///   грань коробки — в эту сторону коробки (у вырезающей — сторона дыры: пол, потолок, стена), комнаты или лестницы;
    ///   стена по линии — в её материал; здание — в наружные стены, стены изнутри, пол, потолок или крышу, смотря что под мышью
    /// - по выделенному — для палитры уровня (<see cref="LiveBuilderMaterialPainter"/>): выделенные грани коробки, коробка, комната, лестница,
    ///   вырез, стена, здание или его часть, объект из коробок
    /// </summary>
    public static class LiveBuilderMaterialSlots
    {
        // ── Под мышью ────────────────────────────────────────────────────

        // Место под лучом; isWholeBox — у коробки или комнаты не одна сторона, а вся. Под мышью не Live Builder — false.
        public static bool TryFindUnder(Ray worldRay, bool isWholeBox, out LiveBuilderMaterialSlot slot)
        {
            var hit = LiveBuilderSceneHit.Find(worldRay);
            slot = default;

            if (hit.HasBoxSurface && !hit.IsPathOwnerCloser)
            {
                var source = BoxModelPicker.SourceBoxOf(hit.BoxSurface);

                if (source == null)
                    return false;

                // Сторона самой коробки: пол дыры смотрит вверх, а создал его низ вырезающей коробки.
                var face = hit.BoxSurface.Surface.Face;
                var side = source.Mode == BoxModelPartMode.Add ? face : BoxFaces.Opposite(face);
                slot = ForBoxSide(source.Owner, source.Mode, side, isWholeBox);
            }
            else if (hit.HasVisibleHit)
            {
                slot = ForPathHit(hit.VisibleHit);
            }

            return slot.IsValid;
        }

        // Стена по линии или часть здания под лучом.
        private static LiveBuilderMaterialSlot ForPathHit(RaycastHit hit)
        {
            if (hit.collider.TryGetComponent<SplineWall>(out var wall))
                return ForWall(wall);

            if (!hit.collider.TryGetComponent<SplineBuildingPart>(out var part))
                return default;

            var building = part.GetComponentInParent<SplineBuilding>();
            return building != null ? ForBuildingSubmesh(building, SubmeshOf(hit)) : default;
        }

        // В какую часть меша попал луч: у части здания номер части — что это (наружные стены, пол…).
        private static int SubmeshOf(RaycastHit hit)
        {
            var mesh = hit.collider is MeshCollider meshCollider ? meshCollider.sharedMesh : null;

            if (mesh == null || hit.triangleIndex < 0)
                return -1;

            var index = hit.triangleIndex * 3;

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var descriptor = mesh.GetSubMesh(submesh);

                if (index >= descriptor.indexStart && index < descriptor.indexStart + descriptor.indexCount)
                    return submesh;
            }

            return -1;
        }


        // ── Выделенное ───────────────────────────────────────────────────

        // Что покрасить на выделенном объекте; объект не Live Builder — пусто.
        // Грани, выделенные в режиме «Грани», — только они (и у коробки, и у её объекта из коробок); иначе коробка или объект целиком.
        public static List<LiveBuilderMaterialSlot> ForSelected(GameObject gameObject)
        {
            var slots = new List<LiveBuilderMaterialSlot>();

            if (gameObject.TryGetComponent<BoxModelPart>(out var part))
            {
                foreach (var face in BoxModelFaceSelection.Faces)
                    if (face.Part == part)
                        slots.Add(ForPartSide(part, face.Face));

                if (slots.Count == 0)
                    slots.Add(ForWholePart(part));

                return slots;
            }

            // В режиме «Грани» в Unity выделен сам объект из коробок, а грани помнит Live Builder: есть они — красятся только они.
            if (gameObject.TryGetComponent<BoxModel>(out var model))
            {
                foreach (var face in BoxModelFaceSelection.Faces)
                    if (face.Model == model)
                        slots.Add(ForPartSide(face.Part, face.Face));

                if (slots.Count > 0)
                    return slots;
            }

            var slot = ForWholeObject(gameObject);

            if (slot.IsValid)
                slots.Add(slot);

            return slots;
        }

        // Комната, лестница, вырез, стена, здание или его часть, объект из коробок — целиком.
        private static LiveBuilderMaterialSlot ForWholeObject(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<BoxModelRoom>(out var room))
                return ForWholeRoom(room);

            if (gameObject.TryGetComponent<BoxModelStairs>(out var stairs))
                return ForStairs(stairs);

            if (gameObject.TryGetComponent<GeometryCutter>(out var cutter))
                return new LiveBuilderMaterialSlot(cutter, "стенки выреза", material => cutter.RevealMaterial = material);

            if (gameObject.TryGetComponent<SplineWall>(out var wall))
                return ForWall(wall);

            if (gameObject.TryGetComponent<SplineBuildingPart>(out var buildingPart))
                return ForBuildingPart(buildingPart);

            if (gameObject.TryGetComponent<SplineBuilding>(out var building))
                return ForBuildingSubmesh(building, SplineBuildingMeshBuilder.OuterWallSubmesh);

            if (gameObject.TryGetComponent<BoxModel>(out var model))
                return new LiveBuilderMaterialSlot(model, "материал объекта из коробок", material => model.DefaultMaterial = material);

            return default;
        }


        // ── Коробки ──────────────────────────────────────────────────────

        // Сторона коробки, комнаты или лестницы; side — сторона самой коробки (BoxFaces).
        private static LiveBuilderMaterialSlot ForBoxSide(Component owner, BoxModelPartMode mode, int side, bool isWholeBox)
        {
            switch (owner)
            {
                case BoxModelPart part:
                    return isWholeBox ? ForWholePart(part) : ForPartSide(part, side);

                case BoxModelRoom room when mode == BoxModelPartMode.Cut:
                    return isWholeBox ? ForWholeRoom(room) : ForRoomSide(room, side);

                case BoxModelRoom room:
                    return new LiveBuilderMaterialSlot(room, "комната снаружи", null,
                        "Снаружи у комнаты своего материала нет — это материал объекта из коробок: выдели объект и покрась его");

                case BoxModelStairs stairs:
                    return ForStairs(stairs);

                default:
                    return default;
            }
        }

        // Одна сторона коробки: верх, низ или один из боков.
        private static LiveBuilderMaterialSlot ForPartSide(BoxModelPart part, int side)
        {
            var axis = BoxFaces.Axis(side);
            var isPositive = BoxFaces.IsPositive(side);

            if (axis == 1)
                return isPositive
                    ? new LiveBuilderMaterialSlot(part, "верх коробки", material => part.TopMaterial = material)
                    : new LiveBuilderMaterialSlot(part, "низ коробки", material => part.BottomMaterial = material);

            if (axis == 0)
                return isPositive
                    ? new LiveBuilderMaterialSlot(part, "бок коробки +X", material => part.RightMaterial = material)
                    : new LiveBuilderMaterialSlot(part, "бок коробки −X", material => part.LeftMaterial = material);

            return isPositive
                ? new LiveBuilderMaterialSlot(part, "бок коробки +Z", material => part.FrontMaterial = material)
                : new LiveBuilderMaterialSlot(part, "бок коробки −Z", material => part.BackMaterial = material);
        }

        // Вся коробка: общий материал, свои у сторон снимаются — коробка вся одного цвета.
        private static LiveBuilderMaterialSlot ForWholePart(BoxModelPart part)
        {
            return new LiveBuilderMaterialSlot(part, "коробка целиком", material =>
            {
                part.Material = material;
                part.TopMaterial = null;
                part.BottomMaterial = null;
                part.SideMaterial = null;
                part.RightMaterial = null;
                part.LeftMaterial = null;
                part.FrontMaterial = null;
                part.BackMaterial = null;
            });
        }

        // Внутри комнаты: верх — потолок, низ — пол, бока — стены.
        private static LiveBuilderMaterialSlot ForRoomSide(BoxModelRoom room, int side)
        {
            if (BoxFaces.Axis(side) != 1)
                return new LiveBuilderMaterialSlot(room, "стены комнаты", material => room.WallMaterial = material);

            return BoxFaces.IsPositive(side)
                ? new LiveBuilderMaterialSlot(room, "потолок комнаты", material => room.CeilingMaterial = material)
                : new LiveBuilderMaterialSlot(room, "пол комнаты", material => room.FloorMaterial = material);
        }

        private static LiveBuilderMaterialSlot ForWholeRoom(BoxModelRoom room)
        {
            return new LiveBuilderMaterialSlot(room, "комната целиком", material =>
            {
                room.FloorMaterial = material;
                room.WallMaterial = material;
                room.CeilingMaterial = material;
            });
        }

        private static LiveBuilderMaterialSlot ForStairs(BoxModelStairs stairs)
        {
            return new LiveBuilderMaterialSlot(stairs, "лестница", material => stairs.Material = material);
        }


        // ── Стены и здания ───────────────────────────────────────────────

        // Стена по линии; с типом материал задаёт тип — свой у неё не виден.
        private static LiveBuilderMaterialSlot ForWall(SplineWall wall)
        {
            return new LiveBuilderMaterialSlot(wall, "стена", material => wall.Material = material, TypeProblemOf(wall.WallType));
        }

        // Часть здания целиком: этаж — его наружные стены, перекрытие — пол, крыша — крыша.
        private static LiveBuilderMaterialSlot ForBuildingPart(SplineBuildingPart part)
        {
            var building = part.GetComponentInParent<SplineBuilding>();

            if (building == null)
                return default;

            return ForBuildingSubmesh(building, part.Kind switch
            {
                SplineBuildingPartKind.Floor => SplineBuildingMeshBuilder.FloorSubmesh,
                SplineBuildingPartKind.Roof => SplineBuildingMeshBuilder.RoofSubmesh,
                _ => SplineBuildingMeshBuilder.OuterWallSubmesh
            });
        }

        // Что у здания в этой части меша. Части дальше пяти — стенки вырезов со своим материалом: их красят у выреза.
        private static LiveBuilderMaterialSlot ForBuildingSubmesh(SplineBuilding building, int submesh)
        {
            return submesh switch
            {
                SplineBuildingMeshBuilder.OuterWallSubmesh => new LiveBuilderMaterialSlot(building, "наружные стены здания",
                    material => building.OuterWallMaterial = material, TypeProblemOf(building.WallType)),
                SplineBuildingMeshBuilder.InnerWallSubmesh => new LiveBuilderMaterialSlot(building, "стены здания изнутри",
                    material => building.InnerWallMaterial = material),
                SplineBuildingMeshBuilder.FloorSubmesh => new LiveBuilderMaterialSlot(building, "пол здания", material => building.FloorMaterial = material),
                SplineBuildingMeshBuilder.CeilingSubmesh => new LiveBuilderMaterialSlot(building, "потолок здания", material => building.CeilingMaterial = material),
                SplineBuildingMeshBuilder.RoofSubmesh => new LiveBuilderMaterialSlot(building, "крыша здания", material => building.RoofMaterial = material),
                _ => default
            };
        }

        private static string TypeProblemOf(SplineWallType wallType)
        {
            return wallType != null
                ? "Материал задаёт тип стены «" + wallType.name + "» — поменяй его в типе или сними тип"
                : null;
        }
    }
}
