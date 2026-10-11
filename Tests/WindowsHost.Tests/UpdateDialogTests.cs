using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WindowsHost.Updates;
using Application = System.Windows.Application;

namespace WindowsHost.Tests;

public class UpdateDialogTests
{
    [Fact]
    public void ExistingThemeAndLocalizedDialogRenderWithoutStartingHost()
    {
        Exception? failure = null;
        var thread = new Thread(() => {
            try {
                var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
                var app = new Application();
                var ns = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
                var resourceElements = XDocument.Load(Path.Combine(root, "Windows-host/MainWindow.xaml")).Root!.Element(ns + "Window.Resources")!.Elements();
                var dictionary = new XElement(ns + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", x.NamespaceName), resourceElements);
                var resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
                var owner = new Window { Resources = resources, FontFamily = new FontFamily("pack://application:,,,/EzAcrossControl;component/Fonts/#Inter") };
                new System.Windows.Interop.WindowInteropHelper(owner).EnsureHandle();
                var release = new UpdateRelease("1.1.20", new Uri("https://github.com/eddesignerez/EzAcrossControl"), 1234, new string('a', 64));
                foreach (var theme in new[] { "Dark", "Light" }) {
                    app.Resources.MergedDictionaries.Clear();
                    using var themeStream = File.OpenRead(Path.Combine(root, $"Windows-host/Themes/{theme}Theme.xaml"));
                    app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(themeStream));
                    foreach (var locale in new[] { "pt-BR", "en" }) {
                        Localization.Apply(locale);
                        var dialog = new UpdateDialog(owner, release);
                        var content = (FrameworkElement)dialog.Content;
                        dialog.Content = null;
                        var frame = new Border { Width = 480, Background = (Brush)dialog.FindResource("SurfaceBrush"), Child = content };
                        dialog.Content = frame;
                        foreach (var phase in new[] { "offer", "download", "failed" }) {
                            if (phase == "download") dialog.Downloading(65);
                            if (phase == "failed") dialog.Failed();
                            frame.Measure(new Size(480, double.PositiveInfinity));
                            frame.Arrange(new Rect(new Point(), frame.DesiredSize));
                            frame.UpdateLayout();
                            Assert.InRange(frame.ActualHeight, 100, 600);
                            var bitmap = new RenderTargetBitmap(480, (int)Math.Ceiling(frame.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(frame);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            var directory = Path.Combine(root, "work/update-preview"); Directory.CreateDirectory(directory);
                            using var output = File.Create(Path.Combine(directory, $"{theme}-{locale}-{phase}.png"));
                            encoder.Save(output);
                        }
                    }
                }
                owner.Close();
                app.Shutdown();
            } catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        Assert.Null(failure);
    }
}
