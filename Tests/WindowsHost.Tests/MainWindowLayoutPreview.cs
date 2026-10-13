using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Application = System.Windows.Application;

namespace WindowsHost.Tests;

internal static class MainWindowLayoutPreview
{
    public static void RenderAndAssert(Application app, string root)
    {
        var ns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
        var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var document = XDocument.Load(Path.Combine(root, "Windows-host/MainWindow.xaml"));
        document.Root!.Attribute(x + "Class")!.Remove();
        var events = new[] { "Click", "Checked", "Unchecked", "SelectionChanged", "TextChanged", "PreviewTextInput", "MouseLeftButtonDown" };
        foreach (var attribute in document.Descendants().Attributes().Where(a => events.Contains(a.Name.LocalName)).ToArray()) attribute.Remove();
        // Load the real layout and resources, but no MainWindow constructor or event handlers.
        document.Root.SetAttributeValue("Icon", "pack://application:,,,/EzAcrossControl;component/Assets/EzAcrossControl.png");
        foreach(var image in document.Descendants(ns + "Image")) image.SetAttributeValue("Source", "pack://application:,,,/EzAcrossControl;component/Assets/EzAcrossControl.png");
        foreach(var font in document.Descendants().Attributes("FontFamily").Where(a => a.Value.StartsWith("./Fonts/")))
            font.Value = "pack://application:,,,/EzAcrossControl;component/Fonts/" + font.Value[8..];
        var directory = Path.Combine(root, "work/ui-startup-preview");
        Directory.CreateDirectory(directory);
        foreach(var theme in new[] { "Dark", "Light" })
        {
            app.Resources.MergedDictionaries.Clear();
            using var themeStream = File.OpenRead(Path.Combine(root, $"Windows-host/Themes/{theme}Theme.xaml"));
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(themeStream));
            foreach(var locale in Localization.Languages.Select(l => l.Code).ToArray())
            {
                Localization.Apply(locale);
                var window = (Window)XamlReader.Parse(document.ToString());
                window.FontFamily = new FontFamily("pack://application:,,,/EzAcrossControl;component/Fonts/#Inter");
                typeof(MainWindow).GetMethod("ApplyLanguageLayoutTo", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { window });
                var content = (FrameworkElement)window.Content;
                var advanced = (Border)window.FindName("AdvancedPanel");
                var start = (Button)window.FindName("BtnStart");
                var stop = (Button)window.FindName("BtnStop");
                var serverButtons = typeof(MainWindow).GetMethod("ApplyServerButtonsTo", BindingFlags.NonPublic | BindingFlags.Static)!;
                foreach(var state in new[] { (running: true, starting: false), (running: false, starting: false), (running: false, starting: true) })
                {
                    serverButtons.Invoke(null, new object[] {window, state.running, state.starting});
                    Assert.IsType<System.Windows.Shapes.Path>(start.Content);
                    Assert.IsType<System.Windows.Shapes.Rectangle>(stop.Content);
                    Assert.Same(app.FindResource(state.running ? "SuccessPrimaryBrush" : "TextPrimaryBrush"), start.Foreground);
                    Assert.Same(app.FindResource(!state.running && !state.starting ? "ErrorPrimaryBrush" : "TextPrimaryBrush"), stop.Foreground);
                    Assert.Equal(!state.running && !state.starting, start.IsEnabled);
                    Assert.Equal(state.running, stop.IsEnabled);
                }
                var applicationVersion = typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
                ((TextBlock)window.FindName("TxtApplicationVersion")).Text = applicationVersion;
                ((ComboBox)window.FindName("CmbLanguage")).ItemsSource = new[] { new LanguageOption("system", Localization.T("System Language")) };
                ((ComboBox)window.FindName("CmbLanguage")).SelectedIndex = 0;
                var checkbox = (CheckBox)window.FindName("ChkStartWithWindows");
                var log = (TextBox)window.FindName("TxtLog");
                log.Text = string.Join(Environment.NewLine, Enumerable.Repeat("[12:00:00] Control ready via USB. Native input capture fixture.", 30));
                advanced.Visibility = Visibility.Visible;
                var measure = typeof(MainWindow).GetMethod("MeasureAdvancedMinimumTo", BindingFlags.NonPublic | BindingFlags.Static)!;
                var shortLogHeight = (double)measure.Invoke(null, new object[] {window})!;
                log.AppendText(string.Concat(Enumerable.Repeat("\nActivity entry", 3000)));
                var longLogHeight = (double)measure.Invoke(null, new object[] {window})!;
                Assert.Equal(shortLogHeight, longLogHeight, 2);
                Assert.InRange(longLogHeight, 535, 900);
                log.Text = string.Join(Environment.NewLine, Enumerable.Repeat("[12:00:00] Control ready via USB. Native input capture fixture.", 30));
                foreach(var mode in new[] { "normal", "advanced", "short", "update" })
                {
                    advanced.Visibility = mode == "normal" ? Visibility.Collapsed : Visibility.Visible;
                    var running = mode != "update";
                    serverButtons.Invoke(null, new object[] {window, running, false});
                    ((TextBlock)window.FindName("TxtServerStatus")).Text = Localization.T(running ? "Server Running" : "Server Stopped");
                    checkbox.IsChecked = mode == "advanced";
                    var availability = mode == "update" ? Updates.UpdateAvailability.Available
                        : mode == "short" ? Updates.UpdateAvailability.Unknown : Updates.UpdateAvailability.Current;
                    var versionText = (TextBlock)window.FindName("TxtApplicationVersion");
                    typeof(MainWindow).GetMethod("ApplyVersionAvailabilityTo", BindingFlags.NonPublic | BindingFlags.Static)!
                        .Invoke(null, new object[] {versionText, availability});
                    Assert.Same(app.FindResource(availability == Updates.UpdateAvailability.Current ? "SuccessPrimaryBrush" : "WarningPrimaryBrush"), versionText.Foreground);
                    Assert.Equal(applicationVersion, versionText.Text);
                    var height = mode == "normal" ? 545 : mode == "short" ? Math.Max(768, (int)Math.Ceiling(longLogHeight)) : 900;
                    var width = Math.Ceiling(window.Width);
                    content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
                    var connection = (Border)window.FindName("ConnectionCard");
                    var network = (Border)window.FindName("NetworkCard");
                    Assert.Equal(connection.ActualWidth, network.ActualWidth, 2);
                    Assert.Equal(connection.ActualHeight, network.ActualHeight, 2);
                    Assert.Equal(start.ActualWidth, stop.ActualWidth, 2);
                    Assert.Equal(start.ActualHeight, stop.ActualHeight, 2);
                    Assert.Equal(36, start.ActualWidth, 2);
                    Assert.InRange(stop.TransformToAncestor(connection).Transform(new Point(stop.ActualWidth, stop.ActualHeight)).X, 0, connection.ActualWidth);
                    var editPort = (Button)window.FindName("BtnApplyPort");
                    Assert.InRange(editPort.TransformToAncestor(network).Transform(new Point(editPort.ActualWidth, editPort.ActualHeight)).X, 0, network.ActualWidth - 16);
                    Assert.DoesNotContain(Descendants(content).OfType<ScrollViewer>(), s => s.Name == "MainScrollViewer");
                    if(mode != "normal")
                    {
                        var diagnostics = (StackPanel)window.FindName("DiagnosticsList");
                        Assert.Equal(6, diagnostics.Children.Count);
                        Assert.Equal(Orientation.Vertical, diagnostics.Orientation);
                        var rows = diagnostics.Children.Cast<Grid>().ToArray();
                        for(var i=1; i<rows.Length; i++)
                            Assert.True(rows[i].TransformToAncestor(diagnostics).Transform(new Point()).Y >=
                                rows[i-1].TransformToAncestor(diagnostics).Transform(new Point(0, rows[i-1].ActualHeight)).Y);
                        var logBottom = log.TransformToAncestor(content).Transform(new Point(log.ActualWidth, log.ActualHeight));
                        Assert.InRange(logBottom.Y, 0, height);
                        Assert.InRange(advanced.TransformToAncestor(content).Transform(new Point(advanced.ActualWidth, advanced.ActualHeight)).Y, 0, height - 16);
                        Assert.True(log.ActualHeight >= 48, $"Log clipped: {theme}/{locale}/{mode}");
                        var language = (ComboBox)window.FindName("CmbLanguage");
                        var version = (TextBlock)window.FindName("TxtApplicationVersion");
                        Assert.True(checkbox.TransformToAncestor(content).Transform(new Point()).Y >= language.TransformToAncestor(content).Transform(new Point(0, language.ActualHeight)).Y);
                        Assert.True(version.TransformToAncestor(content).Transform(new Point()).Y >= checkbox.TransformToAncestor(content).Transform(new Point(0, checkbox.ActualHeight)).Y);
                    }
                    var bitmap = new RenderTargetBitmap((int)width, height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(Path.Combine(directory, $"{theme}-{locale}-{mode}.png")); encoder.Save(output);
                }
                window.Close();
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach(var descendant in Descendants(child)) yield return descendant;
        }
    }
}
