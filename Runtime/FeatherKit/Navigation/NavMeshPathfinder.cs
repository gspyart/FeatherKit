using FeatherKit.Attributes;
using FeatherKit.Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace FeatherKit.Navigation
{
    /// <summary>
    /// Маршрут по навмешу: считает путь до точки и отвечает на единственный вопрос —
    /// в какую сторону шагнуть СЕЙЧАС, чтобы дойти. Сам никого не двигает.
    ///
    /// Так, а не через <c>NavMeshAgent</c>: агент двигает объект сам, и вдвоём с телом
    /// персонажа они спорят за позицию в каждом кадре — выигрывает тот, кто написал
    /// последним. Здесь навмеш отвечает только «куда обойти», а разгон, доворот
    /// и притяжение остаются у движения, и оно ничего про навмеш не знает.
    ///
    /// Путь пересчитывается по интервалу, а не каждый кадр: расчёт стоит дорого,
    /// а стены за десятую долю секунды не переезжают. Уехавшая цель пересчитывает его
    /// раньше срока — иначе бегущий противник уводил бы за собой устаревший маршрут.
    ///
    /// Дойти нельзя — не молчим: <see cref="IsBlocked"/> означает, что навмеш довёл лишь
    /// до ближайшего к цели места. Что с этим делать — перепрыгнуть, обойти, отступиться —
    /// решает тот, кто ведёт персонажа.
    /// </summary>
    [FDescription("Маршрут по навмешу: считает путь и говорит, куда шагнуть сейчас. Никого не двигает — направление берёт тот, кто ведёт персонажа.")]
    public class NavMeshPathfinder : MonoBehaviour
    {
        // Углов в одном пути больше этого не бывает даже на кривой карте. Массив постоянный:
        // путь пересчитывается по нескольку раз в секунду, и новый на каждый расчёт —
        // мусор на ровном месте.
        private const int MaxCorners = 64;

        // Сдвиг больше этого между двумя вопросами — не шаг, а перенос: прыжок, телепорт,
        // выдача из пула. Старый маршрут после такого ведёт с прошлого места, и держаться
        // за него нельзя.
        private const float TeleportStep = 3f;

        [Tooltip("Тип агента: под чьи габариты запечена поверхность. У большого персонажа " +
                 "он свой — иначе тот пойдёт там, где не пролезет телом")]
        [NavMeshAgentType]
        [SerializeField] private int agentType;

        [Tooltip("Как часто пересчитывать путь, сек. Чаще десятой доли незачем: стены " +
                 "не переезжают, а расчёт стоит дорого")]
        [Min(0f)]
        [SerializeField] private float recalculateInterval = 0.3f;

        [Tooltip("На сколько метров должна уехать цель, чтобы пересчитать путь раньше " +
                 "срока. Ноль — пересчитывать только по времени")]
        [Min(0f)]
        [SerializeField] private float destinationTolerance = 1.5f;

        [Tooltip("Насколько близко подойти к углу пути, чтобы считать его пройденным, м. " +
                 "Слишком мало — персонаж крутится вокруг угла, не дотягиваясь до него")]
        [Min(0.05f)]
        [SerializeField] private float cornerReach = 0.6f;

        [Tooltip("Как далеко искать поверхность под персонажем, если он стоит вне неё, м")]
        [Min(0f)]
        [SerializeField] private float sampleDistance = 4f;

        private readonly Vector3[] corners = new Vector3[MaxCorners];

        private NavMeshPath path;
        private Vector3 destination;
        private Vector3 lastSeenPosition;
        private float nextCalculateTime;
        private int cornerCount;
        private int cornerIndex;


        /// <summary>Есть ли куда идти: маршрут посчитан и ещё не пройден до конца.</summary>
        public bool HasPath => cornerIndex < cornerCount;

        /// <summary>
        /// Дойти до цели нельзя: путь не нашёлся вовсе или довёл только до ближайшего
        /// к ней места. Персонаж упрётся в край и встанет, поэтому спрашивать это стоит
        /// тому, у кого есть запасной способ добраться.
        /// </summary>
        public bool IsBlocked { get; private set; }

        /// <summary>К какой точке маршрут посчитан.</summary>
        public Vector3 Destination => destination;

        /// <summary>
        /// Под какие габариты считается путь. Спрашивают те, кому надо выбрать место
        /// на той же поверхности, — например, куда приземлиться после прыжка.
        /// </summary>
        public int AgentType => agentType;


        // ── Направление ───────────────────────────────────────────────────

        /// <summary>
        /// Куда шагнуть, чтобы дойти до точки. Звать каждый кадр — пересчётом маршрута
        /// компонент распоряжается сам.
        ///
        /// Маршрута нет — отдаём направление напрямую: лучше пойти в стену, чем встать
        /// столбом там, где поверхность просто забыли запечь.
        /// </summary>
        public Vector3 DirectionTo(Vector3 point)
        {
            Refresh(point);

            var corner = HasPath ? corners[cornerIndex] : point;

            return FDistanceUtils.FlatDirection(transform.position, corner);
        }

        /// <summary>Забыть маршрут: передумали идти, или персонаж переехал в другое место.</summary>
        public void Clear()
        {
            cornerCount = 0;
            cornerIndex = 0;
            nextCalculateTime = 0f;

            IsBlocked = false;
        }

        private void Refresh(Vector3 point)
        {
            if (ShouldRecalculate(point))
                Recalculate(point);

            AdvanceCorner();
        }

        // Время игровое: Time.time идёт с учётом скорости игры, поэтому в замедлении
        // маршрут пересчитывается так же реже, как и всё остальное.
        private bool ShouldRecalculate(Vector3 point)
        {
            if (!HasPath || Time.time >= nextCalculateTime)
                return true;

            // Персонажа перенесло — маршрут остался от прошлого места и повёл бы обратно.
            if (FDistanceUtils.FlatDistance(transform.position, lastSeenPosition) > TeleportStep)
                return true;

            return destinationTolerance > 0f &&
                   FDistanceUtils.FlatDistance(destination, point) > destinationTolerance;
        }

        private void Recalculate(Vector3 point)
        {
            destination = point;
            lastSeenPosition = transform.position;
            nextCalculateTime = Time.time + recalculateInterval;

            cornerCount = 0;
            cornerIndex = 0;

            path ??= new NavMeshPath();

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agentType,
                areaMask = NavMesh.AllAreas
            };

            // Персонаж мог сойти с поверхности: по земле его ведёт физика, а она про
            // навмеш не знает. Считаем от ближайшего запечённого места, а не от пяток.
            if (!TrySnap(transform.position, filter, out var from) ||
                !TrySnap(point, filter, out var to) ||
                !NavMesh.CalculatePath(from, to, filter, path))
            {
                IsBlocked = true;

                return;
            }

            cornerCount = path.GetCornersNonAlloc(corners);

            // Первый угол — это место, где персонаж и так стоит: идти в него незачем.
            cornerIndex = cornerCount > 1 ? 1 : 0;

            IsBlocked = path.status != NavMeshPathStatus.PathComplete;
        }

        private bool TrySnap(Vector3 point, NavMeshQueryFilter filter, out Vector3 result)
        {
            if (NavMesh.SamplePosition(point, out var hit, sampleDistance, filter))
            {
                result = hit.position;

                return true;
            }

            result = point;

            return false;
        }

        // Пройденные углы снимаем пачкой, а не по одному за кадр: рывком персонажа может
        // пронести сразу через несколько, и возвращаться к оставшемуся позади он не должен.
        private void AdvanceCorner()
        {
            lastSeenPosition = transform.position;

            while (HasPath &&
                   FDistanceUtils.FlatDistance(transform.position, corners[cornerIndex]) <= cornerReach)
                cornerIndex++;
        }


        // ── Unity ─────────────────────────────────────────────────────────

        // Объект уезжает в пул и вернётся в другом углу карты: чужой маршрут повёл бы его
        // к точке, к которой он больше не идёт.
        private void OnDisable()
        {
            Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !HasPath)
                return;

            // Красный — путь не доводит до цели: видно сразу, что персонаж упрётся.
            Gizmos.color = IsBlocked ? Color.red : Color.cyan;

            var previous = transform.position;

            for (var i = cornerIndex; i < cornerCount; i++)
            {
                Gizmos.DrawLine(previous, corners[i]);

                previous = corners[i];
            }
        }
    }
}
