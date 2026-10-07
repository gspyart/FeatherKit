using System.Collections.Generic;
using FeatherKit.Helpers;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Фигуры и подписи для гизмо — то, чего нет в <see cref="Gizmos"/>: круг по земле,
    /// заливка, сектор «радиус плюс угол», стрелка направления, текст в точке мира
    /// и силуэт префаба.
    ///
    /// Зачем: почти всё, что настраивают в инспекторе, — это радиус и угол. Сфера вместо
    /// круга и две линии вместо сектора читаются плохо, а рисовать их руками в каждом
    /// компоненте значит переписывать одно и то же.
    ///
    /// Цвет по умолчанию берётся из <c>Gizmos.color</c>: вызывающему не нужно помнить
    /// про второй набор цветов — поставил один раз, рисует всё подряд.
    ///
    /// В сборке методы пустые: рисует это Handles, а он живёт только в редакторе. Поэтому
    /// звать их можно прямо из OnDrawGizmos, не оборачивая каждый вызов в #if, — сам-то
    /// метод в сборку попадает, просто никто его не зовёт.
    /// </summary>
    public static class FGizmos
    {
        /// <summary>Высота текста в мире по умолчанию, м. До множителя из настроек гизмо.</summary>
        public const float DefaultTextHeight = 1f;

#if UNITY_EDITOR
        // Мера, в которой HandleUtility отвечает про размер: GetHandleSize возвращает,
        // сколько метров мира укладывается в эти 80 экранных пикселей.
        private const float HandlePixels = 80f;

        // Мельче уже не прочитать, и на экране от этого только грязь.
        private const int MinFontSize = 7;

        // Крупнее нет смысла: подпись перекрыла бы то, что подписывает.
        private const int MaxFontSize = 60;

        // Сколько секунд живёт разобранный префаб, прежде чем его разберут заново.
        private const double PartsLifetime = 2f;

        // Стиль один на все подписи: новый GUIStyle в каждом вызове — это мусор на каждой
        // перерисовке сцены, а зовут нас с каждого объекта.
        private static GUIStyle style;

        // Разобранные префабы: ключ — сам префаб, значение — его меши и время разбора.
        private static readonly Dictionary<ulong, (PrefabMeshPart[] Parts, double BuiltAt)> partsByPrefab =
            new Dictionary<ulong, (PrefabMeshPart[], double)>();
#endif


        // ── Круг ──────────────────────────────────────────────────────────

        /// <summary>
        /// Контур круга. Плоскость задаёт <paramref name="normal"/>, по умолчанию
        /// горизонтальная: радиусы почти всегда меряют по земле.
        /// </summary>
        public static void Circle(Vector3 center, float radius, Color? color = null, Vector3 normal = default)
        {
#if UNITY_EDITOR
            if (radius <= 0f)
                return;

            Apply(color);

            Handles.DrawWireDisc(center, Axis(normal), radius);
#endif
        }

        /// <summary>
        /// Круг с заливкой. Цвет берут полупрозрачный: сплошной закроет собой то, что под
        /// ним, и настраивать станет нечего.
        /// </summary>
        public static void SolidCircle(Vector3 center, float radius, Color? color = null, Vector3 normal = default)
        {
#if UNITY_EDITOR
            if (radius <= 0f)
                return;

            Apply(color);

            Handles.DrawSolidDisc(center, Axis(normal), radius);
#endif
        }


        // ── Сектор ────────────────────────────────────────────────────────

        /// <summary>
        /// Сектор: радиус плюс угол — та самая пара, которой задают «что перед героем».
        ///
        /// <paramref name="angle"/> — ПОЛНАЯ ширина в градусах, симметрично вокруг
        /// <paramref name="direction"/>: в инспекторе пишут «120 градусов обзора»,
        /// а не «по шестьдесят в каждую сторону».
        /// </summary>
        public static void Sector(Vector3 center, Vector3 direction, float radius, float angle,
            Color? color = null, Vector3 normal = default)
        {
#if UNITY_EDITOR
            if (radius <= 0f)
                return;

            var axis = Axis(normal);
            var flat = Flatten(direction, axis);

            Apply(color);

            // Полный круг — сектора нет: две сходящиеся линии в середине только мешали бы.
            if (angle >= 360f || flat == Vector3.zero)
            {
                Handles.DrawWireDisc(center, axis, radius);
                return;
            }

            var from = Quaternion.AngleAxis(-angle * 0.5f, axis) * flat;
            var to = Quaternion.AngleAxis(angle * 0.5f, axis) * flat;

            Handles.DrawWireArc(center, axis, from, angle, radius);
            Handles.DrawLine(center, center + from * radius);
            Handles.DrawLine(center, center + to * radius);
#endif
        }

        /// <summary>Сектор с заливкой. Цвет — полупрозрачный, как и у круга.</summary>
        public static void SolidSector(Vector3 center, Vector3 direction, float radius, float angle,
            Color? color = null, Vector3 normal = default)
        {
#if UNITY_EDITOR
            if (radius <= 0f)
                return;

            var axis = Axis(normal);
            var flat = Flatten(direction, axis);

            Apply(color);

            if (angle >= 360f || flat == Vector3.zero)
            {
                Handles.DrawSolidDisc(center, axis, radius);
                return;
            }

            Handles.DrawSolidArc(center, axis, Quaternion.AngleAxis(-angle * 0.5f, axis) * flat, angle, radius);
#endif
        }


        // ── Точка и направление ───────────────────────────────────────────

        /// <summary>Стрелка: куда смотрит, куда летит, куда тянет. Длина в метрах.</summary>
        public static void Arrow(Vector3 from, Vector3 direction, float length, Color? color = null)
        {
#if UNITY_EDITOR
            if (length <= 0f || direction.sqrMagnitude < 0.000001f)
                return;

            Apply(color);

            var forward = direction.normalized;
            var end = from + forward * length;

            Handles.DrawLine(from, end);

            // Наконечник разводим поперёк стрелки. У вертикальной стрелки вертикальная ось
            // вырождается — тогда годится любая другая поперечина.
            var axis = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            var head = length * 0.2f;

            Handles.DrawLine(end, end + Quaternion.AngleAxis(150f, axis) * forward * head);
            Handles.DrawLine(end, end + Quaternion.AngleAxis(-150f, axis) * forward * head);
#endif
        }

        /// <summary>Крестик: пометить место, у которого нет ни размера, ни объекта.</summary>
        public static void Cross(Vector3 center, float size, Color? color = null)
        {
#if UNITY_EDITOR
            if (size <= 0f)
                return;

            Apply(color);

            var half = size * 0.5f;

            Handles.DrawLine(center - Vector3.right * half, center + Vector3.right * half);
            Handles.DrawLine(center - Vector3.up * half, center + Vector3.up * half);
            Handles.DrawLine(center - Vector3.forward * half, center + Vector3.forward * half);
#endif
        }


        // ── Текст ─────────────────────────────────────────────────────────

        /// <summary>Подпись в точке мира текущим цветом гизмо.</summary>
        public static void Label(Vector3 position, string text, float height = DefaultTextHeight)
        {
            Label(position, text, Gizmos.color, height);
        }

        /// <summary>
        /// Подпись в точке мира. <paramref name="height"/> — высота текста в МЕТРАХ,
        /// а не в пикселях: отсюда и берётся «уменьшается с расстоянием».
        ///
        /// Размер умножается на <c>Gizmos.probeSize</c> — ползунок размера гизмо в сцене.
        /// Подписей обычно много, и крутить их по одной незачем. Совсем мелкая не рисуется
        /// вовсе: иначе сцена превращается в кашу из букв, сквозь которую её не видно.
        /// </summary>
        public static void Label(Vector3 position, string text, Color color, float height = DefaultTextHeight)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(text))
                return;

            var fontSize = FontSize(position, height);

            if (fontSize <= 0)
                return;

            if (style == null)
                style = new GUIStyle { alignment = TextAnchor.MiddleCenter };

            style.fontSize = fontSize;
            style.normal.textColor = color;

            Handles.Label(position, text, style);
#endif
        }


        // ── Силуэт префаба ────────────────────────────────────────────────

        /// <summary>
        /// Призрак префаба в точке: его собственные меши там, где он встанет.
        ///
        /// Для спавнеров и любых меток, за которыми стоит префаб. Крестик говорит только
        /// «тут что-то будет», а силуэт — что именно и влезет ли оно между стенами. Меш
        /// берётся у самого префаба, поэтому разойтись с игрой гизмо не может.
        ///
        /// Полупрозрачной заливкой, а не контуром: у персонажа тысячи рёбер, и проволока
        /// из них читается как грязь.
        /// </summary>
        public static void Prefab(GameObject prefab, Vector3 position, Quaternion rotation, Color? color = null)
        {
#if UNITY_EDITOR
            if (prefab == null)
                return;

            var root = Matrix4x4.TRS(position, rotation, Vector3.one);
            var previous = Gizmos.matrix;

            Gizmos.color = color ?? Gizmos.color;

            foreach (var part in PartsOf(prefab))
            {
                // Через матрицу, а не позицией с разворотом: у кусков префаба бывает свой
                // масштаб, и другого способа передать его гизмо нет.
                Gizmos.matrix = root * part.Local;

                Gizmos.DrawMesh(part.Mesh, Vector3.zero, Quaternion.identity, Vector3.one);
            }

            Gizmos.matrix = previous;
#endif
        }

        /// <summary>
        /// То же, когда префаб назван компонентом: у спавнеров ссылка обычно такая —
        /// не на объект, а на моба, сундук, тело предмета.
        /// </summary>
        public static void Prefab(Component prefab, Vector3 position, Quaternion rotation, Color? color = null)
        {
            Prefab(prefab != null ? prefab.gameObject : null, position, rotation, color);
        }

#if UNITY_EDITOR
        // Разбор префаба — это обход всей его иерархии, а зовут нас с каждой перерисовки
        // сцены. Поэтому запоминаем, но ненадолго: префаб правят прямо во время работы,
        // и силуэт, застрявший на прежней версии, врал бы до перекомпиляции.
        private static PrefabMeshPart[] PartsOf(GameObject prefab)
        {
            var key = prefab.GetStableId();
            var now = EditorApplication.timeSinceStartup;

            if (partsByPrefab.TryGetValue(key, out var cached) && now - cached.BuiltAt < PartsLifetime)
                return cached.Parts;

            var parts = CollectParts(prefab);

            partsByPrefab[key] = (parts, now);

            return parts;
        }

        // Меши берём и обычные, и скелетные: у сундука первое, у персонажа второе.
        // Скелетный рисуется в позе привязки — это T-поза, и для силуэта её хватает.
        private static PrefabMeshPart[] CollectParts(GameObject prefab)
        {
            var parts = new List<PrefabMeshPart>();
            var root = prefab.transform;

            // Масштаб корня в эту матрицу НЕ входит намеренно: обычный worldToLocalMatrix
            // поделил бы на него, и префаб, увеличенный в полтора раза, рисовался бы
            // обычного размера. Свой масштаб объект уносит с собой, когда его ставят
            // в сцену, — значит он часть силуэта.
            var toRoot = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
            var hidden = LowerLodRenderers(prefab);

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                AddPart(parts, filter.sharedMesh, filter.GetComponent<MeshRenderer>(), toRoot, hidden);

            foreach (var skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AddPart(parts, skinned.sharedMesh, skinned, toRoot, hidden);

            return parts.ToArray();
        }

        private static void AddPart(List<PrefabMeshPart> parts, Mesh mesh, Renderer renderer,
            Matrix4x4 toRoot, HashSet<Renderer> hidden)
        {
            // Выключенного в игре не видно — и в силуэте ему делать нечего.
            if (mesh == null || renderer == null || !renderer.enabled || !renderer.gameObject.activeSelf)
                return;

            if (hidden != null && hidden.Contains(renderer))
                return;

            parts.Add(new PrefabMeshPart(mesh, toRoot * renderer.transform.localToWorldMatrix));
        }

        // Дальние ступени LOD лежат ровно поверх ближней. Нарисуй их все — и полупрозрачный
        // силуэт ляжет в три слоя: та часть модели, у которой есть LOD, выйдет темнее.
        private static HashSet<Renderer> LowerLodRenderers(GameObject prefab)
        {
            HashSet<Renderer> lower = null;
            HashSet<Renderer> nearest = null;

            foreach (var group in prefab.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = group.GetLODs();

                for (var i = 0; i < levels.Length; i++)
                {
                    if (i == 0)
                        nearest = Collect(levels[i].renderers, nearest);
                    else
                        lower = Collect(levels[i].renderers, lower);
                }
            }

            // Один и тот же рендерер бывает сразу на нескольких ступенях — такой остаётся.
            if (lower != null && nearest != null)
                lower.ExceptWith(nearest);

            return lower;
        }

        private static HashSet<Renderer> Collect(Renderer[] renderers, HashSet<Renderer> into)
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                into ??= new HashSet<Renderer>();

                into.Add(renderer);
            }

            return into;
        }
#endif


#if UNITY_EDITOR
        // ── Своё ──────────────────────────────────────────────────────────

        // Сколько пикселей займёт текст с такой высотой в мире. Ноль — рисовать не стоит:
        // либо мелко до нечитаемости, либо точка вообще за спиной у камеры.
        private static int FontSize(Vector3 position, float height)
        {
            var camera = Camera.current;

            // За спиной камеры Handles выбросил бы подпись на другую сторону экрана,
            // и она повисла бы над чужим объектом.
            if (camera != null && camera.WorldToViewportPoint(position).z <= 0f)
                return 0;

            var handle = HandleUtility.GetHandleSize(position);

            if (handle <= 0.0001f)
                return 0;

            var world = Mathf.Max(0f, height) * Mathf.Max(0f, Gizmos.probeSize);
            var pixels = Mathf.RoundToInt(world * HandlePixels / handle);

            return pixels < MinFontSize ? 0 : Mathf.Min(pixels, MaxFontSize);
        }

        // Пустая нормаль — горизонтальная плоскость: так задают радиусы почти всегда,
        // и писать Vector3.up в каждом вызове незачем.
        private static Vector3 Axis(Vector3 normal)
        {
            return normal == Vector3.zero ? Vector3.up : normal.normalized;
        }

        // Направление кладём в плоскость сектора: у смотрящего чуть вверх персонажа
        // сектор иначе разъезжается с кругом.
        private static Vector3 Flatten(Vector3 direction, Vector3 axis)
        {
            var flat = Vector3.ProjectOnPlane(direction, axis);

            return flat.sqrMagnitude < 0.000001f ? Vector3.zero : flat.normalized;
        }

        private static void Apply(Color? color)
        {
            Handles.color = color ?? Gizmos.color;
        }


        /// <summary>
        /// Один меш префаба и его место ВНУТРИ префаба. Считается по разу на префаб:
        /// на перерисовке остаётся домножить на матрицу точки, где силуэт рисуют.
        /// </summary>
        private readonly struct PrefabMeshPart
        {
            public PrefabMeshPart(Mesh mesh, Matrix4x4 local)
            {
                Mesh = mesh;
                Local = local;
            }


            public Mesh Mesh { get; }

            public Matrix4x4 Local { get; }
        }
#endif
    }
}
