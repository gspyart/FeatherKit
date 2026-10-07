using FeatherKit.Attributes;
using UnityEngine;

namespace FeatherKit.Navigation
{
    /// <summary>
    /// Обход препятствий на глаз: осматривается лучами вокруг себя и отвечает на один
    /// вопрос — в какую сторону шагнуть, чтобы не упереться. Сам никого не двигает.
    ///
    /// Нужен там, где маршрута нет и быть не может: персонаж пятится, отскакивает,
    /// кружит вокруг цели. <see cref="NavMeshPathfinder"/> отвечает на «как дойти
    /// ДО ТОЧКИ», а здесь точки нет вовсе — есть только направление, в котором хочется
    /// идти. Работают они вместе: маршрут ведёт в обход стен, сканер поправляет шаг
    /// на месте.
    ///
    /// Каждому направлению из веера считаются два числа: СВОБОДА — сколько до стены
    /// в долях дальности обзора, и ЖЕЛАННОСТЬ — насколько оно совпадает с нужным.
    /// Идём туда, где произведение больше: свободный проход вбок обыгрывает забитое
    /// намертво «прямо», а в тупике персонаж уходит туда, где просторнее.
    ///
    /// Веер считается по интервалу, а не каждый кадр, и выбранное направление
    /// доворачивается плавно. Без этого персонаж на углу дёргался бы между двумя
    /// одинаково хорошими сторонами по нескольку раз в секунду.
    ///
    /// Смотрит компонент не лучом, а ШАРОМ в ширину тела: узкую щель луч принял бы
    /// за проход, и персонаж застрял бы в ней плечом. Отсюда и главная тонкость высоты:
    /// шар занимает по вертикали радиус вниз и радиус вверх, поэтому просвет под ним
    /// задаётся отдельно от радиуса, а центр считается уже сам.
    ///
    /// Иначе выходило молча и наглухо: шар радиусом в полметра, поставленный на высоту
    /// 0.2, оказывался утоплен в пол на треть метра. Пол лежит в тех же слоях, что стены,
    /// и пересечённый на старте коллайдер возвращается с нулевой дистанцией — во ВСЕ
    /// стороны разом. Обход при этом не ругается, а просто отдаёт направление как есть:
    /// снаружи это выглядит как «сканер выключился», и понять причину можно только
    /// по пропавшим лучам в гизмо.
    ///
    /// Собственное тело лучи пропускают. Иначе маску пришлось бы вычищать от слоя
    /// персонажей, а пока слои не разведены, персонаж считал бы себя запертым со всех
    /// сторон в чистом поле.
    ///
    /// Пропустить можно и ещё кого-то — того, К КОМУ идут. Цель препятствием быть
    /// не может: приняв её за стену, персонаж встал бы в паре шагов и пошёл кружить
    /// вокруг вместо того, чтобы дойти.
    ///
    /// Второй вопрос, на который он отвечает, — «а вижу ли я вон ту точку» и «куда
    /// отойти, чтобы увидеть»: между нами и ею стены нет, а если есть — с какой стороны
    /// её обойти. Здесь же это живёт потому, что спрашивает ровно то же самое — маску
    /// стен, своё тело и того, к кому идут. Заведи это отдельным компонентом, и маска
    /// окажется в двух местах, а разъедутся они молча.
    /// </summary>
    [FDescription("Обход препятствий лучами: говорит, куда шагнуть, чтобы не упереться в стену, видно ли отсюда точку и куда отойти, чтобы увидеть. Никого не двигает — направление берёт тот, кто ведёт персонажа.")]
    public class ObstacleScanner : MonoBehaviour
    {
        // Насколько желанным считается направление точно назад. Не ноль намеренно:
        // в тупике развернуться лучше, чем упираться в стену перед собой.
        private const float MinInterest = 0.15f;

        // Нужное направление отвернуло больше этого — персонаж передумал, и держаться
        // за прошлый осмотр нельзя. Иначе разворот на месте ждал бы конца интервала.
        private const float TurnToRescan = 45f;

        [Tooltip("Что считается препятствием: стены, скалы, дома. Своё тело лучи " +
                 "пропускают, а вот чужие — нет: положишь сюда персонажей — персонаж " +
                 "станет обходить и собственную цель")]
        [SerializeField] private LayerMask obstacles;

        [Tooltip("Как далеко смотреть, м. Примерно два шага: дальше персонаж начинает " +
                 "обходить то, мимо чего и так прошёл бы")]
        [Min(0.1f)]
        [SerializeField] private float lookAhead = 2.5f;

        [Tooltip("Полуширина тела, м. Обычно как радиус у CharacterController: узкую " +
                 "щель персонаж иначе примет за проход и застрянет в ней плечом")]
        [Min(0.05f)]
        [SerializeField] private float bodyRadius = 0.4f;

        [Tooltip("Просвет ПОД шаром, м: на сколько он проходит над землёй своим низом. " +
                 "Ровно столько и будет на самом деле — радиус прибавляется сам. По земле " +
                 "шар цеплялся бы за каждый порог, а утопленный в неё не видит ничего")]
        [Min(0.01f)]
        [SerializeField] private float groundClearance = 0.4f;

        [Tooltip("Сколько направлений в веере. Восьми хватает: больше — точнее обход, " +
                 "но и лучей на каждый осмотр столько же")]
        [Range(4, 16)]
        [SerializeField] private int directionCount = 8;

        [Tooltip("Как часто осматриваться, сек. Чаще незачем: стены не переезжают, " +
                 "а лучи стоят денег")]
        [Min(0f)]
        [SerializeField] private float scanInterval = 0.15f;

        [Tooltip("Как быстро доворачивать шаг к выбранной стороне, градусов в секунду. " +
                 "Меньше — персонаж входит в обход дугой, больше — щёлкает на месте")]
        [Min(1f)]
        [SerializeField] private float turnSpeed = 540f;

        // Один на всех: лучи считаются по очереди, и держать по буферу на каждого
        // персонажа незачем.
        private static readonly RaycastHit[] hits = new RaycastHit[8];

        private Vector3 steering;
        private Vector3 chosen;
        private Vector3 lastWanted;
        private Transform ignored;
        private float nextScanTime;


        // ── Где смотрим ───────────────────────────────────────────────────

        /// <summary>
        /// Откуда пускать шар: столько над ногами, чтобы под ним остался заданный просвет.
        /// Радиус прибавляем здесь, а не просим настройщика держать его в уме, — иначе
        /// каждая правка радиуса молча ломала бы высоту.
        /// </summary>
        private Vector3 Origin => transform.position + Vector3.up * (groundClearance + bodyRadius);


        // ── Направление ───────────────────────────────────────────────────

        /// <summary>
        /// Куда шагнуть, чтобы дойти туда, куда хочется, и не упереться. Звать каждый
        /// кадр — осмотром и плавностью компонент распоряжается сам.
        ///
        /// Дорога свободна — отдаём нужное направление как есть, даже без веера: обходить
        /// нечего, и лишние лучи в открытом поле не нужны.
        ///
        /// <paramref name="ignoredBody"/> — кого не считать препятствием, кроме себя.
        /// Сюда отдают того, к кому идут: он тело, но не стена.
        /// </summary>
        public Vector3 DirectionAround(Vector3 wanted, Transform ignoredBody = null)
        {
            ignored = ignoredBody;
            wanted = Flatten(wanted);

            if (wanted == Vector3.zero)
                return Vector3.zero;

            if (ShouldScan(wanted))
                Rescan(wanted);

            // Первый шаг доворачивать не от чего: начинаем прямо с выбранной стороны.
            if (steering == Vector3.zero)
                steering = chosen;

            steering = Vector3.RotateTowards(steering, chosen,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f).normalized;

            return steering;
        }

        /// <summary>
        /// Выбрать свободную сторону ПРЯМО СЕЙЧАС, одним разом. Для разового движения —
        /// рывка, отскока, прыжка: доворачивать там нечего, направление нужно сразу
        /// и целиком.
        /// </summary>
        public Vector3 PickFreeDirection(Vector3 wanted, Transform ignoredBody = null)
        {
            ignored = ignoredBody;
            wanted = Flatten(wanted);

            return wanted == Vector3.zero ? Vector3.zero : Scan(wanted);
        }

        /// <summary>Забыть обход: персонаж встал, переехал или вернулся из пула.</summary>
        public void Clear()
        {
            steering = Vector3.zero;
            chosen = Vector3.zero;
            lastWanted = Vector3.zero;
            ignored = null;
            nextScanTime = 0f;
        }

        private bool ShouldScan(Vector3 wanted)
        {
            return chosen == Vector3.zero ||
                   Time.time >= nextScanTime ||
                   Vector3.Angle(wanted, lastWanted) > TurnToRescan;
        }

        private void Rescan(Vector3 wanted)
        {
            chosen = Scan(wanted);
            lastWanted = wanted;
            nextScanTime = Time.time + scanInterval;
        }

        // Веер считается от нужного направления, а не от осей мира: тогда первый луч —
        // это всегда «прямо туда, куда хотел», и на открытом месте им всё и кончается.
        private Vector3 Scan(Vector3 wanted)
        {
            var origin = Origin;
            var freedom = Freedom(origin, wanted);

            if (freedom >= 1f)
                return wanted;

            // Нужное направление — такой же участник сравнения, просто желанность у него
            // единица. Иначе слегка задетое «прямо» проигрывало бы любому объезду.
            var best = wanted;
            var bestScore = freedom;
            var step = 360f / directionCount;

            for (var i = 1; i < directionCount; i++)
            {
                var candidate = Quaternion.Euler(0f, step * i, 0f) * wanted;
                var score = Freedom(origin, candidate) * Interest(candidate, wanted);

                if (score <= bestScore)
                    continue;

                best = candidate;
                bestScore = score;
            }

            return best;
        }

        // Ноль — упёрлись прямо сейчас, единица — до самого края обзора чисто.
        //
        // Берём все попадания, а не ближайшее: ближайшим почти всегда оказывается
        // собственное тело — луч выходит из его середины.
        private float Freedom(Vector3 origin, Vector3 direction)
        {
            var count = Physics.SphereCastNonAlloc(origin, bodyRadius, direction, hits,
                lookAhead, obstacles, QueryTriggerInteraction.Ignore);

            var nearest = lookAhead;

            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];

                if (hit.collider == null || IsOwn(hit.collider.transform))
                    continue;

                if (hit.distance < nearest)
                    nearest = hit.distance;
            }

            return Mathf.Clamp01(nearest / lookAhead);
        }

        private bool IsOwn(Transform body)
        {
            return IsOwn(body, ignored);
        }

        private bool IsOwn(Transform body, Transform ignoredBody)
        {
            return body.IsChildOf(transform) || (ignoredBody != null && body.IsChildOf(ignoredBody));
        }

        private float Interest(Vector3 candidate, Vector3 wanted)
        {
            var match = 0.5f + 0.5f * Vector3.Dot(candidate, wanted);

            return Mathf.Lerp(MinInterest, 1f, match);
        }

        private Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }


        // ── Видимость ─────────────────────────────────────────────────────

        /// <summary>
        /// Между персонажем и точкой чисто: стены на прямой нет. Спрашивают перед тем,
        /// что летит по прямой, — замахом, выстрелом, броском: бить в камень, за которым
        /// стоит противник, незачем.
        ///
        /// Смотрит ЛУЧОМ, а не шаром в ширину тела: шар цепляет угол стены, мимо которого
        /// удар прошёл бы, и персонаж отказывался бы бить из-за укрытия, где всё видно.
        ///
        /// Идёт луч от той же высоты, что и обход, — по груди. От ног он утыкался бы
        /// в каждый порог, а от макушки проходил бы над забором.
        ///
        /// <paramref name="ignoredBody"/> — кого не считать стеной, кроме себя. Сюда
        /// отдают того, на кого и смотрят: собой он обзор не закрывает.
        /// </summary>
        public bool HasClearLineTo(Vector3 point, Transform ignoredBody = null)
        {
            return HasClearLine(Origin, point, ignoredBody);
        }

        /// <summary>
        /// Куда отойти вбок, чтобы точка ОТКРЫЛАСЬ: за камнем стоять и ждать нечего.
        ///
        /// Пробуем обе стороны, и не наугад: сначала смотрим, докуда по этой стороне
        /// вообще можно дойти, и уже ОТТУДА проверяем линию. Мерить от места, до которого
        /// не дойти, — то же самое, что не мерить вовсе.
        ///
        /// Любимая сторона идёт первой. Без этого персонаж, у которого открываются обе,
        /// перевыбирал бы их по очереди и топтался бы на месте.
        ///
        /// Не открылось ни с той, ни с другой — отходим туда, где просто просторнее:
        /// за шаг картина меняется, и на следующем осмотре сторона может подойти.
        /// Совсем некуда идти — false, и решать, что делать дальше, будет спрашивающий.
        ///
        /// Место отдаём по ногам, а не по груди: к нему пойдут по навмешу, а тот живёт
        /// на земле.
        /// </summary>
        public bool TryFindSidestep(Vector3 point, out Vector3 spot, float preferredSide = 1f,
            Transform ignoredBody = null)
        {
            ignored = ignoredBody;
            spot = transform.position;

            var origin = Origin;
            var toPoint = Flatten(point - origin);

            if (toPoint == Vector3.zero)
                return false;

            var right = Vector3.Cross(Vector3.up, toPoint);
            var favourite = preferredSide < 0f ? -1f : 1f;

            for (var i = 0; i < 2; i++)
            {
                var step = right * (i == 0 ? favourite : -favourite);
                var reach = Freedom(origin, step) * lookAhead;

                // Упёрлись сразу — с этой стороны и шагу не сделать.
                if (reach < bodyRadius)
                    continue;

                if (!HasClearLine(origin + step * reach, point, ignoredBody))
                    continue;

                spot = transform.position + step * reach;

                return true;
            }

            var free = PickFreeDirection(right * favourite, ignoredBody);

            if (free == Vector3.zero)
                return false;

            spot = transform.position + free * lookAhead;

            return true;
        }

        private bool HasClearLine(Vector3 from, Vector3 to, Transform ignoredBody)
        {
            var offset = to - from;
            var distance = offset.magnitude;

            if (distance < 0.01f)
                return true;

            var count = Physics.RaycastNonAlloc(from, offset / distance, hits, distance,
                obstacles, QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];

                if (hit.collider != null && !IsOwn(hit.collider.transform, ignoredBody))
                    return false;
            }

            return true;
        }

        // ── Unity ─────────────────────────────────────────────────────────

        // Объект уезжает в пул и вернётся в другом углу карты: чужой обход повёл бы его
        // вбок на первом же кадре.
        private void OnDisable()
        {
            Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || chosen == Vector3.zero)
                return;

            var origin = Origin;
            var step = 360f / directionCount;

            for (var i = 0; i < directionCount; i++)
            {
                var direction = Quaternion.Euler(0f, step * i, 0f) * lastWanted;
                var freedom = Freedom(origin, direction);

                // Зелёный — чисто, красный — упёрлись: видно, почему выбрана эта сторона.
                Gizmos.color = Color.Lerp(Color.red, Color.green, freedom);
                Gizmos.DrawRay(origin, direction * lookAhead * freedom);

                // Шар в точке остановки: без него линия читается как тонкий луч, и то,
                // что персонаж меряет проход в ширину тела, увидеть неоткуда.
                Gizmos.DrawWireSphere(origin + direction * lookAhead * freedom, bodyRadius);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, steering * lookAhead);
        }
    }
}
