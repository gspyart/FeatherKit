using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FeatherKit.Rendering
{
    /// <summary>
    /// Пока объект включён, главная камера рисует выбранным рендерером URP, а на выключении
    /// возвращается рендерер по умолчанию.
    ///
    /// Нужен там, где камера одна на всю игру и живёт вне сцен: выставить рендерер на ней
    /// в инспекторе нельзя — он остался бы и в следующей сцене, которая о нём не просила.
    /// А сцена, которой нужен свой рендерер, приносит этот выбор с собой и уносит вместе
    /// с собой же.
    ///
    /// Отвечает ТОЛЬКО за подмену. Что в том рендерере настроено — тени, обводка,
    /// эффекты — дело самого ассета.
    /// </summary>
    [FDescription("Пока сцена открыта, главная камера рисует её выбранным рендерером URP.")]
    public class CameraRendererOverride : MonoBehaviour
    {
        // Рендерер по умолчанию из ассета URP. Прежний номер камеры прочитать нельзя —
        // публичного чтения у URP нет, — но исходное состояние камеры именно такое.
        private const int DefaultRenderer = -1;

        [Tooltip("Номер рендерера в списке Renderer List у ассета URP. Именно номер, а не имя: " +
                 "и URP, и камера знают рендереры только по месту в этом списке")]
        [SerializeField] private int rendererIndex;


        // ── Подмена рендерера ─────────────────────────────────────────────

        // Камеру ищем на каждый вызов, а не помним с включения: она живёт вне сцены,
        // и ссылка на чужой объект пережила бы выгрузку пустой оболочкой.
        private void SetRenderer(int index)
        {
            var mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            mainCamera.GetUniversalAdditionalCameraData().SetRenderer(index);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            SetRenderer(rendererIndex);
        }

        private void OnDisable()
        {
            SetRenderer(DefaultRenderer);
        }
    }
}
