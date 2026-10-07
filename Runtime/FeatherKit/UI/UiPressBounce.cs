using FeatherKit.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Отклик кнопки на нажатие: вдавливается и возвращается — тем же прыжком, что вкладки и меню.
    /// - вешается на префаб кнопки, и отклик получают все кнопки из него;
    /// - отзывается только на нажатие, которое кнопка приняла: выключенная молчит.
    /// </summary>
    [FDescription("Отклик кнопки на нажатие: вдавливается и возвращается, как вкладки и меню.")]
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class UiPressBounce : MonoBehaviour
    {
        [Tooltip("Как кнопка отвечает на нажатие: вдавливается и возвращается")]
        [SerializeField] private UiBounce pressBounce = UiBounce.Press();

        private Button button;


        // ── Нажатие ───────────────────────────────────────────────────────

        private void OnClicked()
        {
            pressBounce.Play();
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            button = GetComponent<Button>();
            pressBounce.Attach(transform);
        }

        private void OnEnable()
        {
            button.onClick.AddListener(OnClicked);
        }

        // Время неигровое: окна живут и при замедлении мира, и отклик не должен тянуться.
        private void Update()
        {
            pressBounce.Tick(Time.unscaledDeltaTime);
        }

        // Выключили посреди вдавливания — вернуть размер, иначе кнопка так и осталась бы сжатой.
        private void OnDisable()
        {
            button.onClick.RemoveListener(OnClicked);
            pressBounce.Rest();
        }
    }
}
