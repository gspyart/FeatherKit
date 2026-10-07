using FeatherKit.Attributes;
using UnityEditor;
using UnityEngine;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Рисует описание компонента из <see cref="FDescriptionAttribute"/> над его полями.
    ///
    /// Отдельно от <see cref="FDescriptionHeader"/>, и это не дублирование: событие шапки
    /// зовётся только для верхнего объекта — выделенного GameObject или ассета, — а до
    /// компонентов внутри инспектора не доходит. Ассетам хватает события, компонентам
    /// нужен свой инспектор.
    ///
    /// ЦЕНА: инспектор заявлен на все монобехи, поэтому перебивает чужие «перехватчики»,
    /// если такие в проекте есть, — пакеты с атрибутами инспектора обычно ставят свой
    /// Editor на UnityEngine.Object целиком, а наш, будучи заявленным на более узкий тип,
    /// побеждает. Их атрибуты НА МОНОБЕХАХ рисоваться не будут; на ScriptableObject мы
    /// не претендуем, поэтому конфиги с такими атрибутами не страдают.
    ///
    /// Конкретные инспекторы (Unity для Image, свои для джойстика) заявлены на свои типы
    /// и остаются за ними — этот заменяет только отрисовку по умолчанию.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public class FDescriptionInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDescription();

            DrawDefaultInspector();
        }

        private void DrawDescription()
        {
            var text = FDescriptionHeader.DescriptionOf(target);

            if (string.IsNullOrEmpty(text))
                return;

            // Без иконки: это пояснение, а не предупреждение, и восклицательный знак
            // у каждого компонента читался бы как ошибка.
            EditorGUILayout.HelpBox(text, MessageType.None);
        }
    }
}
