using System;
using Panoptes.Presentation.ViewModels;
using UnityEngine.UIElements;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class ManagementPanelUiToolkitRenderer
    {
        public const string RootName = "management-panel-root";
        public const string TitleName = "management-panel-title";
        public const string EmptyName = "management-panel-empty";
        public const string GroupsName = "management-panel-groups";

        private Label _empty;
        private VisualElement _groups;
        private Label _title;

        public void Cache(VisualElement root)
        {
            _title = root?.Q<Label>(TitleName);
            _empty = root?.Q<Label>(EmptyName);
            _groups = root?.Q<VisualElement>(GroupsName);
        }

        public void Render(ManagementPanelState state, Action<string> rowActionRequested)
        {
            state ??= new ManagementPanelState();
            SetText(_title, state.Title);
            if (_groups == null)
            {
                return;
            }

            _groups.Clear();
            if (!state.HasGroups)
            {
                SetDisplay(_empty, DisplayStyle.Flex);
                return;
            }

            SetDisplay(_empty, DisplayStyle.None);
            for (var i = 0; i < state.Groups.Count; i++)
            {
                _groups.Add(CreateGroup(state.Groups[i], rowActionRequested));
            }
        }

        public static VisualElement BuildFallbackTree(string title)
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("management-panel-root");
            root.Add(new Label(string.IsNullOrWhiteSpace(title) ? "Panel" : title) { name = TitleName });
            root.Add(new Label("No entries") { name = EmptyName });
            root.Add(new VisualElement { name = GroupsName });
            return root;
        }

        private static VisualElement CreateGroup(ManagementPanelGroupState group, Action<string> rowActionRequested)
        {
            var groupElement = new VisualElement { name = "management-panel-group-" + SafeName(group?.Id) };
            groupElement.AddToClassList("management-panel-group");
            groupElement.Add(new Label(group?.Title ?? "Other") { name = "management-panel-group-title" });
            if (group?.Rows == null)
            {
                return groupElement;
            }

            for (var i = 0; i < group.Rows.Count; i++)
            {
                groupElement.Add(CreateRow(group.Rows[i], rowActionRequested));
            }

            return groupElement;
        }

        private static VisualElement CreateRow(ManagementPanelRowState row, Action<string> rowActionRequested)
        {
            var rowElement = row != null && row.HasAction
                ? new Button(() => rowActionRequested?.Invoke(row.Id))
                : new VisualElement();
            rowElement.name = "management-panel-row-" + SafeName(row?.Id);
            rowElement.AddToClassList("management-panel-row");
            rowElement.Add(new Label(row?.Title ?? "Entry") { name = "management-panel-row-title" });

            if (!string.IsNullOrWhiteSpace(row?.Summary))
            {
                rowElement.Add(new Label(row.Summary) { name = "management-panel-row-summary" });
            }

            if (!string.IsNullOrWhiteSpace(row?.Detail))
            {
                rowElement.Add(new Label(row.Detail) { name = "management-panel-row-detail" });
            }

            if (!string.IsNullOrWhiteSpace(row?.Status))
            {
                rowElement.Add(new Label(row.Status) { name = "management-panel-row-status" });
            }

            if (row != null && row.HasAction)
            {
                rowElement.Add(new Label(row.ActionLabel) { name = "management-panel-row-action" });
            }

            return rowElement;
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private static void SetText(Label label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static void SetDisplay(VisualElement element, DisplayStyle display)
        {
            if (element != null)
            {
                element.style.display = display;
            }
        }
    }
}
