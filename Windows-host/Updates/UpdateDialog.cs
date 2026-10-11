using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace WindowsHost.Updates;

/// <summary>Uses the existing host tokens and Inter; no new visual identity.</summary>
public sealed class UpdateDialog : Window
{
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _accept;
    private readonly Button _later;
    public UpdateDialog(Window owner, UpdateRelease release)
    {
        Owner = owner;
        Title = Localization.T("Update Available");
        Width = 480; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = owner.FontFamily;
        SetResourceReference(BackgroundProperty, "SurfaceBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = Title, FontSize = 20, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = string.Format(Localization.T("Update Offer"), release.Version), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(_status);
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, Visibility = Visibility.Collapsed };
        _progress.SetResourceReference(ForegroundProperty, "SuccessPrimaryBrush");
        _progress.SetResourceReference(BackgroundProperty, "SurfaceSoftBrush");
        panel.Children.Add(_progress);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        _later = new Button { Content = Localization.T("Update Later"), IsCancel = true, Margin = new Thickness(0, 0, 12, 0), Style = (Style)owner.FindResource("SecondaryButtonStyle") };
        _accept = new Button { Content = Localization.T("Update Now"), Style = (Style)owner.FindResource("PrimaryButtonStyle") };
        _later.Click += (_, _) => Close();
        _accept.Click += (_, _) => Accepted?.Invoke(this, EventArgs.Empty);
        buttons.Children.Add(_later); buttons.Children.Add(_accept); panel.Children.Add(buttons);
        Content = panel;
    }
    public event EventHandler? Accepted;
    public void Downloading(int percent)
    {
        _accept.IsEnabled = false;
        _later.Content = Localization.T("Update Cancel");
        _progress.Visibility = Visibility.Visible;
        _progress.Value = percent;
        _status.Text = string.Format(Localization.T("Update Downloading"), percent);
    }
    public void Failed()
    {
        _status.Text = Localization.T("Update Failed");
        _progress.Visibility = Visibility.Collapsed;
        _accept.IsEnabled = true;
        _later.Content = Localization.T("Update Later");
    }
}
