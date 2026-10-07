using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.UI
{
    /// <summary>
    /// Окно интерфейса: слой, который включают и выключают, и анимация его показа.
    /// - Open, Close, Toggle с колбеком доигрывания: открыть может кто угодно, не зная, что за окно;
    /// - что показать и как готовиться, решает наследник: CanOpen, OnOpening, OnClosing;
    /// - анимация — любой <see cref="IShowHideAnimation"/>; окна очереди на экране по одному.
    /// </summary>
    public abstract class UiWindowBase : MonoBehaviour
    {
        // Окна очереди, чей слой сейчас на экране: открытые и уезжающие.
        private static readonly List<UiWindowBase> QueueOnScreen = new List<UiWindowBase>();

        // Кто ждёт своей очереди. Одно окно — последнее попросившее: иначе быстрые
        // нажатия открыли бы два окна разом.
        private static UiWindowBase waitingInQueue;

        [Header("Окно")]
        [Tooltip("Что включать и выключать. Пусто — сам объект окна")]
        [SerializeField] private GameObject content;

        [Tooltip("Как окно появляется и уходит — компонент с IShowHideAnimation. " +
                 "Пусто — окно просто включается и выключается")]
        [SerializeField] private Component showAnimation;

        [Tooltip("Окно очереди: таких на экране одно за раз. Открываясь, оно убирает " +
                 "прежнее и выезжает, только когда то уехало. Разделы меню — в очереди, " +
                 "всплывающие поверх них окна — нет")]
        [SerializeField] private bool queued;

        // Что сделать, когда слой погаснет: так ждёт следующее окно очереди.
        private Action afterLayerHidden;


        /// <summary>Окно закрылось. Зовётся в момент закрытия, не дожидаясь конца ухода.</summary>
        public event Action Closed;


        // Статика переживает выход из плей-мода при выключенной перезагрузке домена:
        // иначе следующий запуск ждал бы окно из прошлого.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            QueueOnScreen.Clear();
            waitingInQueue = null;
        }


        /// <summary>Открыто ли окно. Уезжающее уже закрыто, хотя слой ещё включён.</summary>
        public bool IsOpen => Animation != null ? Animation.IsShown : Content.activeSelf;

        /// <summary>Есть ли что показать. Нет — Open ничего не делает: пустое окно читается как поломка.</summary>
        public virtual bool CanOpen => true;

        /// <summary>Слой окна: его включают и выключают. Не задан — сам объект.</summary>
        protected GameObject Content => content != null ? content : gameObject;

        /// <summary>Анимация показа. Нет её — слой включается и выключается сразу.</summary>
        protected IShowHideAnimation Animation => showAnimation as IShowHideAnimation;


        // ── Открыть и закрыть ─────────────────────────────────────────────

        /// <summary>Показать окно. Без колбека — чтобы его можно было повесить прямо на кнопку.</summary>
        public void Open()
        {
            Open(null);
        }

        /// <summary>Показать окно; opened — когда появление доиграло. Уже открытое только перерисуется.</summary>
        public void Open(Action opened)
        {
            if (!CanOpen)
                return;

            var wasOpen = IsOpen;

            if (queued && !wasOpen && WaitForQueue(opened))
                return;

            // Слой включаем ДО подготовки: наследник рисует в него, и его части должны проснуться.
            Content.SetActive(true);

            if (queued && !QueueOnScreen.Contains(this))
                QueueOnScreen.Add(this);

            OnOpening();

            var animation = Animation;

            if (wasOpen || animation == null)
            {
                opened?.Invoke();
                return;
            }

            animation.Show(opened);
        }

        // Другое окно очереди ещё на экране: просим его уйти и встаём следом. Не отпустило
        // (спросило разрешения) — не ждём: откроемся по следующей просьбе.
        private bool WaitForQueue(Action opened)
        {
            var other = OtherInQueue();

            if (other == null)
            {
                if (waitingInQueue == this)
                    waitingInQueue = null;

                return false;
            }

            if (other.IsOpen)
                other.Close();

            if (other.IsOpen)
                return true;

            // Ушло сразу, без анимации: ждать нечего, но на экране может быть ещё одно.
            if (!other.Content.activeSelf)
                return WaitForQueue(opened);

            waitingInQueue = this;

            other.afterLayerHidden += () =>
            {
                if (waitingInQueue != this)
                    return;

                waitingInQueue = null;

                Open(opened);
            };

            return true;
        }

        // Заодно чистим список: уничтоженные окна о себе не сообщают, их убирает смена сцены.
        private UiWindowBase OtherInQueue()
        {
            for (var i = QueueOnScreen.Count - 1; i >= 0; i--)
            {
                var window = QueueOnScreen[i];

                if (window == null || !window.Content.activeSelf)
                {
                    QueueOnScreen.RemoveAt(i);
                    continue;
                }

                if (window != this)
                    return window;
            }

            return null;
        }


        /// <summary>Убрать окно. Без колбека — чтобы его можно было повесить прямо на кнопку.</summary>
        public void Close()
        {
            Close(null);
        }

        /// <summary>
        /// Убрать окно; closed — когда его на экране уже нет. Закрытое ничего не делает.
        /// Переопределяют, чтобы спросить разрешения; уборка при этом — в OnClosing.
        /// </summary>
        public virtual void Close(Action closed)
        {
            if (!IsOpen)
            {
                // Закрыли, пока окно ждало очереди: ждать больше незачем.
                if (waitingInQueue == this)
                    waitingInQueue = null;

                closed?.Invoke();
                return;
            }

            OnClosing();

            var animation = Animation;

            // С анимацией гасим по приезду: выключить слой на середине ухода значит оборвать её,
            // а прерванный уход колбека не отдаёт — открытое заново окно не погасло бы.
            if (animation != null)
                animation.Hide(() => HideLayer(closed));
            else
                Content.SetActive(false);

            Closed?.Invoke();

            if (animation == null)
                HideLayer(closed);
        }

        // Слой погас — окно очереди освободило экран, и ждущее может выезжать.
        private void HideLayer(Action closed)
        {
            Content.SetActive(false);

            QueueOnScreen.Remove(this);

            closed?.Invoke();

            var next = afterLayerHidden;

            afterLayerHidden = null;
            next?.Invoke();
        }


        public void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }


        // ── Для наследника ────────────────────────────────────────────────

        /// <summary>Подготовить окно к показу: перерисовать, подписаться. Зовут и на повторное Open.</summary>
        protected virtual void OnOpening()
        {
        }

        /// <summary>Прибрать перед уходом: забыть вопрос, отписаться. Зовут только у открытого окна.</summary>
        protected virtual void OnClosing()
        {
        }

        /// <summary>Убрать сразу — без анимации, хуков и события: так прячут окно, которого игрок не видел.</summary>
        protected void HideImmediately()
        {
            Animation?.HideInstant();

            Content.SetActive(false);

            QueueOnScreen.Remove(this);
        }


        // ── Unity ─────────────────────────────────────────────────────────

        // Интерфейс в инспекторе напрямую не хранится, поэтому поле — любой компонент.
        // Положили не тот — ищем подходящий на том же объекте, иначе окно молча осталось бы без анимации.
        protected virtual void OnValidate()
        {
            if (showAnimation == null || showAnimation is IShowHideAnimation)
                return;

            var suitable = showAnimation.GetComponent<IShowHideAnimation>() as Component;

            if (suitable == null)
                Debug.LogWarning($"{GetType().Name} «{name}»: у «{showAnimation.name}» нет анимации показа " +
                                 "(IShowHideAnimation) — ссылка сброшена.", this);

            showAnimation = suitable;
        }
    }
}
