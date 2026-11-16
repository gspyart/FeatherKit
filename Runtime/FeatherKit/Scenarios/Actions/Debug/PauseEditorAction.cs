using System;
using UnityEngine;

namespace FeatherKit.Scenarios
{
    /// <summary>Поставить игру на паузу в редакторе — посмотреть сцену в момент шага. В сборке ничего не делает.</summary>
    [Serializable]
    [ScenarioBlock("Debug/Pause Editor", Description = "Pause the game in the editor at this step")]
    public class PauseEditorAction : ScenarioInstantAction
    {
        protected override void Apply(ScenarioRun run)
        {
            Debug.Break();
        }
    }
}
