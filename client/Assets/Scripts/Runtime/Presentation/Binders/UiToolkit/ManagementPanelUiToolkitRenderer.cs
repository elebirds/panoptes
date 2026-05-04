using System;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class ManagementPanelUiToolkitRenderer
    {
        public const string RootName = "management-panel-root";
        public const string HeaderName = "management-panel-header";
        public const string TitleName = "management-panel-title";
        public const string CloseButtonName = "management-panel-close-button";
        public const string EmptyName = "management-panel-empty";
        public const string GroupsName = "management-panel-groups";

        private Button _closeButton;
        private Action _closeRequested;
        private Label _empty;
        private VisualElement _groups;
        private Label _title;

        public void Cache(VisualElement root, Action closeRequested = null)
        {
            if (_closeButton != null && _closeRequested != null)
            {
                _closeButton.clicked -= _closeRequested;
            }

            _title = root?.Q<Label>(TitleName);
            _closeButton = root?.Q<Button>(CloseButtonName);
            _empty = root?.Q<Label>(EmptyName);
            _groups = root?.Q<VisualElement>(GroupsName);
            _closeRequested = closeRequested;

            if (_closeButton != null && _closeRequested != null)
            {
                _closeButton.clicked += _closeRequested;
            }
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

            var header = new VisualElement { name = HeaderName };
            header.AddToClassList("management-panel-header");
            header.Add(CreateLabel(string.IsNullOrWhiteSpace(title) ? "Panel" : title, TitleName, "management-panel-title"));
            var closeButton = new Button { name = CloseButtonName, text = "X", tooltip = "Close" };
            closeButton.AddToClassList("management-panel-close-button");
            header.Add(closeButton);
            root.Add(header);

            root.Add(CreateLabel("No entries", EmptyName, "management-panel-empty"));

            var groups = new VisualElement { name = GroupsName };
            groups.AddToClassList("management-panel-groups");
            root.Add(groups);
            return root;
        }

        private static VisualElement CreateGroup(ManagementPanelGroupState group, Action<string> rowActionRequested)
        {
            var groupElement = new VisualElement { name = "management-panel-group-" + SafeName(group?.Id) };
            groupElement.AddToClassList("management-panel-group");
            groupElement.Add(CreateLabel(group?.Title ?? "Other", "management-panel-group-title", "management-panel-group-title"));
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
            if (row != null && row.HasIcon)
            {
                rowElement.Add(CreateImage(row.IconKey));
            }

            var title = new Label(row?.Title ?? "Entry") { name = "management-panel-row-title" };
            title.AddToClassList("management-panel-row-title");
            rowElement.Add(title);

            if (!string.IsNullOrWhiteSpace(row?.Summary))
            {
                var summary = new Label(row.Summary) { name = "management-panel-row-summary" };
                summary.AddToClassList("management-panel-row-summary");
                rowElement.Add(summary);
            }

            if (!string.IsNullOrWhiteSpace(row?.Detail))
            {
                var detail = new Label(row.Detail) { name = "management-panel-row-detail" };
                detail.AddToClassList("management-panel-row-detail");
                rowElement.Add(detail);
            }

            if (!string.IsNullOrWhiteSpace(row?.Status))
            {
                rowElement.Add(CreateLabel(row.Status, "management-panel-row-status", "management-panel-row-status"));
            }

            if (row != null && row.HasAction)
            {
                rowElement.Add(CreateLabel(row.ActionLabel, "management-panel-row-action", "management-panel-row-action"));
            }

            return rowElement;
        }

        private static Label CreateLabel(string text, string name, string className)
        {
            var label = new Label(text ?? string.Empty) { name = name };
            if (!string.IsNullOrWhiteSpace(className))
            {
                label.AddToClassList(className);
            }

            return label;
        }

        private static VisualElement CreateImage(string iconKey)
        {
            var image = new VisualElement { name = "management-panel-row-image" };
            image.AddToClassList("management-panel-row-image");
            var texture = LoadPolicyTexture(iconKey);
            if (texture != null)
            {
                image.style.backgroundImage = new StyleBackground(texture);
            }

            return image;
        }

        private static Texture2D LoadPolicyTexture(string iconKey)
        {
            if (string.IsNullOrWhiteSpace(iconKey))
            {
                return null;
            }

            var key = iconKey.Trim();
            return Resources.Load<Texture2D>("Icons/Policy/" + key) ??
                   Resources.Load<Texture2D>("Icons/Policies/" + key) ??
                   Resources.Load<Texture2D>("Icons/" + key);
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
