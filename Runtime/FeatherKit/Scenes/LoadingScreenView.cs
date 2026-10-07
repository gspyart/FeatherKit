using FeatherKit.Attributes;
using FeatherKit.Timers;
using TMPro;
using UnityEngine;

namespace FeatherKit.Scenes
{
    /// <summary>
    /// Экран загрузки: картинка медленно наезжает, полоска и проценты показывают ход.
    /// Ход — медленнее из двух: настоящая загрузка и минимальный срок показа.
    /// Чью загрузку показывать, говорит владелец через TrackLoading; наезд идёт сам с включения.
    /// </summary>
    [FDescription("Экран загрузки: наезд картинки и полоска с процентами. Чью загрузку показывать, говорит владелец.")]
    public class LoadingScreenView : MonoBehaviour
    {
        [Header("Наезд")]
        [Tooltip("Картинка, которая медленно наезжает, пока идёт загрузка")]
        [SerializeField] private RectTransform picture;

        [Tooltip("Насколько картинка вырастает к концу наезда. Выше 1.25 не пускаем: " +
                 "края уезжают за экран, и наезд читается уже как рывок")]
        [Range(1f, 1.25f)]
        [SerializeField] private float zoomScale = 1.08f;

        [Tooltip("За сколько секунд картинка доезжает до конечного размера, сек. Дальше " +
                 "стоит: загрузка бывает длиннее, а бесконечный наезд раздул бы её")]
        [Min(0.01f)]
        [SerializeField] private float zoomDuration = 6f;

        [Header("Полоска")]
        [Tooltip("Заливка полоски. Растягивается якорем по ширине родителя, поэтому " +
                 "скруглённые края спрайта не режутся. Пусто — полоски нет")]
        [SerializeField] private RectTransform progressFill;

        [Tooltip("Подпись с процентами. Пусто — без подписи")]
        [SerializeField] private TMP_Text progressLabel;

        [Tooltip("Как быстро полоска догоняет ход, долей в секунду. Без сглаживания " +
                 "она прыгала бы рывками загрузчика")]
        [Min(0.1f)]
        [SerializeField] private float fillSpeed = 1.5f;

        private IProgressable loading;
        private float minimumDuration;
        private float trackedSince;

        private float shownSince;
        private float shownProgress;
        private int shownPercent = -1;


        // ── Ход загрузки ──────────────────────────────────────────────────

        /// <summary>Показывать ход этой загрузки. До конца срока полоска не дойдёт, даже если сцена уже готова.</summary>
        public void TrackLoading(IProgressable loading, float minimumDuration)
        {
            this.loading = loading;
            this.minimumDuration = minimumDuration;
            trackedSince = Time.unscaledTime;
        }


        private void UpdateProgress()
        {
            var target = CalculateTargetProgress();

            // Только вперёд: полоска, отъехавшая назад, читается как сбой загрузки.
            shownProgress = Mathf.MoveTowards(shownProgress, Mathf.Max(shownProgress, target),
                fillSpeed * Time.unscaledDeltaTime);

            ShowProgress(shownProgress);
        }

        // Быстрая сцена иначе дала бы 100% в первый же кадр, и полоска стояла бы полной до конца срока.
        private float CalculateTargetProgress()
        {
            if (loading == null)
                return 0f;

            var timeShare = minimumDuration > 0f
                ? Mathf.Clamp01((Time.unscaledTime - trackedSince) / minimumDuration)
                : 1f;

            return Mathf.Min(loading.Progress, timeShare);
        }


        private void ResetProgress()
        {
            loading = null;
            shownProgress = 0f;
            shownPercent = -1;

            ShowProgress(0f);
        }


        private void ShowProgress(float progress)
        {
            if (progressFill != null)
            {
                var anchorMax = progressFill.anchorMax;
                anchorMax.x = progress;
                progressFill.anchorMax = anchorMax;
            }

            if (progressLabel == null)
                return;

            // Строку пересобираем только на смене числа: подпись обновляется каждый кадр.
            var percent = Mathf.RoundToInt(progress * 100f);
            if (percent == shownPercent)
                return;

            shownPercent = percent;
            progressLabel.SetText("{0}%", percent);
        }


        // ── Наезд ─────────────────────────────────────────────────────────

        private void UpdateZoom()
        {
            if (picture == null)
                return;

            var progress = Mathf.Clamp01((Time.unscaledTime - shownSince) / zoomDuration);

            // Замедление к концу: ровный наезд на длинной загрузке заметно упирается
            // в предел, а затухающий просто останавливается.
            var scale = Mathf.Lerp(1f, zoomScale, Mathf.SmoothStep(0f, 1f, progress));

            picture.localScale = new Vector3(scale, scale, 1f);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        // По НЕмасштабированному времени: экран загрузки висит и на паузе.
        private void OnEnable()
        {
            shownSince = Time.unscaledTime;

            ResetProgress();
            UpdateZoom();
        }

        private void Update()
        {
            UpdateZoom();
            UpdateProgress();
        }
    }
}
