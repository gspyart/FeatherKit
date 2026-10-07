using System;

namespace FeatherKit.Debugging
{
    /// <summary>Одна команда консоли: имя, подсказка и что делает.</summary>
    public class DebugCommand
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string Usage;

        private readonly Func<string[], string> handler;


        public DebugCommand(string name, string description, Func<string[], string> handler,
            string usage = null)
        {
            Name = name;
            Description = description;
            Usage = usage;
            this.handler = handler;
        }


        public string Invoke(string[] args)
        {
            return handler != null ? handler(args) : null;
        }
    }
}
