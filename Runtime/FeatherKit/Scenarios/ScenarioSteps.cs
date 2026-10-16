using System.Collections;
using System.Collections.Generic;

namespace FeatherKit.Scenarios
{
    /// <summary>
    /// Разворачивает вложенные шаги сценария в один плоский поток для Unity.
    /// Сами, а не руками Unity: ошибка на любой глубине всплывает к сценарию, и он обрывается
    /// с понятным сообщением, а не повисает навсегда «идущим».
    /// </summary>
    public static class ScenarioSteps
    {
        public static IEnumerator Flatten(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Peek();

                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                yield return current.Current;
            }
        }
    }
}
