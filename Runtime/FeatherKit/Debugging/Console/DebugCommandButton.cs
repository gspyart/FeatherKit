using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Кнопка, которая выполняет строку консоли. Быстрый доступ к тому же, что можно
    /// набрать руками: кнопка не знает ни про кошелёк, ни про волны — только про строку.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DebugCommandButton : MonoBehaviour
    {
        [Tooltip("Что выполнить при нажатии — целиком, как в консоли: «gold 500»")]
        [SerializeField] private string command;

        private Button button;


        private void Press()
        {
            DebugConsole.Execute(command);
        }

        private void Awake()
        {
            button = this.GetOrAddComponent<Button>();
        }

        private void OnEnable()
        {
            button.onClick.AddListener(Press);
        }

        private void OnDisable()
        {
            button.onClick.RemoveListener(Press);
        }
    }
}
