using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Находит, куда положить материал в данных Live Builder (<see cref="LiveBuilderMaterialSlot"/>):
    /// - по грани под мышью — для материала, перетащенного в окно сцены (<see cref="LiveBuilderMaterialDrop"/>):
    ///   грань коробки — в эту сторону коробки, у выреза — в его сторону (пол, потолок, стена дыры), комнаты или лестницы;
    ///   стена по линии — в её материал; здание — в наружные стены, стены изнутри, пол, потолок или крышу, смотря что под мышью; колонна — бок, верх или низ
    /// - по выделенному — для палитры уровня (<see cref="LiveBuilderMaterialPainter"/>): выделенные грани коробки, коробка, комната, лестница,
    ///   вырез, стена, здание или его часть, колонна, объект из коробок; все места выделенного по именам — для секции Material панели
    /// У каждого места — как прочитать видимый материал и где его подгонка текстуры: их берёт и кладёт пипетка.
    /// </summary>
    public static class LiveBuilderMaterialSlots
    {
        // Стороны коробки по порядку панели: верх, низ, бока по осям объекта.
        private static readonly (string Name, int Side)[] BoxSideNames =
        {
            ("Top", 2), ("Bottom", 3), ("+X", 0), ("−X", 1), ("+Z", 4), ("−Z", 5)
        };


        // ── Под мышью ────────────────────────────────────────────────────

        // Место под лучом; isWholeBox — у коробки или комнаты не одна сторона, а вся. Под мышью не Live Builder — false.
        public static bool TryFindUnder(Ray worldRay, bool isWholeBox, out LiveBuilderMaterialSlot slot)
        {
            var hit = LiveBuilderSceneHit.Find(worldRay);
            slot = default;

            if (hit.HasBoxSurface && !hit.IsPathOwnerCloser && !hit.IsColumnCloser)
            {
                var source = BoxModelPicker.SourceBoxOf(hit.BoxSurface);

                if (source == null)
                    return false;

                // Сторона самой коробки: пол дыры смотрит вверх, а создал его низ вырезающей коробки.
                var face = hit.BoxSurface.Surface.Face;
                var side = source.Mode == BoxModelPartMode.Add ? face : BoxFaces.Opposite(face);
                slot = ForBoxSide(hit.BoxSurface.Model, source, side, isWholeBox);
            }
            else if (hit.IsColumnCloser)
            {
                slot = isWholeBox ? ForWholeColumn(hit.Column) : ForColumnSurface(hit.Column, ColumnSurfaceOf(hit.Column, hit.VisibleHit.normal));
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

        // Все места материала выделенного по именам — для стороны в секции Material панели: «All», потом стороны.
        // У коробки и выреза-коробки у стороны есть BoxSide — по ней считается Fit.
        public static List<LiveBuilderMaterialPlace> PlacesOf(GameObject gameObject)
        {
            var places = new List<LiveBuilderMaterialPlace>();

            if (gameObject.TryGetComponent<BoxModelPart>(out var part))
            {
                places.Add(new LiveBuilderMaterialPlace("All", ForWholePart(part), -1));

                foreach (var (name, side) in BoxSideNames)
                    places.Add(new LiveBuilderMaterialPlace(name, ForPartSide(part, side), side));
            }
            else if (gameObject.TryGetComponent<GeometryCutter>(out var cutter))
            {
                places.Add(new LiveBuilderMaterialPlace("All", ForWholeCutter(cutter), -1));

                foreach (var (name, side) in BoxSideNames)
                    places.Add(new LiveBuilderMaterialPlace(name, ForCutterSide(cutter, side), side));
            }
            else if (gameObject.TryGetComponent<BoxModelRoom>(out var room))
            {
                places.Add(new LiveBuilderMaterialPlace("All", ForWholeRoom(room), -1));
                places.Add(new LiveBuilderMaterialPlace("Floor", ForRoomSide(room, 3), -1));
                places.Add(new LiveBuilderMaterialPlace("Walls", ForRoomSide(room, 0), -1));
                places.Add(new LiveBuilderMaterialPlace("Ceiling", ForRoomSide(room, 2), -1));
            }
            else if (gameObject.TryGetComponent<LiveBuilderColumn>(out var column))
            {
                places.Add(new LiveBuilderMaterialPlace("All", ForWholeColumn(column), -1));
                places.Add(new LiveBuilderMaterialPlace("Side", ForColumnSurface(column, LiveBuilderColumnSurface.Sides), -1));
                places.Add(new LiveBuilderMaterialPlace("Top", ForColumnSurface(column, LiveBuilderColumnSurface.Top), -1));
                places.Add(new LiveBuilderMaterialPlace("Bottom", ForColumnSurface(column, LiveBuilderColumnSurface.Bottom), -1));
            }
            else if (gameObject.TryGetComponent<SplineBuilding>(out var building))
            {
                places.Add(new LiveBuilderMaterialPlace("Outer walls", ForBuildingSubmesh(building, SplineBuildingMeshBuilder.OuterWallSubmesh), -1));
                places.Add(new LiveBuilderMaterialPlace("Inner walls", ForBuildingSubmesh(building, SplineBuildingMeshBuilder.InnerWallSubmesh), -1));
                places.Add(new LiveBuilderMaterialPlace("Floor", ForBuildingSubmesh(building, SplineBuildingMeshBuilder.FloorSubmesh), -1));
                places.Add(new LiveBuilderMaterialPlace("Ceiling", ForBuildingSubmesh(building, SplineBuildingMeshBuilder.CeilingSubmesh), -1));
                places.Add(new LiveBuilderMaterialPlace("Roof", ForBuildingSubmesh(building, SplineBuildingMeshBuilder.RoofSubmesh), -1));
            }
            else
            {
                var slot = ForWholeObject(gameObject);

                if (slot.IsValid)
                    places.Add(new LiveBuilderMaterialPlace("All", slot, -1));
            }

            return places;
        }

        // Комната, лестница, вырез, стена, здание или его часть, колонна, объект из коробок — целиком.
        private static LiveBuilderMaterialSlot ForWholeObject(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<LiveBuilderColumn>(out var column))
                return ForWholeColumn(column);

            if (gameObject.TryGetComponent<BoxModelRoom>(out var room))
                return ForWholeRoom(room);

            if (gameObject.TryGetComponent<BoxModelStairs>(out var stairs))
                return ForStairs(stairs);

            if (gameObject.TryGetComponent<GeometryCutter>(out var cutter))
                return ForWholeCutter(cutter);

            if (gameObject.TryGetComponent<SplineWall>(out var wall))
                return ForWall(wall);

            if (gameObject.TryGetComponent<SplineBuildingPart>(out var buildingPart))
                return ForBuildingPart(buildingPart);

            if (gameObject.TryGetComponent<SplineBuilding>(out var building))
                return ForBuildingSubmesh(building, SplineBuildingMeshBuilder.OuterWallSubmesh);

            if (gameObject.TryGetComponent<BoxModel>(out var model))
                return new LiveBuilderMaterialSlot(model, "Box Model material", material => model.DefaultMaterial = material, read: () => model.DefaultMaterial);

            return default;
        }


        // ── Коробки ──────────────────────────────────────────────────────

        // Сторона коробки, комнаты или лестницы; side — сторона самой коробки (BoxFaces). У комнаты изнутри — стороны пустоты и оболочки, что смотрят в неё.
        private static LiveBuilderMaterialSlot ForBoxSide(BoxModel model, BoxModelSolverBox source, int side, bool isWholeBox)
        {
            switch (source.Owner)
            {
                case BoxModelPart part:
                    return isWholeBox ? ForWholePart(part) : ForPartSide(part, side);

                case BoxModelRoom room when source.Mode == BoxModelPartMode.Cut:
                    return isWholeBox ? ForWholeRoom(room) : ForRoomSide(room, side);

                case BoxModelRoom room when BoxModelRoomShell.IsInsideSide(model, source, side):
                    return isWholeBox ? ForWholeRoom(room) : ForRoomSide(room, BoxFaces.Opposite(side));

                case BoxModelRoom room:
                    return new LiveBuilderMaterialSlot(room, "room outside", null,
                        "A room has no outside material of its own — it is the Box Model's material: select the model and paint it",
                        () => model.DefaultMaterial);

                case BoxModelStairs stairs:
                    return ForStairs(stairs);

                case GeometryCutter cutter:
                    return isWholeBox
                        ? ForWholeCutter(cutter)
                        : ForCutterSide(cutter, GeometryCutterSolverBoxes.CutterSideOf(cutter, GeometryCutterSolverBoxes.CollectingModelOf(cutter), side));

                default:
                    return default;
            }
        }

        // Одна сторона коробки: верх, низ или один из боков.
        private static LiveBuilderMaterialSlot ForPartSide(BoxModelPart part, int side)
        {
            var axis = BoxFaces.Axis(side);
            var isPositive = BoxFaces.IsPositive(side);
            System.Func<Material> read = () => VisibleMaterial(BoxModelSideMaterials.Of(part).ForSide(side), part);
            var fit = new LiveBuilderTextureFitAccess(() => BoxModelSideFits.ForSide(part.TextureFits, side),
                newFit => part.TextureFits.Set(BoxModelSideFits.SlotOfSide(side), newFit));

            if (axis == 1)
                return isPositive
                    ? new LiveBuilderMaterialSlot(part, "box top", material => part.TopMaterial = material, null, read, fit)
                    : new LiveBuilderMaterialSlot(part, "box bottom", material => part.BottomMaterial = material, null, read, fit);

            if (axis == 0)
                return isPositive
                    ? new LiveBuilderMaterialSlot(part, "box side +X", material => part.RightMaterial = material, null, read, fit)
                    : new LiveBuilderMaterialSlot(part, "box side −X", material => part.LeftMaterial = material, null, read, fit);

            return isPositive
                ? new LiveBuilderMaterialSlot(part, "box side +Z", material => part.FrontMaterial = material, null, read, fit)
                : new LiveBuilderMaterialSlot(part, "box side −Z", material => part.BackMaterial = material, null, read, fit);
        }

        // Вся коробка: общий материал, свои у сторон снимаются — коробка вся одного цвета; подгонка — так же, одна на всю.
        private static LiveBuilderMaterialSlot ForWholePart(BoxModelPart part)
        {
            var fit = new LiveBuilderTextureFitAccess(() => part.TextureFits.Get(BoxModelSideSlot.All), newFit =>
            {
                part.TextureFits.ClearAll();
                part.TextureFits.Set(BoxModelSideSlot.All, newFit);
            });

            return new LiveBuilderMaterialSlot(part, "whole box", material =>
            {
                part.Material = material;
                part.TopMaterial = null;
                part.BottomMaterial = null;
                part.SideMaterial = null;
                part.RightMaterial = null;
                part.LeftMaterial = null;
                part.FrontMaterial = null;
                part.BackMaterial = null;
            }, null, () => VisibleMaterial(part.Material, part), fit);
        }

        // Своего материала нет — виден материал объекта.
        private static Material VisibleMaterial(Material own, Component piece)
        {
            if (own != null)
                return own;

            var model = piece.GetComponentInParent<BoxModel>();
            return model != null ? model.DefaultMaterial : null;
        }

        // Внутри комнаты: верх — потолок, низ — пол, бока — стены.
        private static LiveBuilderMaterialSlot ForRoomSide(BoxModelRoom room, int side)
        {
            var fits = room.TextureFits;

            if (BoxFaces.Axis(side) != 1)
                return new LiveBuilderMaterialSlot(room, "room walls", material => room.WallMaterial = material, null,
                    () => VisibleMaterial(room.WallMaterial, room), LiveBuilderTextureFitAccess.Of(fits, BoxModelRoomTextureSurface.Walls));

            return BoxFaces.IsPositive(side)
                ? new LiveBuilderMaterialSlot(room, "room ceiling", material => room.CeilingMaterial = material, null,
                    () => VisibleMaterial(room.CeilingMaterial, room), LiveBuilderTextureFitAccess.Of(fits, BoxModelRoomTextureSurface.Ceiling))
                : new LiveBuilderMaterialSlot(room, "room floor", material => room.FloorMaterial = material, null,
                    () => VisibleMaterial(room.FloorMaterial, room), LiveBuilderTextureFitAccess.Of(fits, BoxModelRoomTextureSurface.Floor));
        }

        private static LiveBuilderMaterialSlot ForWholeRoom(BoxModelRoom room)
        {
            var fits = room.TextureFits;
            var fit = new LiveBuilderTextureFitAccess(() => fits.Get(BoxModelRoomTextureSurface.Walls), newFit =>
            {
                fits.Set(BoxModelRoomTextureSurface.Walls, newFit);
                fits.Set(BoxModelRoomTextureSurface.Floor, newFit);
                fits.Set(BoxModelRoomTextureSurface.Ceiling, newFit);
            });

            return new LiveBuilderMaterialSlot(room, "whole room", material =>
            {
                room.FloorMaterial = material;
                room.WallMaterial = material;
                room.CeilingMaterial = material;
            }, null, () => VisibleMaterial(room.WallMaterial, room), fit);
        }

        private static LiveBuilderMaterialSlot ForStairs(BoxModelStairs stairs)
        {
            return new LiveBuilderMaterialSlot(stairs, "stairs", material => stairs.Material = material, null, () => VisibleMaterial(stairs.Material, stairs),
                new LiveBuilderTextureFitAccess(() => stairs.TextureFit, fit => stairs.TextureFit = fit != null ? fit.Copy() : null));
        }


        // ── Колонны ──────────────────────────────────────────────────────

        // Куда смотрит грань колонны: вверх — верх, вниз — низ, остальное (бок и кромки) — бок.
        private static LiveBuilderColumnSurface ColumnSurfaceOf(LiveBuilderColumn column, Vector3 worldNormal)
        {
            var up = Vector3.Dot(column.transform.up, worldNormal);
            return up > 0.9f ? LiveBuilderColumnSurface.Top : up < -0.9f ? LiveBuilderColumnSurface.Bottom : LiveBuilderColumnSurface.Sides;
        }

        private static LiveBuilderMaterialSlot ForColumnSurface(LiveBuilderColumn column, LiveBuilderColumnSurface surface)
        {
            var fit = LiveBuilderTextureFitAccess.Of(column.TextureFits, surface);
            var side = column.Material != null ? column.Material : BoxModelFactory.LoadDefaultMaterial();

            return surface switch
            {
                LiveBuilderColumnSurface.Top => new LiveBuilderMaterialSlot(column, "column top", material => column.TopMaterial = material, null,
                    () => column.TopMaterial != null ? column.TopMaterial : side, fit),
                LiveBuilderColumnSurface.Bottom => new LiveBuilderMaterialSlot(column, "column bottom", material => column.BottomMaterial = material, null,
                    () => column.BottomMaterial != null ? column.BottomMaterial : side, fit),
                _ => new LiveBuilderMaterialSlot(column, "column side", material => column.Material = material, null, () => side, fit)
            };
        }

        // Вся колонна: материал бока, свои у верха и низа снимаются; подгонка — одна на бок, верх и низ.
        private static LiveBuilderMaterialSlot ForWholeColumn(LiveBuilderColumn column)
        {
            var fits = column.TextureFits;
            var fit = new LiveBuilderTextureFitAccess(() => fits.Get(LiveBuilderColumnSurface.Sides), newFit =>
            {
                fits.Set(LiveBuilderColumnSurface.Sides, newFit);
                fits.Set(LiveBuilderColumnSurface.Top, newFit);
                fits.Set(LiveBuilderColumnSurface.Bottom, newFit);
            });

            return new LiveBuilderMaterialSlot(column, "whole column", material =>
            {
                column.Material = material;
                column.TopMaterial = null;
                column.BottomMaterial = null;
            }, null, () => column.Material != null ? column.Material : BoxModelFactory.LoadDefaultMaterial(), fit);
        }


        // ── Вырезы ───────────────────────────────────────────────────────

        // Одна сторона выреза-коробки: пол, потолок или стена дыры; side — сторона в осях выреза (BoxFaces).
        private static LiveBuilderMaterialSlot ForCutterSide(GeometryCutter cutter, int side)
        {
            var sides = cutter.SideMaterials;
            System.Func<Material> read = () => CutterMaterialOf(cutter, side);
            var fit = new LiveBuilderTextureFitAccess(() => BoxModelSideFits.ForSide(cutter.TextureFits, side),
                newFit => cutter.TextureFits.Set(BoxModelSideFits.SlotOfSide(side), newFit));

            return side switch
            {
                0 => new LiveBuilderMaterialSlot(cutter, "cut wall +X", material => sides.Right = material, null, read, fit),
                1 => new LiveBuilderMaterialSlot(cutter, "cut wall −X", material => sides.Left = material, null, read, fit),
                2 => new LiveBuilderMaterialSlot(cutter, "cut ceiling", material => sides.Top = material, null, read, fit),
                3 => new LiveBuilderMaterialSlot(cutter, "cut floor", material => sides.Bottom = material, null, read, fit),
                4 => new LiveBuilderMaterialSlot(cutter, "cut wall +Z", material => sides.Front = material, null, read, fit),
                _ => new LiveBuilderMaterialSlot(cutter, "cut wall −Z", material => sides.Back = material, null, read, fit)
            };
        }

        // Свой у стороны, общий у выреза, а нет и его — материал объекта, в котором он режет.
        private static Material CutterMaterialOf(GeometryCutter cutter, int side)
        {
            var own = GeometryCutterSolverBoxes.OwnMaterialOf(cutter, side);
            return VisibleMaterial(own != null ? own : cutter.RevealMaterial, cutter);
        }

        // Весь вырез: общий материал стенок, свои у сторон снимаются — дыра вся одного цвета; подгонка — так же.
        private static LiveBuilderMaterialSlot ForWholeCutter(GeometryCutter cutter)
        {
            var fit = new LiveBuilderTextureFitAccess(() => cutter.TextureFits.Get(BoxModelSideSlot.All), newFit =>
            {
                cutter.TextureFits.ClearAll();
                cutter.TextureFits.Set(BoxModelSideSlot.All, newFit);
            });

            return new LiveBuilderMaterialSlot(cutter, "whole cut", material =>
            {
                cutter.RevealMaterial = material;
                cutter.SideMaterials.Clear();
            }, null, () => VisibleMaterial(cutter.RevealMaterial, cutter), fit);
        }


        // ── Стены и здания ───────────────────────────────────────────────

        // Стена по линии; с типом материал задаёт тип — свой у неё не виден.
        private static LiveBuilderMaterialSlot ForWall(SplineWall wall)
        {
            return new LiveBuilderMaterialSlot(wall, "wall", material => wall.Material = material, TypeProblemOf(wall.WallType),
                () => wall.Material != null ? wall.Material : BoxModelFactory.LoadDefaultMaterial(),
                LiveBuilderTextureFitAccess.Of(wall.TextureFits, SplineWallTextureSurface.Sides));
        }

        // Часть здания целиком: этаж — его наружные стены, перекрытие — пол, крыша — крыша.
        private static LiveBuilderMaterialSlot ForBuildingPart(SplineBuildingPart part)
        {
            var building = part.GetComponentInParent<SplineBuilding>();

            if (building == null)
                return default;

            return ForBuildingSubmesh(building, part.Kind switch
            {
                SplineBuildingPartKind.Slab => SplineBuildingMeshBuilder.FloorSubmesh,
                SplineBuildingPartKind.Roof => SplineBuildingMeshBuilder.RoofSubmesh,
                _ => SplineBuildingMeshBuilder.OuterWallSubmesh
            });
        }

        // Что у здания в этой части меша. Части дальше пяти — стенки вырезов со своим материалом: их красят у выреза.
        private static LiveBuilderMaterialSlot ForBuildingSubmesh(SplineBuilding building, int submesh)
        {
            if (submesh < 0 || submesh >= SplineBuildingMeshBuilder.SubmeshCount)
                return default;

            System.Func<Material> read = () => SplineBuildingMeshBuilder.MaterialsOf(building, BoxModelFactory.LoadDefaultMaterial())[submesh];
            var fit = LiveBuilderTextureFitAccess.Of(building.TextureFits, SplineWallTexturing.BuildingSurfaceOf(submesh, SplineWallFaceKind.Side));

            return submesh switch
            {
                SplineBuildingMeshBuilder.OuterWallSubmesh => new LiveBuilderMaterialSlot(building, "building outer walls",
                    material => building.OuterWallMaterial = material, TypeProblemOf(building.WallType), read, fit),
                SplineBuildingMeshBuilder.InnerWallSubmesh => new LiveBuilderMaterialSlot(building, "building inner walls",
                    material => building.InnerWallMaterial = material, null, read, fit),
                SplineBuildingMeshBuilder.FloorSubmesh => new LiveBuilderMaterialSlot(building, "building floor", material => building.FloorMaterial = material,
                    null, read, fit),
                SplineBuildingMeshBuilder.CeilingSubmesh => new LiveBuilderMaterialSlot(building, "building ceiling",
                    material => building.CeilingMaterial = material, null, read, fit),
                _ => new LiveBuilderMaterialSlot(building, "building roof", material => building.RoofMaterial = material, null, read, fit)
            };
        }

        private static string TypeProblemOf(SplineWallType wallType)
        {
            return wallType != null
                ? "The material comes from wall type '" + wallType.name + "' — change it in the type or remove the type"
                : null;
        }
    }


    /// <summary>Место материала выделенного под своим именем на кнопке стороны: «All», «Top», «Outer walls»; BoxSide — сторона коробки или -1.</summary>
    public readonly struct LiveBuilderMaterialPlace
    {
        public LiveBuilderMaterialPlace(string name, LiveBuilderMaterialSlot slot, int boxSide)
        {
            Name = name;
            Slot = slot;
            BoxSide = boxSide;
        }


        public string Name { get; }

        public LiveBuilderMaterialSlot Slot { get; }

        public int BoxSide { get; }
    }
}
