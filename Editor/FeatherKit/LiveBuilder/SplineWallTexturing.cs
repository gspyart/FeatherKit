using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>Что за грань стены или здания — по нему выбирается место подгонки: сторона, торец (верх, откос, край дыры) или плита.</summary>
    public enum SplineWallFaceKind
    {
        Side,
        End,
        Flat
    }


    /// <summary>
    /// Оси развёртки грани стены или здания до мира: по какому направлению постройки идут плоские U и V грани и откуда их отсчёт.
    /// Сдвиг до мира = где точка опоры постройки вдоль направления + свой отсчёт: у стороны U — от проекции начала пути на первый отрезок,
    /// V — от высоты линии. Нужен только World; хранится в геометрии и не зависит от места постройки — её память переживает перенос.
    /// </summary>
    public readonly struct SplineWallTextureAxes : System.IEquatable<SplineWallTextureAxes>
    {
        public SplineWallTextureAxes(Vector3 uDirection, float uBias, Vector3 vDirection, float vBias, SplineWallFaceKind kind)
        {
            UDirection = uDirection;
            UBias = uBias;
            VDirection = vDirection;
            VBias = vBias;
            Kind = kind;
        }


        public Vector3 UDirection { get; }
        public float UBias { get; }
        public Vector3 VDirection { get; }
        public float VBias { get; }
        public SplineWallFaceKind Kind { get; }


        // ── Виды граней ──────────────────────────────────────────────────

        // Сторона вдоль пути: U = ±длина пути — направление первого отрезка, V = высота над линией — вверх от её высоты.
        public static SplineWallTextureAxes Side(SplineWallPath path, float sign)
        {
            var direction = path.SegmentCount > 0 ? path.DirectionOf(0) : Vector3.forward;
            var start = path.Points.Count > 0 ? path.Points[0] : Vector3.zero;
            return new SplineWallTextureAxes(direction * sign, sign * Vector3.Dot(start, direction), Vector3.up, path.BaseHeightAt(0f), SplineWallFaceKind.Side);
        }


        // Грань по плану: U = X, V = Z постройки.
        public static SplineWallTextureAxes Plan(SplineWallFaceKind kind)
        {
            return new SplineWallTextureAxes(Vector3.right, 0f, Vector3.forward, 0f, kind);
        }


        // Поперёк стены (откос, торец пути): U — поперёк, без сдвига; V — высота над линией.
        public static SplineWallTextureAxes Across(SplineWallPath path)
        {
            return new SplineWallTextureAxes(Vector3.zero, 0f, Vector3.up, path.BaseHeightAt(0f), SplineWallFaceKind.End);
        }


        // Торец края дыры в плите: U — вдоль края, без сдвига; V — высота в осях постройки.
        public static SplineWallTextureAxes HoleEdge()
        {
            return new SplineWallTextureAxes(Vector3.zero, 0f, Vector3.up, 0f, SplineWallFaceKind.End);
        }


        // IEquatable<SplineWallTextureAxes>
        public bool Equals(SplineWallTextureAxes other)
        {
            return UDirection == other.UDirection && UBias.Equals(other.UBias) && VDirection == other.VDirection && VBias.Equals(other.VBias)
                   && Kind == other.Kind;
        }

        public override bool Equals(object other)
        {
            return other is SplineWallTextureAxes axes && Equals(axes);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(UDirection, UBias, VDirection, VBias, Kind);
        }
    }


    /// <summary>
    /// Чем развернуть меш стены или здания при записи (<see cref="SplineWallGeometry.WriteTo"/>): сдвиг до мира (World), плитка материала
    /// каждой части меша и подгонка места — по части меша и виду грани. Всё по умолчанию — плоские метры, как до этапа 5.
    /// </summary>
    public sealed class SplineWallTexturing
    {
        private readonly LiveBuilderTextureSpace space;
        private readonly Material[] materialsBySubmesh;
        private readonly System.Func<int, SplineWallFaceKind, LiveBuilderTextureFit> fitOf;


        public SplineWallTexturing(LiveBuilderTextureSpace space, Material[] materialsBySubmesh, System.Func<int, SplineWallFaceKind, LiveBuilderTextureFit> fitOf)
        {
            this.space = space;
            this.materialsBySubmesh = materialsBySubmesh;
            this.fitOf = fitOf;
        }


        // ── Для кого ─────────────────────────────────────────────────────

        // Стена по линии: часть 0 — стороны и торцы, 1 — пол, 2 — потолок; снизу пола и сверху потолка — торцы.
        public static SplineWallTexturing ForWall(SplineWall wall, Material[] materialsBySubmesh)
        {
            var fits = wall.TextureFits;
            return new SplineWallTexturing(LiveBuilderTextureSpace.Of(wall.transform, wall.TextureAlignment), materialsBySubmesh,
                (submesh, kind) => fits.IsEmpty ? null : fits.Get(WallSurfaceOf(submesh, kind)));
        }

        private static SplineWallTextureSurface WallSurfaceOf(int submesh, SplineWallFaceKind kind)
        {
            return submesh switch
            {
                SplineWallMeshBuilder.FloorSubmesh => SplineWallTextureSurface.Floor,
                SplineWallMeshBuilder.CeilingSubmesh => SplineWallTextureSurface.Ceiling,
                _ => kind == SplineWallFaceKind.Side ? SplineWallTextureSurface.Sides : SplineWallTextureSurface.Ends
            };
        }


        // Здание: части меша — стены снаружи и изнутри, пол, потолок, крыша; не стороны в частях стен — торцы.
        public static SplineWallTexturing ForBuilding(SplineBuilding building, Material[] materialsBySubmesh)
        {
            var fits = building.TextureFits;
            return new SplineWallTexturing(LiveBuilderTextureSpace.Of(building.transform, building.TextureAlignment), materialsBySubmesh,
                (submesh, kind) => fits.IsEmpty ? null : fits.Get(BuildingSurfaceOf(submesh, kind)));
        }

        public static SplineBuildingTextureSurface BuildingSurfaceOf(int submesh, SplineWallFaceKind kind)
        {
            return submesh switch
            {
                SplineBuildingMeshBuilder.OuterWallSubmesh => kind == SplineWallFaceKind.Side ? SplineBuildingTextureSurface.OuterWalls : SplineBuildingTextureSurface.Ends,
                SplineBuildingMeshBuilder.InnerWallSubmesh => kind == SplineWallFaceKind.Side ? SplineBuildingTextureSurface.InnerWalls : SplineBuildingTextureSurface.Ends,
                SplineBuildingMeshBuilder.FloorSubmesh => SplineBuildingTextureSurface.Floor,
                SplineBuildingMeshBuilder.CeilingSubmesh => SplineBuildingTextureSurface.Ceiling,
                _ => SplineBuildingTextureSurface.Roof
            };
        }


        // ── Развёртка ────────────────────────────────────────────────────

        // Object — ноль: отсчёты осей нужны только миру.
        public Vector2 WorldShiftOf(SplineWallTextureAxes axes)
        {
            if (!space.IsWorld)
                return Vector2.zero;

            return new Vector2(space.ShiftAlong(axes.UDirection) + axes.UBias, space.ShiftAlong(axes.VDirection) + axes.VBias);
        }


        public Vector2 Map(Vector2 planarMeters, Vector2 worldShift, int submesh, SplineWallFaceKind kind)
        {
            var material = submesh < materialsBySubmesh.Length ? materialsBySubmesh[submesh] : null;
            return LiveBuilderTextureMapping.Map(planarMeters, worldShift, fitOf(submesh, kind), LiveBuilderMaterialTiling.SizeOf(material));
        }
    }
}
