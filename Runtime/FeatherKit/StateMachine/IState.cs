namespace FeatherKit.StateMachine
{
    /// <summary>Состояние для StateMachine — минимальный контракт жизненного цикла.</summary>
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick(float deltaTime);
    }
}
