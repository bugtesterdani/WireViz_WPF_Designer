using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text;

namespace WireVizWPF
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private UIElement draggedElement = null;
        private Point offset;
        private bool isConnectMode = false;
        private UIElement startPin = null;
        private Line currentLine = null;
        private List<(UIElement startPin, UIElement endPin, Line line, bool isShield)> connections = new List<(UIElement, UIElement, Line, bool)>();
        private List<Element> elements = new List<Element>();
        private List<BomItem> bomItems = new List<BomItem>();
        private Dictionary<string, string> metadata = new Dictionary<string, string>();
        private HarnessOptions options = new HarnessOptions();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddConnector_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateConnectorDialog($"Connector_{elements.Count + 1}", "D-Sub", "female", "1,2", PinSide.Right);
            dialog.ShowDialog();
            if (dialog.Tag is ConnectorProperties props)
            {
                AddConnector(props);
            }
        }

        private void AddCable_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateCableDialog($"Cable_{elements.Count + 1}", "0.25 mm2", "2", "DIN", "1,2", false);
            dialog.ShowDialog();
            if (dialog.Tag is CableProperties props)
            {
                AddCable(props);
            }
        }

        private void AddNode_Click(object sender, RoutedEventArgs e)
        {
            var dialog = CreateNodeDialog($"Node_{elements.Count + 1}", "Splice");
            dialog.ShowDialog();
            if (dialog.Tag is NodeProperties props)
            {
                AddNode(props);
            }
        }

        private void EditProperties_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window { Title = "Select Element", Width = 300, Height = 150 };
            var comboBox = new ComboBox { ItemsSource = elements.Select(x => x.Name).ToList(), Margin = new Thickness(5) };
            var okButton = new Button { Content = "Edit", Margin = new Thickness(5) };
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(okButton);
            dialog.Content = stackPanel;

            okButton.Click += (s, e) => { dialog.Tag = comboBox.SelectedItem; dialog.Close(); };
            dialog.ShowDialog();

            if (dialog.Tag is string selectedName)
            {
                var element = elements.Find(x => x.Name == selectedName);
                if (element.Type == "Connector")
                {
                    var connDialog = CreateConnectorDialog(element.Name, element.ConnectorProps?.Type ?? "", element.ConnectorProps?.Subtype ?? "", string.Join(",", element.Pins.Select(p => p.label)), element.ConnectorProps?.PinSide ?? PinSide.Right);
                    connDialog.ShowDialog();
                    if (connDialog.Tag is ConnectorProperties props)
                    {
                        UpdateConnector(element, props);
                    }
                }
                else if (element.Type == "Cable")
                {
                    var cableDialog = CreateCableDialog(element.Name, element.CableProps?.Gauge ?? "", element.CableProps?.Length.ToString() ?? "", element.CableProps?.ColorCode ?? "", string.Join(",", element.Pins.Where(p => p.pinName != "s").Select(p => p.label)), element.CableProps?.Shield ?? false);
                    cableDialog.ShowDialog();
                    if (cableDialog.Tag is CableProperties props)
                    {
                        UpdateCable(element, props);
                    }
                }
                else if (element.Type == "Node")
                {
                    var nodeDialog = CreateNodeDialog(element.Name, element.NodeProps?.Type ?? "");
                    nodeDialog.ShowDialog();
                    if (nodeDialog.Tag is NodeProperties props)
                    {
                        UpdateNode(element, props);
                    }
                }
            }
        }

        private void AddBomItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window { Title = "Add BOM Item", Width = 300, Height = 200 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var descLabel = new Label { Content = "Description:" };
            var descText = new TextBox { Text = "" };
            var qtyLabel = new Label { Content = "Quantity:" };
            var qtyText = new TextBox { Text = "1" };
            var unitLabel = new Label { Content = "Unit:" };
            var unitText = new TextBox { Text = "" };
            var okButton = new Button { Content = "OK", Margin = new Thickness(5) };
            Grid.SetRow(descLabel, 0); Grid.SetColumn(descLabel, 0);
            Grid.SetRow(descText, 0); Grid.SetColumn(descText, 1);
            Grid.SetRow(qtyLabel, 1); Grid.SetColumn(qtyLabel, 0);
            Grid.SetRow(qtyText, 1); Grid.SetColumn(qtyText, 1);
            Grid.SetRow(unitLabel, 2); Grid.SetColumn(unitLabel, 0);
            Grid.SetRow(unitText, 2); Grid.SetColumn(unitText, 1);
            Grid.SetRow(okButton, 3); Grid.SetColumn(okButton, 1);
            grid.Children.Add(descLabel); grid.Children.Add(descText);
            grid.Children.Add(qtyLabel); grid.Children.Add(qtyText);
            grid.Children.Add(unitLabel); grid.Children.Add(unitText);
            grid.Children.Add(okButton);
            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                if (int.TryParse(qtyText.Text, out int qty))
                {
                    dialog.Tag = new BomItem { Description = descText.Text, Quantity = qty, Unit = unitText.Text };
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show("Invalid quantity. Please enter a number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            dialog.ShowDialog();

            if (dialog.Tag is BomItem item)
            {
                bomItems.Add(item);
            }
        }

        private void EditMetadata_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window { Title = "Edit Metadata", Width = 300, Height = 200 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var titleLabel = new Label { Content = "Title:" };
            var titleText = new TextBox { Text = metadata.GetValueOrDefault("title", "") };
            var descLabel = new Label { Content = "Description:" };
            var descText = new TextBox { Text = metadata.GetValueOrDefault("description", "") };
            var notesLabel = new Label { Content = "Notes:" };
            var notesText = new TextBox { Text = metadata.GetValueOrDefault("notes", "") };
            var okButton = new Button { Content = "OK", Margin = new Thickness(5) };
            Grid.SetRow(titleLabel, 0); Grid.SetColumn(titleLabel, 0);
            Grid.SetRow(titleText, 0); Grid.SetColumn(titleText, 1);
            Grid.SetRow(descLabel, 1); Grid.SetColumn(descLabel, 0);
            Grid.SetRow(descText, 1); Grid.SetColumn(descText, 1);
            Grid.SetRow(notesLabel, 2); Grid.SetColumn(notesLabel, 0);
            Grid.SetRow(notesText, 2); Grid.SetColumn(notesText, 1);
            Grid.SetRow(okButton, 3); Grid.SetColumn(okButton, 1);
            grid.Children.Add(titleLabel); grid.Children.Add(titleText);
            grid.Children.Add(descLabel); grid.Children.Add(descText);
            grid.Children.Add(notesLabel); grid.Children.Add(notesText);
            grid.Children.Add(okButton);
            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                metadata["title"] = titleText.Text;
                metadata["description"] = descText.Text;
                metadata["notes"] = notesText.Text;
                dialog.Close();
            };
            dialog.ShowDialog();
        }

        private void EditOptions_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window { Title = "Edit Options", Width = 300, Height = 300 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var showNameCheck = new CheckBox { Content = "Show Name", IsChecked = options.ShowName };
            var bgcolorLabel = new Label { Content = "Background Color:" };
            var bgcolorText = new TextBox { Text = options.BgColor ?? "" };
            var fontLabel = new Label { Content = "Font Name:" };
            var fontText = new TextBox { Text = options.FontName ?? "" };
            var templateSepLabel = new Label { Content = "Template Separator:" };
            var templateSepText = new TextBox { Text = options.TemplateSeparator ?? "." };
            var okButton = new Button { Content = "OK", Margin = new Thickness(5) };
            Grid.SetRow(showNameCheck, 0); Grid.SetColumn(showNameCheck, 0); Grid.SetColumnSpan(showNameCheck, 2);
            Grid.SetRow(bgcolorLabel, 1); Grid.SetColumn(bgcolorLabel, 0);
            Grid.SetRow(bgcolorText, 1); Grid.SetColumn(bgcolorText, 1);
            Grid.SetRow(fontLabel, 2); Grid.SetColumn(fontLabel, 0);
            Grid.SetRow(fontText, 2); Grid.SetColumn(fontText, 1);
            Grid.SetRow(templateSepLabel, 3); Grid.SetColumn(templateSepLabel, 0);
            Grid.SetRow(templateSepText, 3); Grid.SetColumn(templateSepText, 1);
            Grid.SetRow(okButton, 4); Grid.SetColumn(okButton, 1);
            grid.Children.Add(showNameCheck); grid.Children.Add(bgcolorLabel); grid.Children.Add(bgcolorText);
            grid.Children.Add(fontLabel); grid.Children.Add(fontText);
            grid.Children.Add(templateSepLabel); grid.Children.Add(templateSepText);
            grid.Children.Add(okButton);
            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                options.ShowName = showNameCheck.IsChecked == true;
                options.BgColor = bgcolorText.Text;
                options.FontName = fontText.Text;
                options.TemplateSeparator = templateSepText.Text;
                dialog.Close();
            };
            dialog.ShowDialog();
        }

        private Window CreateConnectorDialog(string defaultName, string defaultType, string defaultSubtype, string defaultPins, PinSide defaultPinSide)
        {
            var dialog = new Window { Title = "Connector Properties", Width = 600, Height = 450 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var nameLabel = new Label { Content = "Name:" };
            var nameText = new TextBox { Text = defaultName };
            var typeLabel = new Label { Content = "Type (e.g., D-Sub):" };
            var typeText = new TextBox { Text = defaultType };
            var subtypeLabel = new Label { Content = "Subtype (e.g., female):" };
            var subtypeText = new TextBox { Text = defaultSubtype };
            var pinsLabel = new Label { Content = "Pin Labels (comma-separated, non-numeric, e.g., A,B):" };
            var pinsText = new TextBox { Text = defaultPins };
            var colorLabel = new Label { Content = "Color:" };
            var colorText = new TextBox { Text = "" };
            var imageLabel = new Label { Content = "Image Path:" };
            var imageText = new TextBox { Text = "" };
            var pinSideLabel = new Label { Content = "Pin Side:" };
            var pinSideCombo = new ComboBox { ItemsSource = new[] { "Right", "Left" }, SelectedItem = defaultPinSide.ToString() };
            var showNameCheck = new CheckBox { Content = "Show Name", IsChecked = false };
            var okButton = new Button { Content = "OK", Margin = new Thickness(5) };
            Grid.SetRow(nameLabel, 0); Grid.SetColumn(nameLabel, 0);
            Grid.SetRow(nameText, 0); Grid.SetColumn(nameText, 1);
            Grid.SetRow(typeLabel, 1); Grid.SetColumn(typeLabel, 0);
            Grid.SetRow(typeText, 1); Grid.SetColumn(typeText, 1);
            Grid.SetRow(subtypeLabel, 2); Grid.SetColumn(subtypeLabel, 0);
            Grid.SetRow(subtypeText, 2); Grid.SetColumn(subtypeText, 1);
            Grid.SetRow(pinsLabel, 3); Grid.SetColumn(pinsLabel, 0);
            Grid.SetRow(pinsText, 3); Grid.SetColumn(pinsText, 1);
            Grid.SetRow(colorLabel, 4); Grid.SetColumn(colorLabel, 0);
            Grid.SetRow(colorText, 4); Grid.SetColumn(colorText, 1);
            Grid.SetRow(imageLabel, 5); Grid.SetColumn(imageLabel, 0);
            Grid.SetRow(imageText, 5); Grid.SetColumn(imageText, 1);
            Grid.SetRow(pinSideLabel, 6); Grid.SetColumn(pinSideLabel, 0);
            Grid.SetRow(pinSideCombo, 6); Grid.SetColumn(pinSideCombo, 1);
            Grid.SetRow(showNameCheck, 7); Grid.SetColumn(showNameCheck, 0);
            Grid.SetRow(okButton, 8); Grid.SetColumn(okButton, 1);
            grid.Children.Add(nameLabel); grid.Children.Add(nameText);
            grid.Children.Add(typeLabel); grid.Children.Add(typeText);
            grid.Children.Add(subtypeLabel); grid.Children.Add(subtypeText);
            grid.Children.Add(pinsLabel); grid.Children.Add(pinsText);
            grid.Children.Add(colorLabel); grid.Children.Add(colorText);
            grid.Children.Add(imageLabel); grid.Children.Add(imageText);
            grid.Children.Add(pinSideLabel); grid.Children.Add(pinSideCombo);
            grid.Children.Add(showNameCheck); grid.Children.Add(okButton);
            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameText.Text))
                {
                    MessageBox.Show("Name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(pinsText.Text))
                {
                    MessageBox.Show("Pin labels cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var pinLabels = pinsText.Text.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                if (!pinLabels.Any())
                {
                    MessageBox.Show("At least one valid pin label is required.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (pinLabels.Any(p => p.All(char.IsDigit)))
                {
                    MessageBox.Show("Numeric pin labels are not allowed. Use non-numeric labels (e.g., A,B).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                dialog.Tag = new ConnectorProperties
                {
                    Name = nameText.Text,
                    Type = typeText.Text,
                    Subtype = subtypeText.Text,
                    PinLabels = pinLabels,
                    Color = colorText.Text,
                    Image = string.IsNullOrEmpty(imageText.Text) ? null : new ImageProperties { Src = imageText.Text },
                    ShowName = showNameCheck.IsChecked == true,
                    PinSide = pinSideCombo.SelectedItem.ToString() == "Left" ? PinSide.Left : PinSide.Right
                };
                Console.WriteLine($"Connector created: Name={nameText.Text}, Pins={pinsText.Text}");
                dialog.Close();
            };
            return dialog;
        }

        private Window CreateCableDialog(string defaultName, string defaultGauge, string defaultLength, string defaultColorCode, string defaultWireLabels, bool defaultShield)
        {
            var dialog = new Window { Title = "Cable Properties", Width = 600, Height = 450 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var nameLabel = new Label { Content = "Name:" };
            var nameText = new TextBox { Text = defaultName };
            var gaugeLabel = new Label { Content = "Gauge (e.g., 0.25 mm2):" };
            var gaugeText = new TextBox { Text = defaultGauge };
            var lengthLabel = new Label { Content = "Length (m):" };
            var lengthText = new TextBox { Text = defaultLength };
            var colorCodeLabel = new Label { Content = "Color Code (e.g., DIN):" };
            var colorCodeText = new TextBox { Text = defaultColorCode };
            var wireLabelsLabel = new Label { Content = "Wire Labels (comma-separated, non-numeric, e.g., Wire1,Wire2):" };
            var wireLabelsText = new TextBox { Text = defaultWireLabels };
            var colorsLabel = new Label { Content = "Wire Colors (comma-separated, optional):" };
            var colorsText = new TextBox { Text = "" };
            var shieldCheck = new CheckBox { Content = "Shield", IsChecked = defaultShield };
            var okButton = new Button { Content = "OK", Margin = new Thickness(5) };
            Grid.SetRow(nameLabel, 0); Grid.SetColumn(nameLabel, 0);
            Grid.SetRow(nameText, 0); Grid.SetColumn(nameText, 1);
            Grid.SetRow(gaugeLabel, 1); Grid.SetColumn(gaugeLabel, 0);
            Grid.SetRow(gaugeText, 1); Grid.SetColumn(gaugeText, 1);
            Grid.SetRow(lengthLabel, 2); Grid.SetColumn(lengthLabel, 0);
            Grid.SetRow(lengthText, 2); Grid.SetColumn(lengthText, 1);
            Grid.SetRow(colorCodeLabel, 3); Grid.SetColumn(colorCodeLabel, 0);
            Grid.SetRow(colorCodeText, 3); Grid.SetColumn(colorCodeText, 1);
            Grid.SetRow(wireLabelsLabel, 4); Grid.SetColumn(wireLabelsLabel, 0);
            Grid.SetRow(wireLabelsText, 4); Grid.SetColumn(wireLabelsText, 1);
            Grid.SetRow(colorsLabel, 5); Grid.SetColumn(colorsLabel, 0);
            Grid.SetRow(colorsText, 5); Grid.SetColumn(colorsText, 1);
            Grid.SetRow(shieldCheck, 6); Grid.SetColumn(shieldCheck, 0);
            Grid.SetRow(okButton, 7); Grid.SetColumn(okButton, 1);
            grid.Children.Add(nameLabel); grid.Children.Add(nameText);
            grid.Children.Add(gaugeLabel); grid.Children.Add(gaugeText);
            grid.Children.Add(lengthLabel); grid.Children.Add(lengthText);
            grid.Children.Add(colorCodeLabel); grid.Children.Add(colorCodeText);
            grid.Children.Add(wireLabelsLabel); grid.Children.Add(wireLabelsText);
            grid.Children.Add(colorsLabel); grid.Children.Add(colorsText);
            grid.Children.Add(shieldCheck); grid.Children.Add(okButton);
            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameText.Text))
                {
                    MessageBox.Show("Name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!double.TryParse(lengthText.Text, out double length))
                {
                    MessageBox.Show("Invalid length. Please enter a number.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (string.IsNullOrWhiteSpace(wireLabelsText.Text))
                {
                    MessageBox.Show("Wire labels cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var wireLabels = wireLabelsText.Text.Split(',').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                if (!wireLabels.Any())
                {
                    MessageBox.Show("At least one valid wire label is required.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (wireLabels.Any(l => l.All(char.IsDigit)))
                {
                    MessageBox.Show("Numeric wire labels are not allowed. Use non-numeric labels (e.g., Wire1,Wire2).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                dialog.Tag = new CableProperties
                {
                    Name = nameText.Text,
                    Gauge = gaugeText.Text,
                    Length = length,
                    ColorCode = colorCodeText.Text,
                    WireCount = wireLabels.Count,
                    WireLabels = wireLabels,
                    Colors = colorsText.Text.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList(),
                    Shield = shieldCheck.IsChecked == true
                };
                Console.WriteLine($"Cable created: Name={nameText.Text}, WireLabels={wireLabelsText.Text}, WireCount={wireLabels.Count}");
                dialog.Close();
            };
            return dialog;
        }

        private Window CreateNodeDialog(string defaultName, string defaultType)
        {
            var dialog = new Window { Title = "Node Properties", Width = 600, Height = 200 };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var nameLabel = new Label { Content = "Name:" };
            var nameText = new TextBox { Text = defaultName };
            var typeLabel = new Label { Content = "Type (e.g., Splice):" };
            var typeText = new TextBox { Text = defaultType };
            var okButton = new Button { Content = "OK", Margin = new Thickness(5) };
            Grid.SetRow(nameLabel, 0); Grid.SetColumn(nameLabel, 0);
            Grid.SetRow(nameText, 0); Grid.SetColumn(nameText, 1);
            Grid.SetRow(typeLabel, 1); Grid.SetColumn(typeLabel, 0);
            Grid.SetRow(typeText, 1); Grid.SetColumn(typeText, 1);
            Grid.SetRow(okButton, 2); Grid.SetColumn(okButton, 1);
            grid.Children.Add(nameLabel); grid.Children.Add(nameText);
            grid.Children.Add(typeLabel); grid.Children.Add(typeText);
            grid.Children.Add(okButton);
            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameText.Text))
                {
                    MessageBox.Show("Name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                dialog.Tag = new NodeProperties { Name = nameText.Text, Type = typeText.Text };
                dialog.Close();
            };
            return dialog;
        }

        private void UpdateConnector(Element oldElement, ConnectorProperties props)
        {
            // Entferne alte Pins und UI-Element von der Canvas
            foreach (var (pin, _, _) in oldElement.Pins)
            {
                MainCanvas.Children.Remove(pin);
            }
            MainCanvas.Children.Remove(oldElement.UIElement);

            // Entferne das alte Element aus der elements-Liste
            elements.Remove(oldElement);
            Console.WriteLine($"Removed old connector: {oldElement.Name}");

            // Füge das neue Element hinzu
            AddConnector(props);
            UpdateConnectionsAfterElementChange(oldElement.UIElement);
            Console.WriteLine($"Updated connector: {props.Name}");
        }

        private void UpdateCable(Element oldElement, CableProperties props)
        {
            // Entferne alte Pins und UI-Element von der Canvas
            foreach (var (pin, _, _) in oldElement.Pins)
            {
                MainCanvas.Children.Remove(pin);
            }
            MainCanvas.Children.Remove(oldElement.UIElement);

            // Entferne das alte Element aus der elements-Liste
            elements.Remove(oldElement);
            Console.WriteLine($"Removed old cable: {oldElement.Name}");

            // Füge das neue Element hinzu
            AddCable(props);
            UpdateConnectionsAfterElementChange(oldElement.UIElement);
            Console.WriteLine($"Updated cable: {props.Name}");
        }

        private void UpdateNode(Element oldElement, NodeProperties props)
        {
            // Entferne alte Pins und UI-Element von der Canvas
            foreach (var (pin, _, _) in oldElement.Pins)
            {
                MainCanvas.Children.Remove(pin);
            }
            MainCanvas.Children.Remove(oldElement.UIElement);

            // Entferne das alte Element aus der elements-Liste
            elements.Remove(oldElement);
            Console.WriteLine($"Removed old node: {oldElement.Name}");

            // Füge das neue Element hinzu
            AddNode(props);
            UpdateConnectionsAfterElementChange(oldElement.UIElement);
            Console.WriteLine($"Updated node: {props.Name}");
        }

        private void UpdateConnectionsAfterElementChange(UIElement oldElement)
        {
            var connectionsToRemove = connections.Where(c => elements.Find(e => e.Pins.Exists(p => p.pin == c.startPin || p.pin == c.endPin))?.UIElement == oldElement).ToList();
            foreach (var conn in connectionsToRemove)
            {
                MainCanvas.Children.Remove(conn.line);
                connections.Remove(conn);
            }
        }

        private void ToggleConnectMode_Click(object sender, RoutedEventArgs e)
        {
            isConnectMode = !isConnectMode;
            ((Button)sender).Content = isConnectMode ? "Connect Mode (ON)" : "Connect Mode (OFF)";
            if (!isConnectMode)
            {
                if (currentLine != null)
                {
                    MainCanvas.Children.Remove(currentLine);
                    currentLine = null;
                }
                startPin = null;
            }
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(MainCanvas);
            Console.WriteLine($"Mouse down at ({mousePos.X}, {mousePos.Y})");

            if (!isConnectMode)
            {
                foreach (var element in elements)
                {
                    if (IsMouseOverElement(element.UIElement, mousePos))
                    {
                        isDragging = true;
                        draggedElement = element.UIElement;
                        offset = (Point)(mousePos - new Point(Canvas.GetLeft(element.UIElement), Canvas.GetTop(element.UIElement)));
                        e.Handled = true;
                        Console.WriteLine($"Started dragging element {element.Name}");
                        return;
                    }
                }
            }
            else if (isConnectMode && startPin == null)
            {
                foreach (var element in elements)
                {
                    foreach (var (pin, pinName, label) in element.Pins)
                    {
                        if (IsMouseOverPin(pin, mousePos))
                        {
                            startPin = pin;
                            currentLine = new Line
                            {
                                Stroke = Brushes.Red,
                                StrokeThickness = 3,
                                X1 = GetPinCenter(pin).X,
                                Y1 = GetPinCenter(pin).Y,
                                X2 = mousePos.X,
                                Y2 = mousePos.Y,
                                Tag = "ConnectionLine"
                            };
                            MainCanvas.Children.Add(currentLine);
                            e.Handled = true;
                            Console.WriteLine($"Started connection from {element.Name}:{pinName} ({label}) at ({GetPinCenter(pin).X}, {GetPinCenter(pin).Y})");
                            return;
                        }
                    }
                }
                Console.WriteLine("No start pin detected");
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && draggedElement != null)
            {
                Point mousePos = e.GetPosition(MainCanvas);
                Canvas.SetLeft(draggedElement, mousePos.X - offset.X);
                Canvas.SetTop(draggedElement, mousePos.Y - offset.Y);

                foreach (var element in elements)
                {
                    if (element.UIElement == draggedElement)
                    {
                        for (int i = 0; i < element.Pins.Count; i++)
                        {
                            var (pin, pinName, _) = element.Pins[i];
                            double pinX, pinY;
                            if (element.Type == "Connector")
                            {
                                pinX = element.ConnectorProps.PinSide == PinSide.Left ? -10 : 60;
                                pinY = 10 + i * 15;
                            }
                            else if (element.Type == "Cable")
                            {
                                pinX = i % 2 == 0 ? -10 : 60;
                                pinY = 10 + (i / 2) * 15;
                                if (pinName == "s") pinY += 15;
                            }
                            else // Node
                            {
                                pinX = 5;
                                pinY = 5;
                            }
                            Canvas.SetLeft(pin, Canvas.GetLeft(element.UIElement) + pinX);
                            Canvas.SetTop(pin, Canvas.GetTop(element.UIElement) + pinY);
                        }
                    }
                }

                foreach (var (startPin, endPin, line, _) in connections)
                {
                    line.X1 = GetPinCenter(startPin).X;
                    line.Y1 = GetPinCenter(startPin).Y;
                    line.X2 = GetPinCenter(endPin).X;
                    line.Y2 = GetPinCenter(endPin).Y;
                }
            }
            else if (isConnectMode && currentLine != null)
            {
                Point mousePos = e.GetPosition(MainCanvas);
                currentLine.X2 = mousePos.X;
                currentLine.Y2 = mousePos.Y;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(MainCanvas);
            Console.WriteLine($"Mouse up at ({mousePos.X}, {mousePos.Y})");

            isDragging = false;
            draggedElement = null;

            if (isConnectMode && currentLine != null && startPin != null)
            {
                foreach (var element in elements)
                {
                    foreach (var (pin, pinName, label) in element.Pins)
                    {
                        if (IsMouseOverPin(pin, mousePos) && pin != startPin)
                        {
                            bool isShield = pinName == "s" || elements.Find(x => x.Pins.Exists(p => p.pin == startPin))?.Pins.Find(p => p.pin == startPin).pinName == "s";
                            currentLine.Stroke = isShield ? Brushes.Yellow : Brushes.Black;
                            currentLine.X2 = GetPinCenter(pin).X;
                            currentLine.Y2 = GetPinCenter(pin).Y;
                            connections.Add((startPin, pin, currentLine, isShield));
                            Console.WriteLine($"Connection saved from {elements.Find(x => x.Pins.Exists(p => p.pin == startPin))?.Name}:{elements.Find(x => x.Pins.Exists(p => p.pin == startPin))?.Pins.Find(p => p.pin == startPin).pinName} to {element.Name}:{pinName} ({label})");
                            startPin = null;
                            currentLine = null;
                            e.Handled = true;
                            return;
                        }
                    }
                }
                MainCanvas.Children.Remove(currentLine);
                currentLine = null;
                startPin = null;
                Console.WriteLine("Connection attempt cancelled");
            }
        }

        private bool IsMouseOverElement(UIElement element, Point mousePos)
        {
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            double width = element is Grid rect ? rect.Width : ((Ellipse)element).Width;
            double height = element is Grid rect2 ? rect2.Height : ((Ellipse)element).Height;

            return mousePos.X >= left && mousePos.X <= left + width &&
                   mousePos.Y >= top && mousePos.Y <= top + height;
        }

        private bool IsMouseOverPin(UIElement pin, Point mousePos)
        {
            double left = Canvas.GetLeft(pin);
            double top = Canvas.GetTop(pin);
            double width = ((Ellipse)pin).Width;
            double height = ((Ellipse)pin).Height;
            double centerX = left + width / 2;
            double centerY = top + height / 2;
            double radius = width * 0.75;

            double distance = Math.Sqrt(Math.Pow(mousePos.X - centerX, 2) + Math.Pow(mousePos.Y - centerY, 2));
            bool isHit = distance <= radius;

            if (isHit)
            {
                Console.WriteLine($"Hit pin at ({left}, {top}) with mouse at ({mousePos.X}, {mousePos.Y})");
            }

            return isHit;
        }

        private Point GetPinCenter(UIElement pin)
        {
            double left = Canvas.GetLeft(pin);
            double top = Canvas.GetTop(pin);
            double width = ((Ellipse)pin).Width;
            double height = ((Ellipse)pin).Height;
            return new Point(left + width / 2, top + height / 2);
        }

        private void AddConnector(ConnectorProperties props)
        {
            if (elements.Any(e => e.Name == props.Name))
            {
                MessageBox.Show($"An element with name {props.Name} already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Grid g = new()
            {
                Width = 60,
                Height = 20 + props.PinLabels.Count * 15,
                Background = Brushes.Blue
            };
            Canvas.SetTop(g, 50);
            Canvas.SetLeft(g, 50);

            g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(5) });
            for (int i = 0; i < props.PinLabels.Count; i++)
                g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(12.5) });

            Rectangle element = new Rectangle
            {
                Fill = Brushes.Transparent,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Grid.SetRow(element, 0);
            Grid.SetRowSpan(element, props.PinLabels.Count + 2);
            g.Children.Add(element);

            List<(UIElement pin, string pinName, string label)> pins = new List<(UIElement, string, string)>();
            for (int i = 0; i < props.PinLabels.Count; i++)
            {
                Ellipse pin = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Gray,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = $"Pin_{props.PinLabels[i]}"
                };
                string pinName = props.PinLabels[i];
                double pinX = props.PinSide == PinSide.Left ? -10 : 60;
                Canvas.SetLeft(pin, Canvas.GetLeft(g) + pinX);
                Canvas.SetTop(pin, Canvas.GetTop(g) + 10 + i * 15);
                pins.Add((pin, pinName, props.PinLabels[i]));
                MainCanvas.Children.Add(pin);

                TextBlock tb = new()
                {
                    Text = props.PinLabels[i],
                    Background = Brushes.Transparent,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetRow(tb, i + 1);
                g.Children.Add(tb);

                Console.WriteLine($"Added connector pin: {props.Name}:{pinName}");
            }

            elements.Add(new Element
            {
                UIElement = g,
                Type = "Connector",
                Name = props.Name,
                ConnectorProps = props,
                Pins = pins
            });
            MainCanvas.Children.Add(g);
            Console.WriteLine($"Added connector: {props.Name}");
        }

        private void AddCable(CableProperties props)
        {
            if (elements.Any(e => e.Name == props.Name))
            {
                MessageBox.Show($"An element with name {props.Name} already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Grid g = new()
            {
                Width = 60,
                Height = 20 + props.WireCount * 15,
                Background = Brushes.Green
            };
            Canvas.SetLeft(g, 50);
            Canvas.SetTop(g, 50);

            g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(5) });
            for (int i = 0; i < props.WireCount; i++)
                g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(12.5) });

            Rectangle element = new Rectangle
            {
                Fill = Brushes.Transparent,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Grid.SetRow(element, 0);
            Grid.SetRowSpan(element, props.WireCount + 2);
            g.Children.Add(element);


            List<(UIElement pin, string pinName, string label)> wires = new List<(UIElement, string, string)>();
            for (int i = 0; i < (props.WireCount * 2); i++)
            {
                Ellipse wire = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Gray,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = $"Wire_{props.WireLabels[i / 2]}"
                };
                string wireName = ((i % 2 == 0) ? "i" : "o") + props.WireLabels[i / 2];
                Canvas.SetLeft(wire, Canvas.GetLeft(g) + (i % 2 == 0 ? -10 : 60));
                Canvas.SetTop(wire, Canvas.GetTop(g) + 10 + (i / 2) * 15);
                wires.Add((wire, wireName, props.Colors.ElementAtOrDefault((i / 2)) ?? wireName));
                MainCanvas.Children.Add(wire);

                if (i % 2 == 0)
                {
                    TextBlock tb = new()
                    {
                        Text = props.WireLabels[i / 2],
                        Background = Brushes.Transparent,
                        FontWeight = FontWeights.Bold,
                        FontSize = 14,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetRow(tb, (i / 2) + 1);
                    g.Children.Add(tb);
                }

                Console.WriteLine($"Added cable wire: {props.Name}:{wireName}");
            }

            if (props.Shield)
            {
                Ellipse shieldPin = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Yellow,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = "Shield"
                };
                Canvas.SetLeft(shieldPin, Canvas.GetLeft(g) + 60);
                Canvas.SetTop(shieldPin, Canvas.GetTop(g) + 10 + (props.WireCount / 2) * 15);
                wires.Add((shieldPin, "s", "Shield"));
                MainCanvas.Children.Add(shieldPin);
                Console.WriteLine($"Added cable shield: {props.Name}:s");
            }

            elements.Add(new Element
            {
                UIElement = g,
                Type = "Cable",
                Name = props.Name,
                CableProps = props,
                Pins = wires
            });
            MainCanvas.Children.Add(g);
            Console.WriteLine($"Added cable: {props.Name}");
        }

        private void AddNode(NodeProperties props)
        {
            if (elements.Any(e => e.Name == props.Name))
            {
                MessageBox.Show($"An element with name {props.Name} already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Ellipse element = new Ellipse
            {
                Width = 20,
                Height = 20,
                Fill = Brushes.Red,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Canvas.SetLeft(element, 50);
            Canvas.SetTop(element, 50);

            List<(UIElement pin, string pinName, string label)> pins = new List<(UIElement, string, string)>();
            Ellipse pin = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.Gray,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Tag = "Node_Pin"
            };
            Canvas.SetLeft(pin, Canvas.GetLeft(element) + 5);
            Canvas.SetTop(pin, Canvas.GetTop(element) + 5);
            pins.Add((pin, "1", "Node"));
            MainCanvas.Children.Add(pin);

            elements.Add(new Element
            {
                UIElement = element,
                Type = "Node",
                Name = props.Name,
                NodeProps = props,
                Pins = pins
            });
            MainCanvas.Children.Add(element);
            Console.WriteLine($"Added node: {props.Name}");
        }

        private void GenerateYaml_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder yaml = new StringBuilder();

            // Connectors
            yaml.AppendLine("connectors:");
            foreach (var element in elements.Where(x => x.Type == "Connector"))
            {
                yaml.AppendLine($"  {element.Name}:");
                yaml.AppendLine($"    type: {element.ConnectorProps?.Type}");
                if (!string.IsNullOrEmpty(element.ConnectorProps?.Subtype)) yaml.AppendLine($"    subtype: {element.ConnectorProps.Subtype}");
                if (element.Pins.Any()) yaml.AppendLine("    pinlabels:");
                foreach (var (_, _, label) in element.Pins)
                {
                    yaml.AppendLine($"      - {label}");
                }
                if (!string.IsNullOrEmpty(element.ConnectorProps?.Color)) yaml.AppendLine($"    color: {element.ConnectorProps.Color}");
                if (element.ConnectorProps?.Image != null)
                {
                    yaml.AppendLine("    image:");
                    yaml.AppendLine($"      src: {element.ConnectorProps.Image.Src}");
                    if (!string.IsNullOrEmpty(element.ConnectorProps.Image.Caption)) yaml.AppendLine($"      caption: {element.ConnectorProps.Image.Caption}");
                    if (!string.IsNullOrEmpty(element.ConnectorProps.Image.BgColor)) yaml.AppendLine($"      bgcolor: {element.ConnectorProps.Image.BgColor}");
                    if (element.ConnectorProps.Image.Width.HasValue) yaml.AppendLine($"      width: {element.ConnectorProps.Image.Width}");
                    if (element.ConnectorProps.Image.Height.HasValue) yaml.AppendLine($"      height: {element.ConnectorProps.Image.Height}");
                }
                if (element.ConnectorProps?.ShowName == true) yaml.AppendLine("    show_name: true");
            }

            // Cables
            yaml.AppendLine("cables:");
            foreach (var element in elements.Where(x => x.Type == "Cable"))
            {
                yaml.AppendLine($"  {element.Name}:");
                yaml.AppendLine($"    gauge: {element.CableProps?.Gauge}");
                yaml.AppendLine($"    length: {element.CableProps?.Length}");
                yaml.AppendLine($"    color_code: {element.CableProps?.ColorCode}");
                yaml.AppendLine($"    wirecount: {element.CableProps?.WireCount}");
                if (element.Pins.Any()) yaml.AppendLine("    wirelabels:");
                foreach (var label in element.CableProps?.WireLabels ?? new())
                {
                    yaml.AppendLine($"      - {label[1..]}");
                }
                if (element.CableProps?.Shield == true) yaml.AppendLine("    shield: true");
                if (element.CableProps?.Colors.Any() == true)
                {
                    yaml.AppendLine("    colors:");
                    foreach (var color in element.CableProps.Colors)
                    {
                        yaml.AppendLine($"      - {color}");
                    }
                }
            }

            // Nodes
            var nodeElements = elements.Where(x => x.Type == "Node").ToList();
            if (nodeElements.Any())
            {
                yaml.AppendLine("nodes:");
                foreach (var element in nodeElements)
                {
                    yaml.AppendLine($"  {element.Name}:");
                    yaml.AppendLine($"    type: {element.NodeProps?.Type}");
                }
            }

            // Connections
            yaml.AppendLine("connections:");
            var cables = elements.Where(x => x.Type == "Cable").ToList();
            foreach (var cable in cables)
            {
                // Find all connections involving this cable
                var cableConnections = connections
                    .Where(c =>
                    {
                        var startElement = elements.Find(e => e.Pins.Exists(p => p.pin == c.startPin));
                        var endElement = elements.Find(e => e.Pins.Exists(p => p.pin == c.endPin));
                        return (startElement?.Name == cable.Name || endElement?.Name == cable.Name) && !c.isShield;
                    })
                    .ToList();

                // Group connections by wire pairs
                var wireConnections = new List<(string leftConnector, string leftPin, string cablePin1, string cablePin2, string rightConnector, string rightPin)>();
                var processedPins = new HashSet<string>();

                foreach (var (startPin, endPin, _, _) in cableConnections)
                {
                    var startElement = elements.Find(x => x.Pins.Exists(p => p.pin == startPin));
                    var endElement = elements.Find(x => x.Pins.Exists(p => p.pin == endPin));
                    var startPinName = startElement?.Pins.Find(p => p.pin == startPin).pinName;
                    var endPinName = endElement?.Pins.Find(p => p.pin == endPin).pinName;

                    Console.WriteLine($"Processing connection: {startElement?.Name}:{startPinName} -> {endElement?.Name}:{endPinName}");

                    if (startElement == null || endElement == null || startPinName == null || endPinName == null)
                    {
                        Console.WriteLine($"Skipping invalid connection: Start={startElement?.Name}, End={endElement?.Name}");
                        continue;
                    }

                    if (processedPins.Contains(startPinName + endPinName))
                        continue;

                    // Handle connections where cable is the start element
                    if (startElement.Name == cable.Name)
                    {
                        // Find matching connection where cable is the end element
                        var relatedConnection = cableConnections
                            .FirstOrDefault(c =>
                            {
                                var otherStartElement = elements.Find(x => x.Pins.Exists(p => p.pin == c.startPin));
                                var otherEndElement = elements.Find(x => x.Pins.Exists(p => p.pin == c.endPin));
                                var otherendpinname = otherEndElement?.Pins.Find(p => p.pin == c.endPin).pinName;
                                var otherstartpinname = otherStartElement?.Pins.Find(p => p.pin == c.startPin).pinName;
                                return otherEndElement?.Name == cable.Name && c.endPin != endPin && !processedPins.Contains(otherstartpinname + otherendpinname);
                            });

                        if (relatedConnection != default)
                        {
                            var otherStartElement = elements.Find(x => x.Pins.Exists(p => p.pin == relatedConnection.startPin));
                            var otherEndElement = elements.Find(x => x.Pins.Exists(p => p.pin == relatedConnection.endPin));
                            var otherStartPinName = otherStartElement?.Pins.Find(p => p.pin == relatedConnection.startPin).pinName;
                            var otherEndPinName = otherEndElement?.Pins.Find(p => p.pin == relatedConnection.endPin).pinName;

                            if (otherStartElement != null && otherStartPinName != null && otherEndPinName != null)
                            {
                                wireConnections.Add((otherStartElement.Name, otherStartPinName, otherEndPinName, startPinName, endElement.Name, endPinName));
                                processedPins.Add(startPinName + endPinName);
                                processedPins.Add(otherStartPinName + otherEndPinName);
                                Console.WriteLine($"Matched connection: {otherStartElement.Name}:{otherStartPinName} -> {cable.Name}:{otherEndPinName} -> {cable.Name}:{startPinName} -> {endElement.Name}:{endPinName}");
                            }
                        }
                    }
                    // Handle connections where cable is the end element
                    else if (endElement.Name == cable.Name)
                    {
                        // Find matching connection where cable is the start element
                        var relatedConnection = cableConnections
                            .FirstOrDefault(c =>
                            {
                                var otherStartElement = elements.Find(x => x.Pins.Exists(p => p.pin == c.startPin));
                                var otherEndElement = elements.Find(x => x.Pins.Exists(p => p.pin == c.endPin));
                                var otherendpinname = otherEndElement?.Pins.Find(p => p.pin == c.endPin).pinName;
                                var otherstartpinname = otherStartElement?.Pins.Find(p => p.pin == c.startPin).pinName;
                                return otherStartElement?.Name == cable.Name && c.startPin != startPin && !processedPins.Contains(otherstartpinname + otherendpinname);
                            });

                        if (relatedConnection != default)
                        {
                            var otherStartElement = elements.Find(x => x.Pins.Exists(p => p.pin == relatedConnection.startPin));
                            var otherEndElement = elements.Find(x => x.Pins.Exists(p => p.pin == relatedConnection.endPin));
                            var otherStartPinName = otherStartElement?.Pins.Find(p => p.pin == relatedConnection.startPin).pinName;
                            var otherEndPinName = otherEndElement?.Pins.Find(p => p.pin == relatedConnection.endPin).pinName;

                            if (otherEndElement != null && otherStartPinName != null && otherEndPinName != null)
                            {
                                wireConnections.Add((startElement.Name, startPinName, endPinName, otherStartPinName, otherEndElement.Name, otherEndPinName));
                                processedPins.Add(startPinName + endPinName);
                                processedPins.Add(otherStartPinName + otherEndPinName);
                                Console.WriteLine($"Matched connection: {startElement.Name}:{startPinName} -> {cable.Name}:{endPinName} -> {cable.Name}:{otherStartPinName} -> {otherEndElement.Name}:{otherEndPinName}");
                            }
                        }
                    }
                }

                // Output valid wire connections
                foreach (var (leftConnector, leftPin, cablePin1, cablePin2, rightConnector, rightPin) in wireConnections)
                {
                    yaml.AppendLine("  -");
                    yaml.AppendLine($"    - {leftConnector}: [{leftPin}]");
                    yaml.AppendLine($"    - {cable.Name}: [{cablePin1[1..]}]");
                    yaml.AppendLine($"    - {rightConnector}: [{rightPin}]");
                    Console.WriteLine($"Adding connection chain: {leftConnector}:[{leftPin}] -> {cable.Name}:[{cablePin1}] -> {cable.Name}:[{cablePin2}] -> {rightConnector}:[{rightPin}]");
                }

                //List<(string, List<string>, List<string>, List<string>, string, List<string>)> _wireConnections =
                //    combineWireConnections(wireConnections);

                //// Output valid wire connections
                //foreach (var (leftConnector, leftPin, cablePin1, cablePin2, rightConnector, rightPin) in _wireConnections)
                //{
                //    yaml.AppendLine("  -");
                //    yaml.AppendLine($"    - {leftConnector}: [{string.Join(", ", leftPin)}]");
                //    yaml.AppendLine($"    - {cable.Name}: [{string.Join(", ", cablePin1)}]");
                //    yaml.AppendLine($"    - {rightConnector}: [{string.Join(", ", rightPin)}]");
                //    Console.WriteLine($"Adding connection chain: {leftConnector}:[{leftPin}] -> {cable.Name}:[{cablePin1}] -> {cable.Name}:[{cablePin2}] -> {rightConnector}:[{rightPin}]");
                //}
            }

            // Shield Connections
            var shieldConnections = connections.Where(c => c.isShield).ToList();
            foreach (var (startPin, endPin, _, _) in shieldConnections)
            {
                var startElement = elements.Find(x => x.Pins.Exists(p => p.pin == startPin));
                var endElement = elements.Find(x => x.Pins.Exists(p => p.pin == endPin));
                var startPinName = startElement?.Pins.Find(p => p.pin == startPin).pinName;
                var endPinName = endElement?.Pins.Find(p => p.pin == endPin).pinName;
                if (startElement != null && endElement != null)
                {
                    yaml.AppendLine("  -");
                    yaml.AppendLine($"    - {startElement.Name}: {startPinName}");
                    yaml.AppendLine($"    - {endElement.Name}: {endPinName}");
                    Console.WriteLine($"Adding shield connection: {startElement.Name}:{startPinName} to {endElement.Name}:{endPinName}");
                }
            }

            // Additional BOM Items
            if (bomItems.Any())
            {
                yaml.AppendLine("additional_bom_items:");
                foreach (var item in bomItems)
                {
                    yaml.AppendLine("  -");
                    yaml.AppendLine($"    description: {item.Description}");
                    yaml.AppendLine($"    qty: {item.Quantity}");
                    if (!string.IsNullOrEmpty(item.Unit)) yaml.AppendLine($"    unit: {item.Unit}");
                }
            }

            // Metadata
            if (metadata.Any())
            {
                yaml.AppendLine("metadata:");
                foreach (var kvp in metadata)
                {
                    yaml.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            // Options
            if (options.ShowName || !string.IsNullOrEmpty(options.BgColor) || !string.IsNullOrEmpty(options.FontName) || !string.IsNullOrEmpty(options.TemplateSeparator))
            {
                yaml.AppendLine("options:");
                if (options.ShowName) yaml.AppendLine("  show_name: true");
                if (!string.IsNullOrEmpty(options.BgColor)) yaml.AppendLine($"  bgcolor: {options.BgColor}");
                if (!string.IsNullOrEmpty(options.FontName)) yaml.AppendLine($"  fontname: {options.FontName}");
                if (!string.IsNullOrEmpty(options.TemplateSeparator)) yaml.AppendLine($"  template_separator: {options.TemplateSeparator}");
            }

            YamlOutput.Text = yaml.ToString();
        }

        //private List<(string, List<string>, List<string>, List<string>, string, List<string>)> combineWireConnections(
        //    List<(string, string, string, string, string, string)> input)
        //{
        //    List<(string, List<string>, List<string>, List<string>, string, List<string>)> output = new();

        //    foreach (var ip in input)
        //    {
        //        // output conn ; output pin ; wire input ; wire output ; input conn ; input pin
        //        var tmplist = output.Where(x => ((x.Item1 == ip.Item1) && (x.Item5 == ip.Item5))).ToList();
        //        if (tmplist.Any())
        //        {
        //            var tmpitem = tmplist[0];
        //            output.Remove(tmpitem);
        //            tmpitem.Item2.Add(ip.Item2);
        //            tmpitem.Item3.Add(ip.Item3[1..]);
        //            tmpitem.Item4.Add(ip.Item4[1..]);
        //            tmpitem.Item6.Add(ip.Item6);
        //            output.Add(tmpitem);
        //        }
        //        else
        //        {
        //            var l1 = new List<string>() { ip.Item2 };
        //            var l2 = new List<string>() { ip.Item3[1..] };
        //            var l3 = new List<string>() { ip.Item4[1..] };
        //            var l4 = new List<string>() { ip.Item6 };
        //            output.Add(new(ip.Item1, l1, l2, l3, ip.Item5, l4));
        //        }
        //    }

        //    return output;
        //}
    }

    public enum PinSide
    {
        Right,
        Left
    }

    public class Element
    {
        public UIElement UIElement { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public ConnectorProperties ConnectorProps { get; set; }
        public CableProperties CableProps { get; set; }
        public NodeProperties NodeProps { get; set; }
        public List<(UIElement pin, string pinName, string label)> Pins { get; set; }
    }

    public class ConnectorProperties
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public List<string> PinLabels { get; set; }
        public string Color { get; set; }
        public ImageProperties Image { get; set; }
        public bool ShowName { get; set; }
        public PinSide PinSide { get; set; }
    }

    public class CableProperties
    {
        public string Name { get; set; }
        public string Gauge { get; set; }
        public double Length { get; set; }
        public string ColorCode { get; set; }
        public int WireCount { get; set; }
        public List<string> WireLabels { get; set; }
        public List<string> Colors { get; set; }
        public bool Shield { get; set; }
    }

    public class NodeProperties
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class ImageProperties
    {
        public string Src { get; set; }
        public string Caption { get; set; }
        public string BgColor { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class BomItem
    {
        public string Description { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
    }

    public class HarnessOptions
    {
        public bool ShowName { get; set; }
        public string BgColor { get; set; }
        public string FontName { get; set; }
        public string TemplateSeparator { get; set; }
    }
}