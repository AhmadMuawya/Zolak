// Resolve ambiguity between System.Windows.Forms and System.Windows (WPF) namespaces.
// When UseWindowsForms + UseWPF are both enabled with ImplicitUsings,
// several types overlap. We explicitly pick the WPF versions.
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
global using Window = System.Windows.Window;
global using Button = System.Windows.Controls.Button;
global using ComboBox = System.Windows.Controls.ComboBox;
global using TextBox = System.Windows.Controls.TextBox;
global using Image = System.Windows.Controls.Image;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Orientation = System.Windows.Controls.Orientation;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using VerticalAlignment = System.Windows.VerticalAlignment;
global using Stretch = System.Windows.Media.Stretch;
