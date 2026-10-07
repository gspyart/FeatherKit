using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace FeatherKit.CameraEffects
{
    /// <summary>
    /// Зум для CinemachineCamera: постоянный (SetZoom) и punch-зум для взрывов (PunchZoom, in → out).
    /// Работает и с ортографической, и с перспективной камерой — режим читается из самого
    /// Lens.Orthographic, который Cinemachine синхронизирует с реальной камерой сам.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraZoom : MonoBehaviour
    {
        private CinemachineCamera vcam;
        private float baseValue;
        private Coroutine punchRoutine;


        protected virtual void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
            baseValue = GetLensValue();
        }


        // Значение остаётся насовсем, пока не позовёшь SetZoom/PunchZoom снова.
        public void SetZoom(float value)
        {
            if (vcam == null)
                return;

            if (punchRoutine != null)
            {
                StopCoroutine(punchRoutine);
                punchRoutine = null;
            }

            baseValue = value;
            SetLensValue(value);
        }

        // Быстро зумит к value и плавно возвращается к текущему базовому значению — для взрывов и т.п.
        public void PunchZoom(float value, float inDuration, float outDuration)
        {
            if (vcam == null || inDuration <= 0f || outDuration <= 0f)
                return;

            if (punchRoutine != null)
                StopCoroutine(punchRoutine);

            punchRoutine = StartCoroutine(PunchRoutine(value, inDuration, outDuration));
        }

        /// <summary>
        /// То же самое, но ОТ базового значения: -3 — наехать на три, +3 — отъехать.
        /// Меньше значит ближе и у перспективной камеры, и у ортографической.
        ///
        /// Относительно, а не абсолютом, потому что базовый ракурс подбирают под сцену
        /// глазами. Абсолютное число живёт у вызывающего — у модуля удара, у способности —
        /// и на первой же правке ракурса разъезжается с ним: наезд превращается в отъезд,
        /// причём молча.
        ///
        /// Отсчёт от базового, а не от текущего: посреди уже идущего наезда текущее
        /// значение — это середина анимации, и второй удар считал бы дельту от неё.
        /// </summary>
        public void PunchZoomBy(float delta, float inDuration, float outDuration)
        {
            PunchZoom(baseValue + delta, inDuration, outDuration);
        }


        private IEnumerator PunchRoutine(float value, float inDuration, float outDuration)
        {
            yield return Animate(GetLensValue(), value, inDuration);
            yield return Animate(value, baseValue, outDuration);
            punchRoutine = null;
        }

        private IEnumerator Animate(float from, float to, float duration)
        {
            var elapsed = 0f;

            while (elapsed < duration)
            {
                SetLensValue(Mathf.Lerp(from, to, elapsed / duration));
                // Реальное время: зум — фидбек, он доигрывает и на паузе. По игровому
                // времени punch-зум застрял бы наполовину наехавшим.
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetLensValue(to);
        }

        private float GetLensValue()
        {
            var lens = vcam.Lens;
            return lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView;
        }

        private void SetLensValue(float value)
        {
            var lens = vcam.Lens;

            if (lens.Orthographic)
                lens.OrthographicSize = value;
            else
                lens.FieldOfView = value;

            vcam.Lens = lens;
        }
    }
}
