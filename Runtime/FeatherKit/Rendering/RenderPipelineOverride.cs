using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Пока объект включён, игра рисуется ассетом URP этой сцены, а на выключении
    /// возвращается прежний.
    ///
    /// Нужен там, где сцене мало общих настроек качества: вид сверху требует своей
    /// дальности теней, тесный интерьер — своих каскадов. Ассет один на всю игру, и просто
    /// поставить в нём «как надо в хабе» нельзя — это утащило бы за собой и рейд.
    ///
    /// Отвечает ТОЛЬКО за подмену. Что в том ассете настроено — тени, каскады, качество —
    /// дело самого ассета. Рендерер подменяет сосед <see cref="CameraRendererOverride"/>:
    /// это разные вещи, и путать их не стоит — у рендерера проходы и эффекты, у ассета
    /// качество картинки целиком.
    ///
    /// Цена подмены — пересборка пайплайна, кадр-другой. Поэтому меняем на входе в сцену,
    /// а не по ходу игры.
    /// </summary>
    [FDescription("Пока сцена открыта, игра рисуется её ассетом URP: своя дальность теней и качество.")]
    public class RenderPipelineOverride : MonoBehaviour
    {
        [Tooltip("Ассет URP этой сцены. Пусто — подмены нет, играем на общем")]
        [SerializeField] private RenderPipelineAsset pipeline;

        // Что стояло до нас. Возвращаем именно его, а не пустоту: уровень качества мог
        // держать свой ассет, и стереть его значило бы увести игру на другую картинку.
        private RenderPipelineAsset previous;

        private bool overridden;


        // ── Подмена ассета ────────────────────────────────────────────────

        private void Apply()
        {
            if (overridden || pipeline == null || QualitySettings.renderPipeline == pipeline)
                return;

            previous = QualitySettings.renderPipeline;
            overridden = true;

            QualitySettings.renderPipeline = pipeline;
        }

        private void Restore()
        {
            if (!overridden)
                return;

            QualitySettings.renderPipeline = previous;

            previous = null;
            overridden = false;
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Restore();
        }
    }
}
