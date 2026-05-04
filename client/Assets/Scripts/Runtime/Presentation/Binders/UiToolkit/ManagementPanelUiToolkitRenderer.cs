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
            header.Add(new Label(string.IsNullOrWhiteSpace(title) ? "Panel" : title) { name = TitleName });
            header.Add(new Button { name = CloseButtonName, text = "X", tooltip = "Close" });
            root.Add(header);

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
                rowElement.Add(new Label(row.Status) { name = "management-panel-row-status" });
            }

            if (row != null && row.HasAction)
            {
                rowElement.Add(new Label(row.ActionLabel) { name = "management-panel-row-action" });
            }

            return rowElement;
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
