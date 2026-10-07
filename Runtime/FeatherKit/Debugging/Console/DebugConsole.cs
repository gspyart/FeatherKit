using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using FeatherKit.Logging;
using UnityEngine;

namespace FeatherKit.Debugging
{
    /// <summary>
    /// Отладочные команды в виде текста: «gold 500», «kill», «wave 3».
    ///
    /// Почему через текст, а не набор методов: команда регистрируется той системой, которая
    /// умеет её выполнить, а вызвать её может кто угодно — кнопка на панели, поле ввода,
    /// хоткей. Кнопка при этом ничего не знает про кошелёк, она знает только строку.
    ///
    /// Статический, потому что регистрируются отовсюду и в любой момент. Сбрасывается
    /// при входе в игру — иначе команды прошлого запуска остались бы висеть со ссылками
    /// на мёртвые объекты.
    ///
    /// В релизной сборке ничего не регистрируется: Register вырезается компилятором,
    /// если игра не поставила символ FEATHERKIT_DEBUG.
    /// </summary>
    public static class DebugConsole
    {
        /// <summary>Символ компиляции: с ним консоль и панели отладки живут и в релизной сборке.</summary>
        public const string AnyBuildSymbol = "FEATHERKIT_DEBUG";

        private const string EditorOnly = "UNITY_EDITOR";
        private const string DevelopmentOnly = "DEVELOPMENT_BUILD";

        private static readonly Dictionary<string, DebugCommand> commands =
            new Dictionary<string, DebugCommand>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> history = new List<string>();

        //История нужна только для стрелки «вверх», хранить всю сессию незачем.
        private const int HistoryLimit = 64;


        /// <summary>Кто-то выполнил команду: панели полезно показать результат.</summary>
        public static event Action<string> Executed;


        public static IReadOnlyCollection<DebugCommand> Commands => commands.Values;

        /// <summary>Что уже вводили — для стрелки «вверх» в поле ввода.</summary>
        public static IReadOnlyList<string> History => history;


        /// <summary>
        /// Зарегистрировать команду. Аргументы приходят строками: «gold 500» — это args[0] = "500".
        /// Вернуть можно строку-ответ, она уйдёт в лог и на панель.
        /// </summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuildSymbol)]
        public static void Register(string name, string description, Func<string[], string> handler,
            string usage = null)
        {
            if (string.IsNullOrWhiteSpace(name) || handler == null)
                return;

            commands[name.Trim()] = new DebugCommand(name.Trim(), description, handler, usage);
        }

        /// <summary>Команда без ответа — когда возвращать нечего.</summary>
        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuildSymbol)]
        public static void Register(string name, string description, Action<string[]> handler,
            string usage = null)
        {
            Register(name, description, args =>
            {
                handler(args);
                return null;
            }, usage);
        }

        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuildSymbol)]
        public static void Unregister(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                commands.Remove(name.Trim());
        }

        /// <summary>Выполнить строку целиком: «gold 500». Возвращает ответ команды или ошибку.</summary>
        public static string Execute(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            line = line.Trim();

            history.Add(line);

            if (history.Count > HistoryLimit)
                history.RemoveAt(0);

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var name = parts[0];

            if (!commands.TryGetValue(name, out var command))
            {
                var unknown = $"Нет команды «{name}». Список — help.";

                FLog.Warning((unknown, FLogColor.Warn));
                Executed?.Invoke(unknown);

                return unknown;
            }

            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            string result;

            // Отладочная команда не должна ронять игру: её пишут наспех, и половина
            // падений тут — просто неверный аргумент.
            try
            {
                result = command.Invoke(args);
            }
            catch (Exception exception)
            {
                result = $"Команда «{name}» упала: {exception.Message}";

                FLog.Error((result, FLogColor.Bad));
            }

            if (!string.IsNullOrEmpty(result))
                FLog.Info((result, FLogColor.Accent));

            Executed?.Invoke(result);

            return result;
        }

        /// <summary>Разбор числа из аргумента — самое частое, что делает команда.</summary>
        public static int Int(string[] args, int index, int fallback = 0)
        {
            return args != null && index < args.Length && int.TryParse(args[index], out var value)
                ? value
                : fallback;
        }

        public static float Float(string[] args, int index, float fallback = 0f)
        {
            return args != null && index < args.Length
                   && float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            commands.Clear();
            history.Clear();
            Executed = null;

            RegisterHelp();
        }

        [Conditional(EditorOnly), Conditional(DevelopmentOnly), Conditional(AnyBuildSymbol)]
        private static void RegisterHelp()
        {
            Register("help", "Список команд", args =>
            {
                var text = "Команды:";

                foreach (var command in commands.Values)
                    text += $"\n  {command.Usage ?? command.Name} — {command.Description}";

                return text;
            });
        }
    }
}
