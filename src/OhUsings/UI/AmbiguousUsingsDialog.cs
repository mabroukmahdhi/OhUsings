using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using OhUsings.Commands;
using OhUsings.Models;

namespace OhUsings.UI
{
    internal sealed class AmbiguousUsingsDialog : DialogWindow
    {
        private readonly List<AmbiguousItemRow> _rows = new List<AmbiguousItemRow>();
        private readonly ComboBox _scopeCombo;

        public bool Applied { get; private set; }

        public ImportScope SelectedScope =>
            (ImportScope)_scopeCombo.SelectedIndex;

        public AmbiguousUsingsDialog(
            IReadOnlyList<AmbiguousImport> ambiguousImports,
            ImportScope currentScope)
        {
            Title = "OhUsings \u2013 Resolve Ambiguous Usings";
            Width = 580;
            Height = 420;
            MinWidth = 500;
            MinHeight = 300;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new TextBlock
            {
                Text = "The following types resolved to multiple namespaces. " +
                       "Select the namespace you want for each, or leave \u201c(skip)\u201d to ignore.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // Scrollable list of ambiguous items
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(scrollViewer, 1);

            var itemsPanel = new StackPanel();
            foreach (var import in ambiguousImports)
            {
                var row = new AmbiguousItemRow(import);
                _rows.Add(row);
                itemsPanel.Children.Add(row.Panel);
            }
            scrollViewer.Content = itemsPanel;
            root.Children.Add(scrollViewer);

            // Scope selector
            var scopePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };
            scopePanel.Children.Add(new TextBlock
            {
                Text = "Apply to:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            _scopeCombo = new ComboBox { Width = 180 };
            _scopeCombo.Items.Add("Current File");
            _scopeCombo.Items.Add("Current Project");
            _scopeCombo.Items.Add("Solution");
            _scopeCombo.SelectedIndex = (int)currentScope;
            scopePanel.Children.Add(_scopeCombo);

            Grid.SetRow(scopePanel, 2);
            root.Children.Add(scopePanel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var applyButton = new Button
            {
                Content = "Apply",
                Width = 80,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            applyButton.Click += (s, e) =>
            {
                Applied = true;
                Close();
            };

            var skipButton = new Button
            {
                Content = "Skip All",
                Width = 80,
                IsCancel = true
            };
            skipButton.Click += (s, e) =>
            {
                Applied = false;
                Close();
            };

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(skipButton);
            Grid.SetRow(buttonPanel, 3);
            root.Children.Add(buttonPanel);

            Content = root;
        }

        public IReadOnlyList<ResolvedImport> GetResolvedImports()
        {
            return _rows
                .Where(r => r.SelectedNamespace != null && r.SelectedNamespace != "(skip)")
                .Select(r => new ResolvedImport(r.TypeName, r.SelectedNamespace))
                .ToList();
        }

        private sealed class AmbiguousItemRow
        {
            private readonly ComboBox _combo;

            public string TypeName { get; }
            public StackPanel Panel { get; }

            public string SelectedNamespace =>
                _combo.SelectedItem as string ?? "(skip)";

            public AmbiguousItemRow(AmbiguousImport import)
            {
                TypeName = import.TypeName;

                Panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 2, 0, 2)
                };

                Panel.Children.Add(new TextBlock
                {
                    Text = import.TypeName,
                    Width = 170,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                });

                _combo = new ComboBox { MinWidth = 320 };
                _combo.Items.Add("(skip)");
                foreach (var ns in import.CandidateNamespaces)
                {
                    _combo.Items.Add(ns);
                }
                _combo.SelectedIndex = 0;
                Panel.Children.Add(_combo);
            }
        }
    }
}
