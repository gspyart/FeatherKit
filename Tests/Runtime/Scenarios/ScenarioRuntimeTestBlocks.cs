using System;
using System.Collections;
using FeatherKit.Scenarios;
using UnityEngine;

// Конструкторы только с параметрами: без пустого конструктора меню «+» и поиск нод тестовые блоки не покажут.
namespace FeatherKit.Tests.Scenarios
{
    /// <summary>Тестовый долгий эффект: считает, сколько раз граф его отпустил.</summary>
    public class ScenarioTestHeldEffect : IScenarioHeldEffect
    {
        public int ReleaseCount { get; private set; }

        public bool IsReleased => ReleaseCount > 0;


        // IScenarioHeldEffect
        public void Release()
        {
            ReleaseCount++;
        }
    }


    /// <summary>Тестовый шаг: берёт свой долгий эффект под присмотр графа.</summary>
    [Serializable]
    public class ScenarioTestHoldEffectAction : ScenarioInstantAction
    {
        [NonSerialized] private ScenarioTestHeldEffect effect;


        public ScenarioTestHoldEffectAction(ScenarioTestHeldEffect effect)
        {
            this.effect = effect;
        }


        protected override void Apply(ScenarioRun run)
        {
            run.Hold(effect);
        }
    }


    /// <summary>Тестовый шаг: ждёт столько кадров; без «Wait Until Done» уходит в фон.</summary>
    [Serializable]
    public class ScenarioTestWaitFramesAction : ScenarioAction
    {
        [SerializeField] private int frames;
        [SerializeField] private bool waitsForEnd;


        public ScenarioTestWaitFramesAction(int frames, bool waitsForEnd = true)
        {
            this.frames = frames;
            this.waitsForEnd = waitsForEnd;
        }


        public override bool WaitsForEnd => waitsForEnd;


        public override IEnumerator Perform(ScenarioRun run)
        {
            for (var frame = 0; frame < frames; frame++)
                yield return null;
        }
    }


    /// <summary>Тестовый шаг: ждёт кадры, затем пишет метку — фоновой веткой видно, дожила ли она до конца.</summary>
    [Serializable]
    public class ScenarioTestBackgroundRecordAction : ScenarioAction
    {
        [SerializeField] private int frames;
        [SerializeField] private string mark;


        public ScenarioTestBackgroundRecordAction(int frames, string mark)
        {
            this.frames = frames;
            this.mark = mark;
        }


        public override bool WaitsForEnd => false;


        public override IEnumerator Perform(ScenarioRun run)
        {
            for (var frame = 0; frame < frames; frame++)
                yield return null;

            ScenarioTestRecordAction.RecordedMarks.Add(mark);
        }
    }


    /// <summary>Тестовое условие: всегда верно и считает, сколько раз его проверили.</summary>
    [Serializable]
    public class ScenarioTestCountingCondition : ScenarioCondition
    {
        [SerializeField] private string unusedName;


        public ScenarioTestCountingCondition(string unusedName)
        {
            this.unusedName = unusedName;
        }


        public int CheckCount { get; private set; }


        protected override bool Check(ScenarioRun run)
        {
            CheckCount++;
            return true;
        }
    }


    /// <summary>Тестовый шаг, который всегда падает.</summary>
    [Serializable]
    public class ScenarioTestFailAction : ScenarioInstantAction
    {
        public const string Message = "Test step failed on purpose";

        [SerializeField] private string unusedName;


        public ScenarioTestFailAction(string unusedName)
        {
            this.unusedName = unusedName;
        }


        protected override void Apply(ScenarioRun run)
        {
            throw new InvalidOperationException(Message);
        }
    }


    /// <summary>Тестовый шаг: пишет «метка=значение» флага — общего или переменной графа.</summary>
    [Serializable]
    public class ScenarioTestRecordFlagAction : ScenarioInstantAction
    {
        [SerializeField] private string mark;
        [SerializeField] private ScenarioFlagReference flag;


        public ScenarioTestRecordFlagAction(string mark, ScenarioFlagReference flag)
        {
            this.mark = mark;
            this.flag = flag;
        }


        protected override void Apply(ScenarioRun run)
        {
            ScenarioTestRecordAction.RecordedMarks.Add($"{mark}={flag.Get(run)}");
        }
    }


    /// <summary>Тестовый шаг: ставит флаг — общий или переменную графа.</summary>
    [Serializable]
    public class ScenarioTestSetFlagAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioFlagReference flag;
        [SerializeField] private bool value;


        public ScenarioTestSetFlagAction(ScenarioFlagReference flag, bool value)
        {
            this.flag = flag;
            this.value = value;
        }


        protected override void Apply(ScenarioRun run)
        {
            flag.Set(run, value);
        }
    }


    /// <summary>Тестовый шаг: прибавляет к счётчику — общему или переменной графа — и пишет «метка=значение».</summary>
    [Serializable]
    public class ScenarioTestCountAction : ScenarioInstantAction
    {
        [SerializeField] private string mark;
        [SerializeField] private ScenarioCounterReference counter;


        public ScenarioTestCountAction(string mark, ScenarioCounterReference counter)
        {
            this.mark = mark;
            this.counter = counter;
        }


        protected override void Apply(ScenarioRun run)
        {
            counter.Set(run, counter.Get(run) + 1);
            ScenarioTestRecordAction.RecordedMarks.Add($"{mark}={counter.Get(run)}");
        }
    }


    /// <summary>Тестовый шаг: пишет имя объекта из поля «значение или переменная» — так видно привязку.</summary>
    [Serializable]
    public class ScenarioTestRecordLightAction : ScenarioInstantAction
    {
        [SerializeField] private ScenarioValue<Light> lamp;


        public ScenarioTestRecordLightAction(ScenarioValue<Light> lamp)
        {
            this.lamp = lamp;
        }


        protected override void Apply(ScenarioRun run)
        {
            var light = lamp.Get(run);
            ScenarioTestRecordAction.RecordedMarks.Add(light != null ? light.name : "no light");
        }
    }
}
