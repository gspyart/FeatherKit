namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Нода с памятью, которая переживает Stop: открыты ли ворота, пройдена ли Once, сколько раз прошли Do N.
    /// Забывается при уничтожении объекта, вызовом Scenario.ResetNodeMemory и своими входами Reset, Open, Close.
    /// </summary>
    public interface IScenarioNodeMemory
    {
        void ForgetMemory();
    }
}
