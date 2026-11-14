using System.Collections.Generic;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Панель переменных поверх холста: «Graph» — внутренние, «Set on Object» — открытые (у граф-файла).
    /// - «+» добавляет, двойной клик по имени переименовывает, Delete или меню удаляет
    /// - у переменной: вид, тип объекта, значение по умолчанию, Set on Object, Saved
    /// - граф-файл, открытый через объект, — колонка «On …» с живой привязкой этого объекта и предупреждения
    /// - раскрытые строки помнит по id переменной: пересборка и перезагрузка домена их не сворачивают
    /// </summary>
    internal class ScenarioGraphBlackboard : Blackboard
    {
        private const string ExpandedRowsSessionKey = "FeatherKit.ScenarioGraph.ExpandedVariables";
        private const char ExpandedRowsSeparator = ';';
        private const string KindPropertyName = "kind";
        private const string InitialValuePropertyName = "initialValue";
        private const string ExposedHintForEmbedded = "Only for graph files — here, put scene objects straight into the variable";
        private const string ExposedHintForFile = "Each Scenario using this file sets its own value in Scenario → Set on Object";
        private static readonly Rect DefaultArea = new Rect(10f, 30f, 300f, 340f);

        private readonly ScenarioGraphView scenarioGraphView;
        private readonly BlackboardSection graphSection;
        private readonly BlackboardSection exposedSection;
        private readonly HashSet<string> expandedVariableIds;


        public ScenarioGraphBlackboard(ScenarioGraphView graphView) : base(graphView)
        {
            scenarioGraphView = graphView;
            title = "Variables";
            scrollable = true;
            SetPosition(DefaultArea);

            // Длинное имя переменной не должно растягивать панель вбок: тогда поля уезжают за край.
            var scrollView = this.Q<ScrollView>();

            if (scrollView != null)
                scrollView.mode = ScrollViewMode.Vertical;

            expandedVariableIds = new HashSet<string>(SessionState.GetString(ExpandedRowsSessionKey, "")
                .Split(new[] { ExpandedRowsSeparator }, System.StringSplitOptions.RemoveEmptyEntries));

            graphSection = new BlackboardSection { title = "Graph" };
            exposedSection = new BlackboardSection { title = "Set on Object" };
            Add(graphSection);
            Add(exposedSection);

            addItemRequested = _ => ShowAddMenu();
            editTextRequested = (_, element, newName) => Rename(element, newName);
        }


        private ScenarioGraphHost Host => scenarioGraphView.Host;


        // ── Список ───────────────────────────────────────────────────────

        public void Rebuild()
        {
            RememberExpandedRows();
            graphSection.Clear();
            exposedSection.Clear();

            if (Host is not { IsAlive: true } || Host.Graph == null)
                return;

            subTitle = Host.IsGraphFile ? "Graph File" : "In This Object";
            Host.Bindings?.SerializedScenario.Update();

            foreach (var variable in Host.Graph.Variables)
            {
                var variableProperty = variable != null ? Host.VariableProperty(variable.Id) : null;

                if (variableProperty == null)
                    continue;

                var section = Host.IsGraphFile && variable.IsExposed ? exposedSection : graphSection;
                section.Add(CreateRow(variable, variableProperty));
            }

            exposedSection.style.display = Host.IsGraphFile ? DisplayStyle.Flex : DisplayStyle.None;

            if (Host.IsGraphFile && Host.Bindings == null)
                exposedSection.Insert(0, HintLabel(ExposedHintForFile));
        }


        private VisualElement CreateRow(ScenarioVariableDefinition variable, SerializedProperty variableProperty)
        {
            var warning = WarningOf(variable);
            var field = new BlackboardField(ScenarioIcons.IconOf(variable), variable.Name, warning != null ? $"⚠ {KindTextOf(variable)}" : KindTextOf(variable))
            {
                userData = variable.Id,
                tooltip = warning != null ? $"{variable.Name}\n{warning}" : $"{variable.Name} — {variable.Kind} variable"
            };

            field.AddToClassList("scenario-variable-field");

            field.AddManipulator(new ContextualMenuManipulator(menuEvent =>
            {
                menuEvent.menu.AppendAction("Rename", _ => field.OpenTextEditor());
                menuEvent.menu.AppendAction("Delete", _ => scenarioGraphView.ApplyEdit(editing => editing.RemoveVariable(variable.Id)));
            }));

            return new BlackboardRow(field, CreateSettings(variable, variableProperty, warning))
            {
                expanded = expandedVariableIds.Contains(variable.Id)
            };
        }

        private static string KindTextOf(ScenarioVariableDefinition variable)
        {
            if (variable.Kind != ScenarioVariableKind.Object)
                return variable.Kind.ToString();

            return variable.ObjectType == typeof(Object) ? "Object" : variable.ObjectType.Name;
        }

        // Тот же список, что у строки состояния и инспектора Scenario: одно правило — один текст.
        private string WarningOf(ScenarioVariableDefinition variable)
        {
            var messages = scenarioGraphView.Problems.Where(problem => problem.VariableId == variable.Id).Select(problem => problem.Message).ToList();

            return messages.Count > 0 ? string.Join("\n", messages) : null;
        }


        // Строку раскрывают кнопкой BlackboardRow, события об этом нет — читаем раскрытость перед каждой пересборкой.
        private void RememberExpandedRows()
        {
            foreach (var row in this.Query<BlackboardRow>().ToList())
            {
                if (row.Q<BlackboardField>()?.userData is not string variableId)
                    continue;

                if (row.expanded)
                    expandedVariableIds.Add(variableId);
                else
                    expandedVariableIds.Remove(variableId);
            }

            SessionState.SetString(ExpandedRowsSessionKey, string.Join(ExpandedRowsSeparator.ToString(), expandedVariableIds));
        }


        /// <summary>Раскрыть строку переменной и прокрутить к ней — так строка состояния ведёт к проблеме переменной.</summary>
        public void ShowVariable(string variableId)
        {
            var row = this.Query<BlackboardRow>().ToList()
                .FirstOrDefault(candidate => candidate.Q<BlackboardField>()?.userData as string == variableId);

            if (row == null)
                return;

            row.expanded = true;
            this.Q<ScrollView>()?.ScrollTo(row);
        }


        // ── Настройки переменной ─────────────────────────────────────────

        private VisualElement CreateSettings(ScenarioVariableDefinition variable, SerializedProperty variableProperty, string warning)
        {
            var settings = new VisualElement();
            settings.AddToClassList("scenario-variable");

            if (warning != null)
                settings.Add(CreateWarning(variable, warning));

            var kindProperty = variableProperty.FindPropertyRelative(KindPropertyName);
            settings.Add(BoundField(kindProperty, "Kind"));

            // Вид поменялся — поле значения другое: пересобрать панель, когда правка уже записана.
            settings.TrackPropertyValue(kindProperty, _ => schedule.Execute(Rebuild));

            if (variable.Kind == ScenarioVariableKind.Object)
                settings.Add(CreateObjectTypeButton(variable));

            var defaultLabel = Host.IsGraphFile && variable.IsExposed ? "Default" : "Initial Value";
            settings.Add(CreateValueField(variable, variableProperty.FindPropertyRelative(InitialValuePropertyName), defaultLabel,
                Host.CanReferenceSceneObjects));

            if (Host.Bindings != null && variable.IsExposed)
                settings.Add(CreateBindingField(variable));

            settings.Add(CreateExposedToggle(variable, variableProperty));
            settings.Add(BoundField(variableProperty.FindPropertyRelative("isSaved"), "Saved"));

            return settings;
        }

        private VisualElement CreateWarning(ScenarioVariableDefinition variable, string warning)
        {
            var row = new VisualElement();
            row.AddToClassList("scenario-variable__warning");
            row.Add(new Image { image = ScenarioIcons.Warning });
            row.Add(new Label(warning) { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1f } });

            if (Host.IsGraphFile && !variable.IsExposed)
                row.Add(new Button(() => SetExposed(variable.Id, true)) { text = "Set on Object" });

            return row;
        }

        private VisualElement CreateExposedToggle(ScenarioVariableDefinition variable, SerializedProperty variableProperty)
        {
            var toggle = BoundField(variableProperty.FindPropertyRelative("isExposed"), "Set on Object");
            toggle.tooltip = Host.IsGraphFile ? ExposedHintForFile : ExposedHintForEmbedded;

            // Во встроенном графе привязок нет; уже включённую галку можно только снять.
            toggle.SetEnabled(Host.IsGraphFile || variable.IsExposed);

            // Галка переносит переменную между секциями — пересобрать, когда правка записана.
            toggle.TrackPropertyValue(variableProperty.FindPropertyRelative("isExposed"), _ => schedule.Execute(Rebuild));

            return toggle;
        }

        private void SetExposed(string variableId, bool isExposed)
        {
            var variableProperty = Host.VariableProperty(variableId);

            if (variableProperty == null)
                return;

            variableProperty.FindPropertyRelative("isExposed").boolValue = isExposed;
            variableProperty.serializedObject.ApplyModifiedProperties();
            Rebuild();
        }


        private VisualElement CreateObjectTypeButton(ScenarioVariableDefinition variable)
        {
            var row = new VisualElement();
            row.AddToClassList("scenario-variable__type");
            row.Add(new Label("Type"));

            Button button = null;
            button = new Button(() => new ScenarioObjectTypePicker(objectType =>
                    scenarioGraphView.ApplyEdit(editing => editing.SetVariableObjectType(variable.Id, objectType)))
                .Show(button.worldBound))
            {
                text = variable.ObjectType == typeof(Object) ? "Any Object" : variable.ObjectType.Name,
                tooltip = "Which component or asset fits this variable"
            };

            row.Add(button);

            return row;
        }

        private static VisualElement CreateValueField(ScenarioVariableDefinition variable, SerializedProperty value, string label,
            bool allowsSceneObjects)
        {
            switch (variable.Kind)
            {
                case ScenarioVariableKind.Flag:
                    return BoundField(value.FindPropertyRelative("flagValue"), label);

                case ScenarioVariableKind.Counter:
                    return BoundField(value.FindPropertyRelative("counterValue"), label);

                case ScenarioVariableKind.Number:
                    return BoundField(value.FindPropertyRelative("numberValue"), label);

                default:
                    var objectField = new ObjectField(label) { objectType = variable.ObjectType, allowSceneObjects = allowsSceneObjects };
                    objectField.BindProperty(value.FindPropertyRelative("objectValue"));
                    return objectField;
            }
        }


        // ── Привязка на объекте ──────────────────────────────────────────

        // Своё значение есть — живое поле привязки; нет — поле с значением файла, правка заводит привязку.
        private VisualElement CreateBindingField(ScenarioVariableDefinition variable)
        {
            var bindings = Host.Bindings;
            var label = $"On {bindings.Scenario.name}";
            var row = new VisualElement();
            row.AddToClassList("scenario-variable__binding");

            var boundValue = bindings.ValueOf(variable.Id);

            if (boundValue != null)
            {
                row.Add(CreateValueField(variable, boundValue, label, allowsSceneObjects: true));
                row.Add(new Button(() => EditBindings(edit => edit.RemoveBinding(variable.Id))) { text = "↺", tooltip = "Back to the file's default" });
            }
            else
            {
                row.Add(CreateUnboundField(variable, label));
            }

            return row;
        }

        private VisualElement CreateUnboundField(ScenarioVariableDefinition variable, string label)
        {
            var initial = variable.InitialValue;

            switch (variable.Kind)
            {
                case ScenarioVariableKind.Flag:
                    var toggle = new Toggle(label) { value = initial.FlagValue };
                    toggle.RegisterValueChangedCallback(change => SetBinding(variable, value => value.FindPropertyRelative("flagValue").boolValue = change.newValue));
                    return toggle;

                case ScenarioVariableKind.Counter:
                    var counter = new IntegerField(label) { value = initial.CounterValue, isDelayed = true };
                    counter.RegisterValueChangedCallback(change => SetBinding(variable, value => value.FindPropertyRelative("counterValue").intValue = change.newValue));
                    return counter;

                case ScenarioVariableKind.Number:
                    var number = new FloatField(label) { value = initial.NumberValue, isDelayed = true };
                    number.RegisterValueChangedCallback(change => SetBinding(variable, value => value.FindPropertyRelative("numberValue").floatValue = change.newValue));
                    return number;

                default:
                    var objectField = new ObjectField(label) { objectType = variable.ObjectType, allowSceneObjects = true, value = initial.ObjectValue };
                    objectField.RegisterValueChangedCallback(change => SetBinding(variable, value => value.FindPropertyRelative("objectValue").objectReferenceValue = change.newValue));
                    return objectField;
            }
        }

        private void SetBinding(ScenarioVariableDefinition variable, System.Action<SerializedProperty> setValue)
        {
            EditBindings(edit => setValue(edit.AddBinding(variable.Id)));
        }

        // Правка привязки — правка компонента на объекте: своя запись undo и «изменена сцена», граф-файл не трогается.
        // Пересобирается весь холст: от привязки зависят предупреждения и счётчик проблем.
        private void EditBindings(System.Action<ScenarioBindingsEditing> edit)
        {
            var bindings = Host.Bindings;
            bindings.SerializedScenario.Update();
            edit(bindings);
            bindings.SerializedScenario.ApplyModifiedProperties();
            scenarioGraphView.schedule.Execute(scenarioGraphView.Rebuild);
        }


        // ── Общие помощники ──────────────────────────────────────────────

        private static PropertyField BoundField(SerializedProperty property, string label)
        {
            var field = new PropertyField(property, label);
            field.BindProperty(property);

            return field;
        }

        private static Label HintLabel(string text)
        {
            var hint = new Label(text);
            hint.AddToClassList("scenario-variable__hint");

            return hint;
        }


        // ── Добавить, переименовать ──────────────────────────────────────

        private void ShowAddMenu()
        {
            if (scenarioGraphView.Editing == null)
                return;

            var menu = new GenericMenu();

            foreach (ScenarioVariableKind kind in System.Enum.GetValues(typeof(ScenarioVariableKind)))
                menu.AddItem(new GUIContent(kind.ToString()), false,
                    () => scenarioGraphView.ApplyEdit(editing => expandedVariableIds.Add(editing.AddVariable(kind).Id)));

            menu.ShowAsContext();
        }

        private void Rename(VisualElement element, string newName)
        {
            if (element is BlackboardField { userData: string variableId })
                scenarioGraphView.ApplyEdit(editing => editing.RenameVariable(variableId, newName));
        }
    }
}
