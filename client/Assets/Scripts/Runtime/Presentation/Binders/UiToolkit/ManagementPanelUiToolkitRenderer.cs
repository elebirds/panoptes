using System;
using System.Collections.Generic;
using Panoptes.Presentation.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Panoptes.Presentation.Binders.UiToolkit
{
    public sealed class ManagementPanelUiToolkitRenderer
    {
        public const string RootName = "management-panel-root";
        public const string TitleName = "management-panel-title";
        public const string CloseButtonName = "management-panel-close";
        public const string EmptyName = "management-panel-empty";
        public const string GroupsName = "management-panel-groups";
        private const string TechTreeBackgroundResource = "Textures/UI/tech_tree_background";
        private const string PolicyFocusBackgroundResource = "Textures/UI/policy_focus_background";
        private static readonly TechConnectorColors[] TechConnectorPalette =
        {
            new(new Color(0.98f, 0.98f, 0.92f, 0.95f), new Color(1f, 1f, 0.96f, 1f)),
            new(new Color(0.02f, 0.02f, 0.02f, 0.95f), new Color(0f, 0f, 0f, 1f)),
            new(new Color(0.22f, 0.55f, 1f, 0.95f), new Color(0.36f, 0.68f, 1f, 1f)),
            new(new Color(0.16f, 0.82f, 0.34f, 0.95f), new Color(0.26f, 0.95f, 0.46f, 1f)),
            new(new Color(1f, 0.22f, 0.18f, 0.95f), new Color(1f, 0.34f, 0.30f, 1f)),
            new(new Color(0.70f, 0.32f, 1f, 0.95f), new Color(0.82f, 0.48f, 1f, 1f))
        };

        private Button _closeButton;
        private Label _empty;
        private VisualElement _groups;
        private VisualElement _root;
        private Label _title;
        private Action _closeRequested;

        public void Cache(VisualElement root, Action closeRequested = null)
        {
            if (_closeButton != null && _closeRequested != null)
            {
                _closeButton.clicked -= _closeRequested;
            }

            _closeRequested = closeRequested;
            _root = root?.Q<VisualElement>(RootName);
            _title = root?.Q<Label>(TitleName);
            _closeButton = root?.Q<Button>(CloseButtonName);
            _empty = root?.Q<Label>(EmptyName);
            _groups = root?.Q<VisualElement>(GroupsName);
            if (_groups != null)
            {
                _groups.style.flexGrow = 1f;
                _groups.style.minHeight = 0f;
            }

            if (_closeButton != null && _closeRequested != null)
            {
                _closeButton.clicked -= _closeRequested;
                _closeButton.clicked += _closeRequested;
            }
        }

        public void Render(ManagementPanelState state, Action<string> rowActionRequested)
        {
            state ??= new ManagementPanelState();
            SetText(_title, state.Title);
            ApplyTitleStyle(state.Title);
            ApplyCloseButtonStyle(state.Title);
            ApplyPanelBackground(state.Title);
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
            if (IsTechTreeTitle(state.Title))
            {
                ApplyTechTreeTitleStyle();
                ConfigureTechTreeOuterScroll();
                _groups.Add(CreateTechTree(state, rowActionRequested));
                return;
            }

            if (IsPolicyFocusTitle(state.Title))
            {
                ConfigurePolicyFocusOuterScroll();
                _groups.Add(CreatePolicyFocusList(state, rowActionRequested));
                return;
            }

            for (var i = 0; i < state.Groups.Count; i++)
            {
                _groups.Add(CreateGroup(state.Groups[i], rowActionRequested));
            }
        }

        public static VisualElement BuildFallbackTree(string title)
        {
            var root = new VisualElement { name = RootName };
            root.AddToClassList("management-panel-root");
            root.style.flexDirection = FlexDirection.Column;

            var header = new VisualElement { name = "management-panel-header" };
            header.AddToClassList("management-panel-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14f;

            var titleLabel = CreateLabel(string.IsNullOrWhiteSpace(title) ? "面板" : title, TitleName, "management-panel-title");
            titleLabel.style.flexGrow = 1f;
            header.Add(titleLabel);

            var close = new Button { name = CloseButtonName, text = "X" };
            close.AddToClassList("management-panel-close");
            close.style.width = 34f;
            close.style.height = 30f;
            close.style.flexShrink = 0f;
            close.style.backgroundColor = new Color(0.20f, 0.08f, 0.045f, 0.95f);
            close.style.color = new Color(1f, 0.86f, 0.62f, 1f);
            close.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(close);
            root.Add(header);
            root.Add(CreateLabel("暂无内容", EmptyName, "management-panel-empty"));

            var scroll = new ScrollView { name = "management-panel-scroll" };
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.overflow = Overflow.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            EnableDragScroll(scroll);

            var groups = new VisualElement { name = GroupsName };
            groups.AddToClassList("management-panel-groups");
            groups.style.flexGrow = 1f;
            groups.style.minHeight = 0f;
            scroll.Add(groups);
            root.Add(scroll);
            return root;
        }

        private static VisualElement CreateGroup(ManagementPanelGroupState group, Action<string> rowActionRequested)
        {
            var groupElement = new VisualElement { name = "management-panel-group-" + SafeName(group?.Id) };
            groupElement.AddToClassList("management-panel-group");
            groupElement.style.flexGrow = 1f;
            groupElement.Add(CreateLabel(group?.Title ?? "其他", "management-panel-group-title", "management-panel-group-title"));
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
            rowElement.style.backgroundColor = new Color(0.10f, 0.07f, 0.055f, 0.96f);
            rowElement.style.borderBottomColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderLeftColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderRightColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderTopColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderBottomWidth = 1f;
            rowElement.style.borderLeftWidth = 1f;
            rowElement.style.borderRightWidth = 1f;
            rowElement.style.borderTopWidth = 1f;
            rowElement.style.marginBottom = 12f;
            rowElement.style.paddingBottom = 12f;
            rowElement.style.paddingLeft = 12f;
            rowElement.style.paddingRight = 12f;
            rowElement.style.paddingTop = 12f;
            ApplyRowStatusStyle(rowElement, row);
            var header = new VisualElement { name = "management-panel-row-header" };
            header.AddToClassList("management-panel-row-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8f;
            var icon = CreateIcon(row);
            if (icon != null)
            {
                header.Add(icon);
            }

            header.Add(CreateLabel(row?.Title ?? "条目", "management-panel-row-title", "management-panel-row-title"));
            rowElement.Add(header);

            if (!string.IsNullOrWhiteSpace(row?.Summary))
            {
                rowElement.Add(CreateLabel(row.Summary, "management-panel-row-summary", "management-panel-row-summary"));
            }

            if (!string.IsNullOrWhiteSpace(row?.Detail))
            {
                rowElement.Add(CreateLabel(row.Detail, "management-panel-row-detail", "management-panel-row-detail"));
            }

            if (row?.Costs != null && row.Costs.Count > 0)
            {
                rowElement.Add(CreateAmountStrip("消耗", row.Costs));
            }
            else if (!string.IsNullOrWhiteSpace(row?.EmptyCostsLabel))
            {
                rowElement.Add(CreateAmountStrip("消耗", row.Costs, row.EmptyCostsLabel));
            }

            if (row?.Outputs != null && row.Outputs.Count > 0)
            {
                rowElement.Add(CreateAmountStrip("产出", row.Outputs));
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

        private static VisualElement CreatePolicyFocusList(ManagementPanelState state, Action<string> rowActionRequested)
        {
            var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "management-panel-policy-scroll" };
            scroll.AddToClassList("management-panel-policy-scroll");
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.overflow = Overflow.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            EnableHorizontalDragScroll(scroll);

            var track = new VisualElement { name = "management-panel-policy-track" };
            track.AddToClassList("management-panel-policy-track");
            track.style.flexDirection = FlexDirection.Row;
            track.style.alignItems = Align.Stretch;
            track.style.minHeight = 720f;
            track.style.paddingBottom = 24f;
            track.style.paddingRight = 56f;

            var rows = FlattenRows(state);
            for (var i = 0; i < rows.Count; i++)
            {
                track.Add(CreatePolicyFocusRow(rows[i], rowActionRequested));
            }

            scroll.Add(track);
            return scroll;
        }

        private static VisualElement CreatePolicyFocusRow(ManagementPanelRowState row, Action<string> rowActionRequested)
        {
            var rowElement = row != null && row.HasAction
                ? new Button(() => rowActionRequested?.Invoke(row.Id))
                : new VisualElement();
            rowElement.name = "management-panel-row-" + SafeName(row?.Id);
            rowElement.AddToClassList("management-panel-row");
            rowElement.AddToClassList("policy-focus-row");
            rowElement.tooltip = row?.ActionLabel ?? string.Empty;
            rowElement.style.flexDirection = FlexDirection.Column;
            rowElement.style.alignItems = Align.Stretch;
            rowElement.style.width = 360f;
            rowElement.style.minHeight = 700f;
            rowElement.style.height = Length.Percent(100f);
            rowElement.style.flexShrink = 0f;
            rowElement.style.marginRight = 22f;
            rowElement.style.paddingBottom = 24f;
            rowElement.style.paddingLeft = 22f;
            rowElement.style.paddingRight = 22f;
            rowElement.style.paddingTop = 22f;
            rowElement.style.backgroundColor = new Color(0.10f, 0.07f, 0.055f, 0.96f);
            rowElement.style.borderBottomColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderLeftColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderRightColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderTopColor = new Color(0.36f, 0.24f, 0.14f, 0.95f);
            rowElement.style.borderBottomWidth = 1f;
            rowElement.style.borderLeftWidth = 1f;
            rowElement.style.borderRightWidth = 1f;
            rowElement.style.borderTopWidth = 1f;
            ApplyRowStatusStyle(rowElement, row);

            var icon = CreateIcon(row);
            if (icon != null)
            {
                icon.AddToClassList("policy-focus-row-image");
                icon.style.alignSelf = Align.Center;
                icon.style.width = 310f;
                icon.style.height = 220f;
                icon.style.marginBottom = 20f;
                icon.style.marginRight = 0f;
                rowElement.Add(icon);
            }

            var title = CreateLabel(row?.Title ?? "条目", "management-panel-row-title", "management-panel-row-title");
            title.AddToClassList("policy-focus-row-title");
            title.style.fontSize = 26f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 18f;
            rowElement.Add(title);

            if (!string.IsNullOrWhiteSpace(row?.Summary))
            {
                var summary = CreateLabel(row.Summary, "management-panel-row-summary", "management-panel-row-summary");
                summary.AddToClassList("policy-focus-row-effect");
                summary.style.fontSize = 20f;
                summary.style.unityFontStyleAndWeight = FontStyle.Bold;
                summary.style.color = new Color(1f, 0.83f, 0.42f, 1f);
                summary.style.marginBottom = 18f;
                rowElement.Add(summary);
            }

            if (!string.IsNullOrWhiteSpace(row?.Detail))
            {
                var detail = CreateLabel(row.Detail, "management-panel-row-detail", "management-panel-row-detail");
                detail.AddToClassList("policy-focus-row-detail");
                detail.style.fontSize = 18f;
                detail.style.color = new Color(0.88f, 0.91f, 0.94f, 1f);
                rowElement.Add(detail);
            }

            return rowElement;
        }

        private static VisualElement CreateTechTree(ManagementPanelState state, Action<string> rowActionRequested)
        {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { name = "management-panel-tech-scroll" };
            scroll.AddToClassList("management-panel-tech-scroll");
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;
            scroll.style.overflow = Overflow.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            EnableDragScroll(scroll);

            var columns = BuildTechColumns(state);
            var titleById = BuildTitleById(columns);
            var dependentIds = BuildDependentIds(columns);
            var track = new VisualElement { name = "management-panel-tech-columns" };
            track.AddToClassList("management-panel-tech-columns");
            track.style.position = Position.Relative;
            track.style.flexDirection = FlexDirection.Row;
            track.style.alignItems = Align.FlexStart;
            track.style.minHeight = 720f;
            track.style.paddingBottom = 40f;
            track.style.paddingRight = 40f;

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                track.Add(CreateTechColumn(columnIndex, columns.Count, columns[columnIndex], titleById, dependentIds, rowActionRequested));
            }

            var connectorLayer = new VisualElement { name = "management-panel-tech-connector-layer" };
            connectorLayer.pickingMode = PickingMode.Ignore;
            connectorLayer.style.position = Position.Absolute;
            connectorLayer.style.left = 0f;
            connectorLayer.style.top = 0f;
            connectorLayer.style.right = 0f;
            connectorLayer.style.bottom = 0f;
            track.Add(connectorLayer);
            connectorLayer.SendToBack();
            ScheduleTechConnectorRebuild(track, connectorLayer, state);
            track.RegisterCallback<GeometryChangedEvent>(_ => RebuildTechConnectors(track, connectorLayer, state));

            scroll.Add(track);
            return scroll;
        }

        private static VisualElement CreateTechColumn(
            int columnIndex,
            int columnCount,
            IReadOnlyList<ManagementPanelRowState> rows,
            IReadOnlyDictionary<string, string> titleById,
            ISet<string> dependentIds,
            Action<string> rowActionRequested)
        {
            var column = new VisualElement { name = "management-panel-tech-column-" + columnIndex };
            column.AddToClassList("management-panel-tech-column");
            column.style.width = 268f;
            column.style.flexShrink = 0f;
            column.style.marginRight = columnIndex < columnCount - 1 ? 48f : 0f;

            if (rows != null)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = CreateTechNode(rows[i], columnIndex, columnCount, titleById, dependentIds, rowActionRequested);
                    row.AddToClassList("management-panel-tech-node");
                    row.style.width = 244f;
                    row.style.minHeight = 168f;
                    row.style.flexShrink = 0f;
                    column.Add(row);
                }
            }

            return column;
        }

        private static VisualElement CreateTechNode(
            ManagementPanelRowState state,
            int columnIndex,
            int columnCount,
            IReadOnlyDictionary<string, string> titleById,
            ISet<string> dependentIds,
            Action<string> rowActionRequested)
        {
            var wrapper = new VisualElement { name = "management-panel-tech-node-wrap-" + SafeName(state?.Id) };
            wrapper.style.position = Position.Relative;
            wrapper.style.marginTop = 12f;
            wrapper.style.marginBottom = 14f;
            wrapper.style.marginLeft = columnIndex > 0 ? 16f : 0f;
            wrapper.style.marginRight = 0f;
            wrapper.style.width = 260f;
            wrapper.style.flexShrink = 0f;

            var row = CreateRow(state, rowActionRequested);
            wrapper.Add(row);
            if (HasPrerequisites(state))
            {
                wrapper.Add(CreatePrerequisiteHint(state, titleById));
            }
            return wrapper;
        }

        private static VisualElement CreatePrerequisiteHint(
            ManagementPanelRowState state,
            IReadOnlyDictionary<string, string> titleById)
        {
            var hint = new VisualElement { name = "management-panel-tech-prerequisites" };
            hint.AddToClassList("management-panel-tech-prerequisites");
            hint.style.flexDirection = FlexDirection.Row;
            hint.style.flexWrap = Wrap.Wrap;
            hint.style.marginTop = 6f;
            hint.style.marginLeft = 8f;

            var title = CreateLabel("前置：", "management-panel-tech-prerequisite-title", "management-panel-tech-prerequisite-title");
            title.style.color = new Color(1f, 0.82f, 0.52f, 1f);
            hint.Add(title);

            var prerequisites = state?.PrerequisiteIds;
            for (var i = 0; prerequisites != null && i < prerequisites.Count; i++)
            {
                var id = NormalizeId(prerequisites[i]);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var label = titleById != null && titleById.TryGetValue(id, out var resolvedTitle) && !string.IsNullOrWhiteSpace(resolvedTitle)
                    ? resolvedTitle
                    : id;
                var chip = CreateLabel(label, "management-panel-tech-prerequisite", "management-panel-tech-prerequisite");
                chip.style.marginRight = 8f;
                chip.style.color = new Color(0.86f, 0.74f, 0.56f, 1f);
                hint.Add(chip);
            }

            return hint;
        }

        private static VisualElement CreateTechConnectorSegment(string name)
        {
            var connector = new VisualElement { name = name };
            connector.AddToClassList("management-panel-tech-connector");
            connector.style.position = Position.Absolute;
            connector.style.top = Length.Percent(50f);
            connector.style.height = 3f;
            connector.style.backgroundColor = new Color(1f, 0.72f, 0.28f, 0.9f);
            connector.style.borderBottomColor = new Color(1f, 0.92f, 0.55f, 0.72f);
            connector.style.borderBottomWidth = 1f;
            return connector;
        }

        private static void ScheduleTechConnectorRebuild(
            VisualElement track,
            VisualElement connectorLayer,
            ManagementPanelState state)
        {
            if (track == null || connectorLayer == null)
            {
                return;
            }

            track.schedule.Execute(() => RebuildTechConnectors(track, connectorLayer, state)).ExecuteLater(0);
            track.schedule.Execute(() => RebuildTechConnectors(track, connectorLayer, state)).ExecuteLater(120);
            track.schedule.Execute(() => RebuildTechConnectors(track, connectorLayer, state)).ExecuteLater(300);
        }

        private static void RebuildTechConnectors(
            VisualElement track,
            VisualElement connectorLayer,
            ManagementPanelState state)
        {
            if (track == null || connectorLayer == null || state == null)
            {
                return;
            }

            connectorLayer.Clear();
            var rows = FlattenRows(state);
            if (rows.Count == 0)
            {
                return;
            }

            var nodesById = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                var id = NormalizeId(rows[i]?.Id);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var node = track.Q<VisualElement>("management-panel-tech-node-wrap-" + SafeName(id));
                if (node != null)
                {
                    nodesById[id] = node;
                }
            }

            var trackBounds = track.worldBound;
            if (trackBounds.width <= 1f || trackBounds.height <= 1f)
            {
                return;
            }

            var colorsById = BuildTechConnectorColors(rows, nodesById);
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var target = rows[rowIndex];
                var targetId = NormalizeId(target?.Id);
                if (string.IsNullOrEmpty(targetId) || !nodesById.TryGetValue(targetId, out var targetNode))
                {
                    continue;
                }

                var prerequisites = target.PrerequisiteIds;
                if (prerequisites == null)
                {
                    continue;
                }

                for (var prerequisiteIndex = 0; prerequisiteIndex < prerequisites.Count; prerequisiteIndex++)
                {
                    var prerequisiteId = NormalizeId(prerequisites[prerequisiteIndex]);
                    if (string.IsNullOrEmpty(prerequisiteId) ||
                        !nodesById.TryGetValue(prerequisiteId, out var prerequisiteNode))
                    {
                        continue;
                    }

                    AddTechConnector(
                        connectorLayer,
                        trackBounds,
                        prerequisiteNode.worldBound,
                        targetNode.worldBound,
                        ResolveTechConnectorColors(targetId, colorsById));
                }
            }
        }

        private static void AddTechConnector(
            VisualElement connectorLayer,
            Rect trackBounds,
            Rect fromBounds,
            Rect toBounds,
            TechConnectorColors colors)
        {
            if (connectorLayer == null ||
                fromBounds.width <= 1f ||
                fromBounds.height <= 1f ||
                toBounds.width <= 1f ||
                toBounds.height <= 1f)
            {
                return;
            }

            var startX = fromBounds.xMax - trackBounds.xMin;
            var startY = fromBounds.center.y - trackBounds.yMin;
            var endX = toBounds.xMin - trackBounds.xMin;
            var endY = toBounds.center.y - trackBounds.yMin;

            var line = new TechConnectorLine(
                new Vector2(startX, startY),
                new Vector2(endX, endY),
                3f,
                colors.Line,
                colors.Arrow)
            {
                name = "management-panel-tech-connector-line",
                pickingMode = PickingMode.Ignore
            };
            line.AddToClassList("management-panel-tech-connector");
            line.style.position = Position.Absolute;
            line.style.left = 0f;
            line.style.top = 0f;
            line.style.right = 0f;
            line.style.bottom = 0f;
            connectorLayer.Add(line);
        }

        private readonly struct TechConnectorColors
        {
            public readonly Color Arrow;
            public readonly Color Line;

            public TechConnectorColors(Color line, Color arrow)
            {
                Line = line;
                Arrow = arrow;
            }
        }

        private static TechConnectorColors ResolveTechConnectorColors(
            string technologyId,
            IReadOnlyDictionary<string, TechConnectorColors> colorsById)
        {
            return !string.IsNullOrWhiteSpace(technologyId) &&
                   colorsById != null &&
                   colorsById.TryGetValue(technologyId, out var colors)
                ? colors
                : TechConnectorPalette[0];
        }

        private static IReadOnlyDictionary<string, TechConnectorColors> BuildTechConnectorColors(
            IReadOnlyList<ManagementPanelRowState> rows,
            IReadOnlyDictionary<string, VisualElement> nodesById)
        {
            var result = new Dictionary<string, TechConnectorColors>(StringComparer.Ordinal);
            var rowsById = new Dictionary<string, ManagementPanelRowState>(StringComparer.Ordinal);
            var orderById = new Dictionary<string, int>(StringComparer.Ordinal);
            if (rows == null || rows.Count == 0)
            {
                return result;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var id = NormalizeId(rows[i]?.Id);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                rowsById[id] = rows[i];
                orderById[id] = i;
            }

            var roots = new List<ManagementPanelRowState>();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && !HasPrerequisites(row))
                {
                    roots.Add(row);
                }
            }

            roots.Sort((left, right) => CompareTechRowsByVisualTop(left, right, nodesById, orderById));
            for (var i = 0; i < roots.Count; i++)
            {
                var id = NormalizeId(roots[i]?.Id);
                if (!string.IsNullOrEmpty(id))
                {
                    result[id] = TechConnectorPalette[i % TechConnectorPalette.Length];
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                ResolveInheritedTechConnectorColors(rows[i], rowsById, nodesById, orderById, result, new HashSet<string>(StringComparer.Ordinal));
            }

            return result;
        }

        private static TechConnectorColors ResolveInheritedTechConnectorColors(
            ManagementPanelRowState row,
            IReadOnlyDictionary<string, ManagementPanelRowState> rowsById,
            IReadOnlyDictionary<string, VisualElement> nodesById,
            IReadOnlyDictionary<string, int> orderById,
            IDictionary<string, TechConnectorColors> result,
            ISet<string> visiting)
        {
            var id = NormalizeId(row?.Id);
            if (string.IsNullOrEmpty(id))
            {
                return TechConnectorPalette[0];
            }

            if (result.TryGetValue(id, out var existing))
            {
                return existing;
            }

            if (!visiting.Add(id))
            {
                return TechConnectorPalette[0];
            }

            var selectedPrerequisite = ResolveUpperPrerequisiteId(row, rowsById, nodesById, orderById);
            var colors = !string.IsNullOrEmpty(selectedPrerequisite) &&
                         rowsById.TryGetValue(selectedPrerequisite, out var prerequisite)
                ? ResolveInheritedTechConnectorColors(prerequisite, rowsById, nodesById, orderById, result, visiting)
                : TechConnectorPalette[0];

            visiting.Remove(id);
            result[id] = colors;
            return colors;
        }

        private static string ResolveUpperPrerequisiteId(
            ManagementPanelRowState row,
            IReadOnlyDictionary<string, ManagementPanelRowState> rowsById,
            IReadOnlyDictionary<string, VisualElement> nodesById,
            IReadOnlyDictionary<string, int> orderById)
        {
            var prerequisites = row?.PrerequisiteIds;
            if (prerequisites == null || prerequisites.Count == 0)
            {
                return string.Empty;
            }

            var selected = string.Empty;
            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisiteId = NormalizeId(prerequisites[i]);
                if (string.IsNullOrEmpty(prerequisiteId) || !rowsById.ContainsKey(prerequisiteId))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(selected) ||
                    CompareTechIdsByVisualTop(prerequisiteId, selected, nodesById, orderById) < 0)
                {
                    selected = prerequisiteId;
                }
            }

            return selected;
        }

        private static int CompareTechRowsByVisualTop(
            ManagementPanelRowState left,
            ManagementPanelRowState right,
            IReadOnlyDictionary<string, VisualElement> nodesById,
            IReadOnlyDictionary<string, int> orderById)
        {
            return CompareTechIdsByVisualTop(NormalizeId(left?.Id), NormalizeId(right?.Id), nodesById, orderById);
        }

        private static int CompareTechIdsByVisualTop(
            string leftId,
            string rightId,
            IReadOnlyDictionary<string, VisualElement> nodesById,
            IReadOnlyDictionary<string, int> orderById)
        {
            var topCompare = ResolveTechNodeTop(leftId, nodesById).CompareTo(ResolveTechNodeTop(rightId, nodesById));
            if (topCompare != 0)
            {
                return topCompare;
            }

            return ResolveTechRowOrder(leftId, orderById).CompareTo(ResolveTechRowOrder(rightId, orderById));
        }

        private static float ResolveTechNodeTop(string id, IReadOnlyDictionary<string, VisualElement> nodesById)
        {
            return !string.IsNullOrEmpty(id) &&
                   nodesById != null &&
                   nodesById.TryGetValue(id, out var node) &&
                   node != null
                ? node.worldBound.yMin
                : float.MaxValue;
        }

        private static int ResolveTechRowOrder(string id, IReadOnlyDictionary<string, int> orderById)
        {
            return !string.IsNullOrEmpty(id) &&
                   orderById != null &&
                   orderById.TryGetValue(id, out var order)
                ? order
                : int.MaxValue;
        }

        private sealed class TechConnectorLine : VisualElement
        {
            private readonly Color _color;
            private readonly Vector2 _end;
            private readonly Color _headColor;
            private readonly Vector2 _start;
            private readonly float _width;

            public TechConnectorLine(Vector2 start, Vector2 end, float width, Color color, Color headColor)
            {
                _start = start;
                _end = end;
                _width = Mathf.Max(1f, width);
                _color = color;
                _headColor = headColor;
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                var direction = _end - _start;
                if (direction.sqrMagnitude <= 0.01f)
                {
                    return;
                }

                direction.Normalize();
                var normal = new Vector2(-direction.y, direction.x);
                const float arrowLength = 13f;
                const float arrowHalfWidth = 6f;
                var lineEnd = _end - direction * 3f;
                var arrowBase = _end - direction * arrowLength;
                var arrowLeft = arrowBase + normal * arrowHalfWidth;
                var arrowRight = arrowBase - normal * arrowHalfWidth;

                var painter = context.painter2D;
                painter.lineWidth = _width;
                painter.strokeColor = _color;
                painter.BeginPath();
                painter.MoveTo(_start);
                painter.LineTo(lineEnd);
                painter.Stroke();

                painter.fillColor = _headColor;
                painter.BeginPath();
                painter.MoveTo(_end);
                painter.LineTo(arrowLeft);
                painter.LineTo(arrowRight);
                painter.ClosePath();
                painter.Fill();
            }
        }

        private static List<ManagementPanelRowState> FlattenRows(ManagementPanelState state)
        {
            var rows = new List<ManagementPanelRowState>();
            if (state?.Groups == null)
            {
                return rows;
            }

            for (var groupIndex = 0; groupIndex < state.Groups.Count; groupIndex++)
            {
                var groupRows = state.Groups[groupIndex]?.Rows;
                if (groupRows == null)
                {
                    continue;
                }

                for (var rowIndex = 0; rowIndex < groupRows.Count; rowIndex++)
                {
                    if (groupRows[rowIndex] != null)
                    {
                        rows.Add(groupRows[rowIndex]);
                    }
                }
            }

            return rows;
        }

        private static void ApplyRowStatusStyle(VisualElement rowElement, ManagementPanelRowState row)
        {
            if (rowElement == null || row == null)
            {
                return;
            }

            var status = row.Status ?? string.Empty;
            if (ContainsAny(status, "已研究", "已完成", "已激活"))
            {
                ApplyRowPalette(
                    rowElement,
                    new Color(0.055f, 0.22f, 0.12f, 0.98f),
                    new Color(0.22f, 0.78f, 0.34f, 0.95f));
                return;
            }

            if (ContainsAny(status, "研究中", "已选择", "已规划", "已设为研究目标"))
            {
                ApplyRowPalette(
                    rowElement,
                    new Color(0.055f, 0.12f, 0.28f, 0.98f),
                    new Color(0.24f, 0.56f, 1f, 0.95f));
            }
        }

        private static void ApplyRowPalette(VisualElement rowElement, Color background, Color border)
        {
            rowElement.style.backgroundColor = background;
            rowElement.style.borderBottomColor = border;
            rowElement.style.borderLeftColor = border;
            rowElement.style.borderRightColor = border;
            rowElement.style.borderTopColor = border;
            rowElement.style.borderBottomWidth = 2f;
            rowElement.style.borderLeftWidth = 2f;
            rowElement.style.borderRightWidth = 2f;
            rowElement.style.borderTopWidth = 2f;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(value) || needles == null)
            {
                return false;
            }

            for (var i = 0; i < needles.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(needles[i]) &&
                    value.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static VisualElement CreateAmountStrip(
            string label,
            IReadOnlyList<ManagementPanelAmountState> amounts,
            string emptyLabel = "")
        {
            var strip = new VisualElement { name = "management-panel-amount-strip-" + SafeName(label) };
            strip.AddToClassList("management-panel-amount-strip");
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.flexWrap = Wrap.Wrap;
            strip.style.alignItems = Align.Center;
            strip.style.marginTop = 8f;
            strip.style.marginBottom = 4f;

            var title = CreateLabel(label + "：", "management-panel-amount-strip-title", "management-panel-amount-strip-title");
            title.style.marginRight = 6f;
            title.style.color = new Color(1f, 0.82f, 0.52f, 1f);
            strip.Add(title);

            if ((amounts == null || amounts.Count == 0) && !string.IsNullOrWhiteSpace(emptyLabel))
            {
                var empty = CreateLabel(emptyLabel, "management-panel-amount-empty", "management-panel-amount-empty");
                empty.style.color = new Color(0.86f, 0.78f, 0.64f, 1f);
                strip.Add(empty);
                return strip;
            }

            for (var i = 0; amounts != null && i < amounts.Count; i++)
            {
                var amount = amounts[i];
                if (amount == null)
                {
                    continue;
                }

                strip.Add(CreateAmountPill(amount));
            }

            return strip;
        }

        private static VisualElement CreateAmountPill(ManagementPanelAmountState amount)
        {
            var pill = new VisualElement { name = "management-panel-amount-" + SafeName(amount?.Id) };
            pill.AddToClassList("management-panel-amount");
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.marginRight = 8f;
            pill.style.marginBottom = 6f;
            pill.style.paddingBottom = 3f;
            pill.style.paddingLeft = 5f;
            pill.style.paddingRight = 7f;
            pill.style.paddingTop = 3f;
            pill.style.backgroundColor = new Color(0.16f, 0.10f, 0.065f, 0.96f);
            pill.style.borderBottomColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderLeftColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderRightColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderTopColor = new Color(0.55f, 0.36f, 0.16f, 0.85f);
            pill.style.borderBottomWidth = 1f;
            pill.style.borderLeftWidth = 1f;
            pill.style.borderRightWidth = 1f;
            pill.style.borderTopWidth = 1f;

            var icon = CreateSmallIcon(amount?.IconKey, amount?.Id);
            if (icon != null)
            {
                pill.Add(icon);
            }

            var text = CreateLabel($"{amount?.Label ?? string.Empty} x{Mathf.Max(0, amount?.Amount ?? 0)}", "management-panel-amount-label", "management-panel-amount-label");
            text.style.whiteSpace = WhiteSpace.NoWrap;
            pill.Add(text);
            return pill;
        }

        private static VisualElement CreateSmallIcon(string iconKey, string fallbackId)
        {
            var sprite = LoadIconSprite(
                iconKey,
                fallbackId,
                "Icons/Resources",
                "Icons/Points",
                "Icons/Units",
                "Icons/Recipes");
            var icon = new VisualElement { name = "management-panel-amount-icon" };
            icon.style.width = 22f;
            icon.style.height = 22f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 5f;
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
            }
            else
            {
                icon.style.backgroundColor = new Color(0.42f, 0.28f, 0.12f, 0.95f);
            }

            return icon;
        }

        public static void EnableDragScroll(ScrollView scroll)
        {
            if (scroll == null)
            {
                return;
            }

            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            if (scroll.userData is not DragScrollState)
            {
                scroll.userData = new DragScrollState();
            }

            if (scroll.userData is DragScrollState state)
            {
                state.HorizontalOnly = false;
            }

            scroll.UnregisterCallback<WheelEvent>(OnHorizontalWheelScroll);
            scroll.UnregisterCallback<PointerDownEvent>(OnDragScrollPointerDown, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerMoveEvent>(OnDragScrollPointerMove, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerUpEvent>(OnDragScrollPointerUp, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerCancelEvent>(OnDragScrollPointerCancel, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerDownEvent>(OnDragScrollPointerDown);
            scroll.UnregisterCallback<PointerMoveEvent>(OnDragScrollPointerMove);
            scroll.UnregisterCallback<PointerUpEvent>(OnDragScrollPointerUp);
            scroll.UnregisterCallback<PointerCancelEvent>(OnDragScrollPointerCancel);
            scroll.RegisterCallback<PointerDownEvent>(OnDragScrollPointerDown, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerMoveEvent>(OnDragScrollPointerMove, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerUpEvent>(OnDragScrollPointerUp, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerCancelEvent>(OnDragScrollPointerCancel, TrickleDown.TrickleDown);
        }

        public static void EnableHorizontalDragScroll(ScrollView scroll)
        {
            EnableDragScroll(scroll);
            if (scroll == null)
            {
                return;
            }

            if (scroll.userData is DragScrollState state)
            {
                state.HorizontalOnly = true;
            }

            scroll.UnregisterCallback<WheelEvent>(OnHorizontalWheelScroll);
            scroll.RegisterCallback<WheelEvent>(OnHorizontalWheelScroll);
        }

        public static void DisableDragScroll(ScrollView scroll)
        {
            if (scroll == null)
            {
                return;
            }

            scroll.UnregisterCallback<WheelEvent>(OnHorizontalWheelScroll);
            scroll.UnregisterCallback<PointerDownEvent>(OnDragScrollPointerDown);
            scroll.UnregisterCallback<PointerMoveEvent>(OnDragScrollPointerMove);
            scroll.UnregisterCallback<PointerUpEvent>(OnDragScrollPointerUp);
            scroll.UnregisterCallback<PointerCancelEvent>(OnDragScrollPointerCancel);
            scroll.UnregisterCallback<PointerDownEvent>(OnDragScrollPointerDown, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerMoveEvent>(OnDragScrollPointerMove, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerUpEvent>(OnDragScrollPointerUp, TrickleDown.TrickleDown);
            scroll.UnregisterCallback<PointerCancelEvent>(OnDragScrollPointerCancel, TrickleDown.TrickleDown);
            if (scroll.userData is DragScrollState)
            {
                scroll.userData = null;
            }
        }

        private sealed class DragScrollState
        {
            public bool Dragging;
            public bool HorizontalOnly;
            public Vector2 LastPosition;
            public bool Pressed;
            public Vector2 StartPosition;
        }

        private static void OnDragScrollPointerDown(PointerDownEvent evt)
        {
            if (evt.currentTarget is not ScrollView scroll || evt.button != 0)
            {
                return;
            }

            var state = scroll.userData as DragScrollState;
            if (state == null)
            {
                state = new DragScrollState();
                scroll.userData = state;
            }

            state.Dragging = false;
            state.Pressed = true;
            state.StartPosition = evt.position;
            state.LastPosition = evt.position;
        }

        private static void OnDragScrollPointerMove(PointerMoveEvent evt)
        {
            if (evt.currentTarget is not ScrollView scroll ||
                scroll.userData is not DragScrollState state ||
                !state.Pressed)
            {
                return;
            }

            var current = (Vector2)evt.position;
            if ((evt.pressedButtons & 1) == 0)
            {
                ReleaseDragScrollPointer(scroll, evt.pointerId);
                return;
            }

            var delta = current - state.LastPosition;
            if (!state.Dragging)
            {
                var total = current - state.StartPosition;
                if (state.HorizontalOnly && Mathf.Abs(total.x) < Mathf.Abs(total.y))
                {
                    ReleaseDragScrollPointer(scroll, evt.pointerId);
                    return;
                }

                var dragDistance = state.HorizontalOnly
                    ? Mathf.Abs(current.x - state.StartPosition.x)
                    : Vector2.Distance(current, state.StartPosition);
                if (dragDistance < 6f)
                {
                    return;
                }

                state.Dragging = true;
                scroll.CapturePointer(evt.pointerId);
            }

            state.LastPosition = current;
            if (state.HorizontalOnly)
            {
                delta.y = 0f;
            }

            scroll.scrollOffset -= delta;
            evt.StopPropagation();
        }

        private static void OnHorizontalWheelScroll(WheelEvent evt)
        {
            if (evt.currentTarget is not ScrollView scroll)
            {
                return;
            }

            var wheelDelta = Mathf.Abs(evt.delta.x) > Mathf.Abs(evt.delta.y)
                ? evt.delta.x
                : evt.delta.y;
            if (Mathf.Approximately(wheelDelta, 0f))
            {
                return;
            }

            scroll.scrollOffset = new Vector2(
                Mathf.Max(0f, scroll.scrollOffset.x + wheelDelta * 48f),
                scroll.scrollOffset.y);
            evt.StopPropagation();
        }

        private static void OnDragScrollPointerUp(PointerUpEvent evt)
        {
            var scroll = evt.currentTarget as ScrollView;
            var wasDragging = scroll?.userData is DragScrollState state && state.Dragging;
            ReleaseDragScrollPointer(scroll, evt.pointerId);
            if (wasDragging)
            {
                evt.StopPropagation();
            }
        }

        private static void OnDragScrollPointerCancel(PointerCancelEvent evt)
        {
            ReleaseDragScrollPointer(evt.currentTarget as ScrollView, evt.pointerId);
        }

        private static void ReleaseDragScrollPointer(ScrollView scroll, int pointerId)
        {
            if (scroll == null)
            {
                return;
            }

            if (scroll.userData is DragScrollState state)
            {
                state.Dragging = false;
                state.Pressed = false;
            }

            if (scroll.HasPointerCapture(pointerId))
            {
                scroll.ReleasePointer(pointerId);
            }
        }

        private static bool HasPrerequisites(ManagementPanelRowState row)
        {
            return HasEntries(row?.PrerequisiteIds);
        }

        private static bool IsTechTreeTitle(string title)
        {
            return string.Equals(title, "Tech Tree", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "科技树", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyTechTreeTitleStyle()
        {
            if (_title == null)
            {
                return;
            }

            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.fontSize = 30f;
            _title.style.marginBottom = 18f;
        }

        private void ConfigureTechTreeOuterScroll()
        {
            var scroll = _root?.Q<ScrollView>("management-panel-scroll");
            if (scroll == null)
            {
                return;
            }

            DisableDragScroll(scroll);
            scroll.scrollOffset = Vector2.zero;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        private void ConfigurePolicyFocusOuterScroll()
        {
            var scroll = _root?.Q<ScrollView>("management-panel-scroll");
            if (scroll == null)
            {
                return;
            }

            DisableDragScroll(scroll);
            scroll.scrollOffset = Vector2.zero;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        private static bool IsPolicyFocusTitle(string title)
        {
            return string.Equals(title, "Policy Focus", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, "国策", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyTitleStyle(string title)
        {
            if (_title == null)
            {
                return;
            }

            if (IsPolicyFocusTitle(title))
            {
                _title.style.fontSize = 28f;
                _title.style.unityFontStyleAndWeight = FontStyle.Bold;
                _title.style.unityTextAlign = TextAnchor.MiddleCenter;
                _title.style.alignSelf = Align.Auto;
                _title.style.marginBottom = 20f;
                _title.style.flexGrow = 1f;
                _title.style.width = StyleKeyword.Auto;
                return;
            }

            _title.style.fontSize = 16f;
            _title.style.unityTextAlign = TextAnchor.MiddleLeft;
            _title.style.alignSelf = Align.Auto;
            _title.style.flexGrow = 1f;
            _title.style.width = StyleKeyword.Auto;
        }

        private void ApplyCloseButtonStyle(string title)
        {
            if (_closeButton == null)
            {
                return;
            }

            if (IsPolicyFocusTitle(title))
            {
                _closeButton.style.width = 52f;
                _closeButton.style.height = 46f;
                _closeButton.style.flexShrink = 0f;
                _closeButton.style.marginLeft = 16f;
                _closeButton.style.marginRight = 0f;
                _closeButton.style.marginTop = 0f;
                _closeButton.style.marginBottom = 12f;
                _closeButton.style.fontSize = 24f;
                _closeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                _closeButton.style.backgroundColor = new Color(0.20f, 0.08f, 0.045f, 0.98f);
                _closeButton.style.color = new Color(1f, 0.86f, 0.62f, 1f);
                return;
            }

            _closeButton.style.width = 34f;
            _closeButton.style.height = 30f;
            _closeButton.style.marginLeft = 0f;
            _closeButton.style.marginBottom = 0f;
            _closeButton.style.fontSize = 12f;
        }

        private void ApplyPanelBackground(string title)
        {
            if (_root == null)
            {
                return;
            }

            var resource = ResolvePanelBackgroundResource(title);
            if (string.IsNullOrWhiteSpace(resource))
            {
                _root.style.backgroundImage = StyleKeyword.Null;
                return;
            }

            var sprite = Resources.Load<Sprite>(resource);
            if (sprite == null)
            {
                return;
            }

            _root.style.backgroundImage = new StyleBackground(sprite);
            _root.style.backgroundColor = new Color(0.035f, 0.026f, 0.022f, 0.92f);
        }

        private static string ResolvePanelBackgroundResource(string title)
        {
            if (IsTechTreeTitle(title))
            {
                return TechTreeBackgroundResource;
            }

            if (IsPolicyFocusTitle(title))
            {
                return PolicyFocusBackgroundResource;
            }

            return string.Empty;
        }

        private static IReadOnlyDictionary<string, string> BuildTitleById(IReadOnlyList<List<ManagementPanelRowState>> columns)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (columns == null)
            {
                return result;
            }

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var rows = columns[columnIndex];
                if (rows == null)
                {
                    continue;
                }

                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var row = rows[rowIndex];
                    var id = NormalizeId(row?.Id);
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    result[id] = row.Title;
                }
            }

            return result;
        }

        private static ISet<string> BuildDependentIds(IReadOnlyList<List<ManagementPanelRowState>> columns)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (columns == null)
            {
                return result;
            }

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var rows = columns[columnIndex];
                if (rows == null)
                {
                    continue;
                }

                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var prerequisites = rows[rowIndex]?.PrerequisiteIds;
                    if (prerequisites == null)
                    {
                        continue;
                    }

                    for (var i = 0; i < prerequisites.Count; i++)
                    {
                        var id = NormalizeId(prerequisites[i]);
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            result.Add(id);
                        }
                    }
                }
            }

            return result;
        }

        private static bool HasEntries(IReadOnlyCollection<string> values)
        {
            return values != null && values.Count > 0;
        }

        private static List<List<ManagementPanelRowState>> BuildTechColumns(ManagementPanelState state)
        {
            var allRows = new List<ManagementPanelRowState>();
            var byId = new Dictionary<string, ManagementPanelRowState>(StringComparer.Ordinal);
            if (state?.Groups != null)
            {
                for (var groupIndex = 0; groupIndex < state.Groups.Count; groupIndex++)
                {
                    var rows = state.Groups[groupIndex]?.Rows;
                    if (rows == null)
                    {
                        continue;
                    }

                    for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                    {
                        var row = rows[rowIndex];
                        if (row == null || string.IsNullOrWhiteSpace(row.Id))
                        {
                            continue;
                        }

                        allRows.Add(row);
                        byId[NormalizeId(row.Id)] = row;
                    }
                }
            }

            var depthCache = new Dictionary<string, int>(StringComparer.Ordinal);
            var columns = new List<List<ManagementPanelRowState>>();
            for (var i = 0; i < allRows.Count; i++)
            {
                var row = allRows[i];
                var depth = ResolveTechDepth(row, byId, depthCache, new HashSet<string>(StringComparer.Ordinal));
                while (columns.Count <= depth)
                {
                    columns.Add(new List<ManagementPanelRowState>());
                }

                columns[depth].Add(row);
            }

            return columns;
        }

        private static int ResolveTechDepth(
            ManagementPanelRowState row,
            IReadOnlyDictionary<string, ManagementPanelRowState> byId,
            IDictionary<string, int> depthCache,
            ISet<string> visiting)
        {
            var id = NormalizeId(row?.Id);
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            if (depthCache.TryGetValue(id, out var cached))
            {
                return cached;
            }

            if (!visiting.Add(id))
            {
                return 0;
            }

            var depth = 0;
            var prerequisites = row.PrerequisiteIds;
            if (prerequisites != null)
            {
                for (var i = 0; i < prerequisites.Count; i++)
                {
                    var prerequisiteId = NormalizeId(prerequisites[i]);
                    if (string.IsNullOrEmpty(prerequisiteId) || !byId.TryGetValue(prerequisiteId, out var prerequisite))
                    {
                        continue;
                    }

                    depth = Mathf.Max(depth, ResolveTechDepth(prerequisite, byId, depthCache, visiting) + 1);
                }
            }

            visiting.Remove(id);
            depthCache[id] = depth;
            return depth;
        }

        private static VisualElement CreateIcon(ManagementPanelRowState row)
        {
            if (row == null)
            {
                return null;
            }

            var sprite = LoadIconSprite(
                row.IconKey,
                row.Id,
                "Icons/Policies",
                "Icons/Policy",
                "Icons/Tech",
                "Icons/Recipes",
                "Icons/Buildings",
                "Icons/Units",
                "Icons/Resources",
                "Icons/Points");
            var icon = new VisualElement { name = "management-panel-row-icon" };
            icon.AddToClassList("management-panel-row-icon");
            icon.style.width = 58f;
            icon.style.height = 58f;
            icon.style.flexShrink = 0f;
            icon.style.marginRight = 12f;
            if (sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
                return icon;
            }

            icon.style.backgroundColor = new Color(0.22f, 0.12f, 0.08f, 0.95f);
            icon.style.borderBottomColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
            icon.style.borderLeftColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
            icon.style.borderRightColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
            icon.style.borderTopColor = new Color(0.95f, 0.72f, 0.36f, 0.9f);
            icon.style.borderBottomWidth = 1f;
            icon.style.borderLeftWidth = 1f;
            icon.style.borderRightWidth = 1f;
            icon.style.borderTopWidth = 1f;
            icon.style.alignItems = Align.Center;
            icon.style.justifyContent = Justify.Center;
            var glyph = CreateLabel(ResolveInitials(row.Title), "management-panel-row-icon-fallback", "management-panel-row-icon-fallback");
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.color = new Color(1f, 0.86f, 0.58f, 1f);
            icon.Add(glyph);
            return icon;
        }

        public static Sprite LoadIconSprite(string iconKey, string fallbackId, params string[] roots)
        {
            var key = NormalizeIconKey(iconKey);
            var fallback = NormalizeIconKey(fallbackId);
            var sprite = LoadIconSpriteByKey(key, roots);
            return sprite != null ? sprite : LoadIconSpriteByKey(fallback, roots);
        }

        private static Sprite LoadIconSpriteByKey(string key, params string[] roots)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var direct = Resources.Load<Sprite>(key);
            if (direct != null)
            {
                return direct;
            }

            if (roots == null)
            {
                return null;
            }

            for (var i = 0; i < roots.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(roots[i]))
                {
                    continue;
                }

                var sprite = Resources.Load<Sprite>($"{roots[i].TrimEnd('/')}/{key}");
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static Label CreateLabel(string text, string name, string className)
        {
            var label = new Label(text ?? string.Empty) { name = name };
            if (!string.IsNullOrWhiteSpace(className))
            {
                label.AddToClassList(className);
            }

            label.style.color = new Color(0.96f, 0.88f, 0.74f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');
        }

        private static string NormalizeIconKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private static string ResolveInitials(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= 2 ? trimmed : trimmed.Substring(0, 2).ToUpperInvariant();
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
