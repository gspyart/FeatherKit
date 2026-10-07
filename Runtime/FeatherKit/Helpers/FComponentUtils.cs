using UnityEngine;

/// <summary>
/// Дополнение к тому, что и так есть у любого компонента: GetComponent, AddComponent —
/// и рядом GetOrAddComponent.
///
/// Пространства имён у класса нет намеренно, и это единственное такое место в библиотеке:
/// метод должен подсказываться сразу после точки, как родные, а не после того, как
/// вспомнишь про using. Остальные расширения лежат в FeatherKit.Helpers — глобальные
/// имена общие на весь проект, и занимать их стоит только тем, чем пользуются везде.
/// </summary>
public static class FComponentUtils
{
    /// <summary>Компонент этого объекта, а если его нет — добавить. Объекта нет — null.</summary>
    public static T GetOrAddComponent<T>(this Component component) where T : Component
    {
        return component == null ? null : component.gameObject.GetOrAddComponent<T>();
    }

    public static T GetOrAddComponent<T>(this GameObject target) where T : Component
    {
        if (target == null)
            return null;

        return target.TryGetComponent<T>(out var existing) ? existing : target.AddComponent<T>();
    }
}
