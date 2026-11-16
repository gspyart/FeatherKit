using System.IO;
using System.Linq;
using FeatherKit.Scenarios;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FeatherKit.EditorTools
{
    /// <summary>
    /// Команды над графом целиком в шапке окна — часть ScenarioGraphToolbar.
    /// - Make Graph File, Embed in Object, Convert to Graph: кнопки только там, где применимы
    /// - полоса «старые поля в N блоках» с кнопкой Upgrade Fields
    /// - отказ из-за нод пропавших классов — с выбором «Delete Anyway»
    /// </summary>
    internal class ScenarioGraphFileCommands
    {
        private const string DeleteAnywayText = "Delete Anyway";

        private readonly ScenarioGraphWindow window;
        private readonly ScenarioGraphView graphView;
        private readonly Label oldFieldsText;


        public ScenarioGraphFileCommands(ScenarioGraphWindow window, ScenarioGraphView graphView)
        {
            this.window = window;
            this.graphView = graphView;

            MakeGraphFileButton = ScenarioGraphToolbar.IconButton(ScenarioIcons.GraphFile, "Make Graph File",
                "Move this graph into a graph file other objects can share; scene objects become Set on Object variables. "
                + "Undo brings the graph back into the object, but doesn't delete the created file", MakeGraphFile);
            EmbedButton = ScenarioGraphToolbar.IconButton(ScenarioIcons.TypeIcon(typeof(Scenario)), "Embed in Object",
                "Copy the graph file into this object only; its Set on Object values become the variables' values", EmbedInObject);
            ConvertButton = ScenarioGraphToolbar.IconButton(ScenarioIcons.Flow, "Convert to Graph",
                "This object still uses the old When / If / Do lists — turn them into graph nodes", ConvertToGraph);

            OldFieldsBar = ScenarioGraphToolbar.Bar("scenario-bar--shared", "");
            oldFieldsText = OldFieldsBar.Q<Label>();
            OldFieldsBar.Add(new Button(UpgradeFields)
            {
                text = ScenarioLegacyFieldsUpgrade.ButtonText,
                tooltip = "Move the old fields of these blocks to the new ones. One undo step brings them back"
            });
        }


        public ToolbarButton MakeGraphFileButton { get; }

        public ToolbarButton EmbedButton { get; }

        public ToolbarButton ConvertButton { get; }

        public VisualElement OldFieldsBar { get; }


        public void Refresh(ScenarioGraphHost host)
        {
            Show(MakeGraphFileButton, ScenarioGraphFileConverter.CanMakeGraphFile(host));
            Show(EmbedButton, ScenarioGraphFileConverter.CanEmbed(host));
            Show(ConvertButton, host is { IsAlive: true } && ScenarioGraphConverter.CanConvert(host.Scenario));

            var oldFieldsCount = graphView.Problems.Count(problem => problem.IsOldFields);
            oldFieldsText.text = $"Old fields in {ScenarioLegacyFieldsUpgrade.BlocksText(oldFieldsCount)}: they still work, but press {ScenarioLegacyFieldsUpgrade.ButtonText} to move them";
            Show(OldFieldsBar, oldFieldsCount > 0);
        }

        private static void Show(VisualElement element, bool isShown)
        {
            element.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
        }


        // ── Граф в файл и обратно ────────────────────────────────────────

        private void MakeGraphFile()
        {
            var host = graphView.Host;

            if (!ScenarioGraphFileConverter.CanMakeGraphFile(host) || !ConfirmLosingMissingNodes("Make Graph File", host.Scenario, out var dropsMissing))
                return;

            var scenePath = host.Scenario.gameObject.scene.path;
            var folder = string.IsNullOrEmpty(scenePath) ? "Assets" : Path.GetDirectoryName(scenePath);
            var path = EditorUtility.SaveFilePanelInProject("Make Graph File", $"{host.Scenario.name} Graph", "asset",
                "Where to save the graph file", folder);

            if (string.IsNullOrEmpty(path))
                return;

            var refusals = ScenarioGraphFileConverter.MakeGraphFile(host.Scenario, path, dropsMissing);

            if (refusals.Count > 0)
            {
                EditorUtility.DisplayDialog("Make Graph File",
                    "Can't move the graph to a file:\n\n" + string.Join("\n", refusals.Take(15).Select(refusal => "• " + refusal))
                    + "\n\nScene objects in plain block fields can't use a variable, so a graph file can't keep them. Nothing was changed.", "OK");
                return;
            }

            window.ShowCurrentGraph();
        }

        private void EmbedInObject()
        {
            var host = graphView.Host;

            if (!ScenarioGraphFileConverter.CanEmbed(host) || !ConfirmLosingMissingNodes("Embed in Object", host.Owner, out var dropsMissing))
                return;

            var isConfirmed = EditorUtility.DisplayDialog("Embed in Object",
                $"Copy “{host.Owner.name}” into {host.Scenario.name}? The object stops using the file; the file itself stays as it is.",
                "Embed", "Cancel");

            if (!isConfirmed)
                return;

            ScenarioGraphFileConverter.EmbedInObject(host.Scenario, dropsMissing);
            window.ShowCurrentGraph();
        }

        // Нод без класса не перенести: сказать, каких именно, и дать выбор — бросить их или остаться как есть.
        private static bool ConfirmLosingMissingNodes(string title, Object graphOwner, out bool dropsMissing)
        {
            var message = ScenarioGraphFileConverter.MissingClassesMessageOf(graphOwner);
            dropsMissing = message != null;

            return message == null
                   || EditorUtility.DisplayDialog(title, $"{message}\n\n“{DeleteAnywayText}” goes on without these nodes and their wires.",
                       DeleteAnywayText + "…", "Cancel");
        }


        // ── Прочие команды ───────────────────────────────────────────────

        private void ConvertToGraph()
        {
            var host = graphView.Host;

            if (host is not { IsAlive: true } || !ScenarioGraphConverter.CanConvert(host.Scenario))
                return;

            ScenarioGraphConverter.Convert(host.Scenario);
            graphView.Rebuild();
            graphView.Navigator.FrameWholeGraph();
        }

        private void UpgradeFields()
        {
            if (graphView.Host is not { IsAlive: true } host)
                return;

            ScenarioLegacyFieldsUpgrade.Upgrade(host.Owner);
            graphView.Rebuild();
        }
    }
}
