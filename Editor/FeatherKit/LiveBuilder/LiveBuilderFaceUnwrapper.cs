using System.Collections.Generic;
using FeatherKit.LiveBuilder;
using UnityEngine;

namespace FeatherKit.EditorTools.LiveBuilder
{
    /// <summary>
    /// Развёртка новых граней после вычитания (<see cref="MeshSubtractionPolygons"/>): стенки дыр, фаска, скосы рёбер.
    /// - плоские метры — на ближайшую из трёх плоскостей меша, как раньше, дальше — общая формула <see cref="LiveBuilderTextureMapping"/>
    /// - плитка — материала своей части меша, сдвиг до мира — постройки (World), подгонка — стороны выреза; у фаски и скосов — «как есть»
    /// - косой скат скоса у постройки World — по своей плоскости: U вдоль ребра, V вверх по скату, без растяжения
    /// </summary>
    public sealed class LiveBuilderFaceUnwrapper
    {
        // Грань косая, если её нормаль отходит от ближайшей оси хоть немного: у прямых граней этот косинус — ровно 1.
        private const float AxisAlignedCosine = 0.9999f;

        private readonly LiveBuilderTextureSpace space;
        private readonly IReadOnlyList<Material> materials;
        private readonly System.Func<Vector3, LiveBuilderTextureFit> fitOfNormal;
        private readonly bool unwrapsSlopesOnOwnPlane;


        public LiveBuilderFaceUnwrapper(LiveBuilderTextureSpace space, IReadOnlyList<Material> materials,
            System.Func<Vector3, LiveBuilderTextureFit> fitOfNormal = null, bool unwrapsSlopesOnOwnPlane = false)
        {
            this.space = space;
            this.materials = materials;
            this.fitOfNormal = fitOfNormal;
            this.unwrapsSlopesOnOwnPlane = unwrapsSlopesOnOwnPlane;
        }


        // ── Для кого ─────────────────────────────────────────────────────

        // Фаска и скосы объекта из коробок, материалы частей его меша — materials.
        public static LiveBuilderFaceUnwrapper ForModel(BoxModel model, IReadOnlyList<Material> materials)
        {
            return new LiveBuilderFaceUnwrapper(LiveBuilderTextureSpace.Of(model.transform, model.TextureAlignment), materials);
        }


        // Скосы рёбер: у World скат — по своей плоскости; у Object — как раньше, иначе поменялся бы вид старых пандусов.
        public LiveBuilderFaceUnwrapper ForSlopes()
        {
            return new LiveBuilderFaceUnwrapper(space, materials, fitOfNormal, space.IsWorld);
        }


        // Стенки дыры выреза в меше owner: подгонка — стороны выреза, куда стенка смотрит из дыры.
        public static LiveBuilderFaceUnwrapper ForCutter(Transform owner, Matrix4x4 cutterToMesh, GeometryCutter cutter, IReadOnlyList<Material> materials)
        {
            var space = LiveBuilderTextureSpace.OfMeshOwner(owner);

            if (cutter.TextureFits.IsEmpty)
                return new LiveBuilderFaceUnwrapper(space, materials);

            // Нормаль из осей меша в оси выреза — транспонированной матрицей; стенка смотрит в дыру, сторона выреза — наружу от неё.
            var meshToCutterNormal = cutterToMesh.transpose;
            return new LiveBuilderFaceUnwrapper(space, materials,
                normal => BoxModelSideFits.ForSide(cutter.TextureFits, SideOf(-meshToCutterNormal.MultiplyVector(normal))));
        }

        // Сторона (BoxFaces), куда смотрит нормаль.
        private static int SideOf(Vector3 normal)
        {
            var axis = NearestAxisOf(normal);
            return BoxFaces.Index(axis, normal[axis] > 0f);
        }


        // ── Развёртка ────────────────────────────────────────────────────

        // Нормаль — по плоскости грани; развёртка — метры на ближайшую плоскость меша (или на плоскость ската) через общую формулу.
        public void Unwrap(MeshBooleanPolygon polygon)
        {
            var normal = polygon.Plane.Normal;
            GetSurfaceAxes(normal, out var directionU, out var directionV);
            var worldShift = new Vector2(space.ShiftAlong(directionU), space.ShiftAlong(directionV));
            var fit = fitOfNormal?.Invoke(normal);
            var material = polygon.Submesh >= 0 && polygon.Submesh < materials.Count ? materials[polygon.Submesh] : null;
            var metersPerTile = LiveBuilderMaterialTiling.SizeOf(material);

            for (var index = 0; index < polygon.Vertices.Count; index++)
            {
                var vertex = polygon.Vertices[index];
                var position = vertex.Position;
                vertex.Normal = normal;
                vertex.TextureCoordinate = LiveBuilderTextureMapping.Map(new Vector2(Vector3.Dot(position, directionU), Vector3.Dot(position, directionV)),
                    worldShift, fit, metersPerTile);
                polygon.Vertices[index] = vertex;
            }
        }

        // Оси ближайшей плоскости меша; у косого ската — ось ребра и вверх по скату, у стоячего среза — по горизонтали и вверх.
        private void GetSurfaceAxes(Vector3 normal, out Vector3 directionU, out Vector3 directionV)
        {
            var axis = NearestAxisOf(normal);

            if (unwrapsSlopesOnOwnPlane && Mathf.Abs(normal[axis]) < AxisAlignedCosine)
            {
                var edgeAxis = FarthestAxisOf(normal);
                directionU = edgeAxis == 1 ? Vector3.Cross(Vector3.up, normal).normalized : BoxModelEdgeCutting.AxisVector(edgeAxis);
                directionV = edgeAxis == 1 ? Vector3.up : Vector3.Cross(normal, directionU).normalized;

                if (directionV.y < 0f)
                    directionV = -directionV;

                return;
            }

            directionU = BoxModelEdgeCutting.AxisVector(axis == 0 ? 2 : 0);
            directionV = BoxModelEdgeCutting.AxisVector(axis == 1 ? 2 : 1);
        }

        // Ось, вдоль которой грань не наклонена: у ската скоса — ось ребра.
        private static int FarthestAxisOf(Vector3 normal)
        {
            var absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            return absolute.x <= absolute.y && absolute.x <= absolute.z ? 0 : absolute.y <= absolute.z ? 1 : 2;
        }

        // Как у прежней развёртки: при равенстве — X раньше Y, Y раньше Z.
        private static int NearestAxisOf(Vector3 normal)
        {
            var absolute = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            return absolute.x >= absolute.y && absolute.x >= absolute.z ? 0 : absolute.y >= absolute.z ? 1 : 2;
        }
    }
}
