using System.Collections.Generic;
using UnityEngine;

namespace FeatherKit.StateMachine
{
    /// <summary>
    /// Простая машина состояний без завязки на конкретную игру: переключает текущее состояние
    /// и тикает его. Не фреймворк "на всё" — здесь ровно это и ничего больше.
    ///
    /// Сами состояния создаёт и хранит владелец машины, а переход делается передачей объекта,
    /// а не типа: так один класс состояния может жить в нескольких экземплярах с разными
    /// настройками, и машине не нужно уметь их различать.
    ///
    /// Register при этом обязателен: он не про "какие типы бывают", а про то, что переключаться
    /// можно только на заранее созданные экземпляры. Иначе случайный ChangeState(new SomeState())
    /// молча увёл бы машину в объект-однодневку — Enter у него вызвался, а Exit потом сработал
    /// бы уже не у того состояния, которое считал текущим владелец.
    /// </summary>
    public class StateMachine
    {
        private readonly HashSet<IState> registered = new HashSet<IState>();

        private IState current;


        public IState Current => current;


        // Null и повторная регистрация одного объекта игнорируются.
        public void Register(IState state)
        {
            if (state == null)
                return;

            registered.Add(state);
        }


        // Null и повторный переход в то же самое состояние игнорируются молча — это не ошибки.
        public void ChangeState(IState next)
        {
            if (next == null || next == current)
                return;

            // А вот незарегистрированное состояние — почти наверняка опечатка на стороне
            // вызывающего, поэтому громко в консоль, а не тихий выход.
            if (!registered.Contains(next))
            {
                Debug.LogError($"StateMachine: состояние {next.GetType().Name} не зарегистрировано — переход отменён.");
                return;
            }

            current?.Exit();
            current = next;
            current.Enter();
        }


        public void Tick(float deltaTime)
        {
            current?.Tick(deltaTime);
        }
    }
}
