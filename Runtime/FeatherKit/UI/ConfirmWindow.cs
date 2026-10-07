using System;
using FeatherKit.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FeatherKit.UI
{
    /// <summary>
    /// Окно «вопрос — да или отмена». Одно на холст: разные окна одного вопроса разъехались бы по виду.
    /// - что делать при согласии, ему передают: само окно ничего не знает о спрашивающем;
    /// - согласие срабатывает ОДИН раз: второй тап за те же кадры выполнил бы действие дважды.
    /// </summary>
    [FDescription("Окно «вопрос — да или отмена». Одно на холст: что делать при согласии, ему передают.")]
    public class ConfirmWindow : UiWindowBase
    {
        [Header("Вопрос")]
        [Tooltip("Текст вопроса")]
        [SerializeField] private TMP_Text question;

        [Header("Управление")]
        [Tooltip("Кнопка согласия")]
        [SerializeField] private Button confirmButton;

        [Tooltip("Кнопка отказа. Она же закрывает окно")]
        [SerializeField] private Button cancelButton;

        private Action confirmed;


        // ── Вопрос ────────────────────────────────────────────────────────

        /// <summary>Спросить. Прошлый незакрытый вопрос заменяется этим: висеть двум сразу негде.</summary>
        public void Ask(string text, Action onConfirmed)
        {
            confirmed = onConfirmed;

            if (question != null)
                question.text = text;

            Open();
        }

        protected override void OnClosing()
        {
            confirmed = null;
        }

        private void OnConfirmed()
        {
            var action = confirmed;

            // Гасим окно и забываем действие ДО вызова: оно может выгрузить сцену вместе с окном.
            Close();

            action?.Invoke();
        }


        // ── Unity ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmed);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(Close);

            // Закрыто на старте: вопрос без спрашивающего игрок читает как поломку.
            HideImmediately();
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnConfirmed);

            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(Close);
        }
    }
}
