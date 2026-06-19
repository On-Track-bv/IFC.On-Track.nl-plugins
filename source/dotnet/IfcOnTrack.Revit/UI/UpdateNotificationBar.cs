// Purpose: Shows update notification in Revit UI
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IfcOnTrack.Core.Update;
using WpfColor = System.Windows.Media.Color;

namespace IfcOnTrack.Revit.UI;

/// <summary>
/// Update notification bar shown at top of dockable pane.
/// </summary>
public class UpdateNotificationBar : Border
{
    public UpdateNotificationBar(UpdateInfo updateInfo)
    {
        Background = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xF3, 0xCD)); // Light yellow
        BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xC1, 0x07)); // Orange
        BorderThickness = new Thickness(0, 0, 0, 2);
        Padding = new Thickness(12, 8, 12, 8);

        var stackPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Icon
        var icon = new TextBlock
        {
            Text = "🔔",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        stackPanel.Children.Add(icon);

        // Message
        var message = new TextBlock
        {
            Text = $"Nieuwe versie beschikbaar: {updateInfo.LatestVersion}",
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        stackPanel.Children.Add(message);

        // Download button
        var downloadButton = new Button
        {
            Content = "Download",
            Padding = new Thickness(12, 4, 12, 4),
            Background = new SolidColorBrush(WpfColor.FromRgb(0x0D, 0x6E, 0xFD)), // Blue
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontWeight = FontWeights.Medium
        };

        downloadButton.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(updateInfo.ReleaseUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updateInfo.ReleaseUrl,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Silently fail if browser cannot be opened
                }
            }
        };

        stackPanel.Children.Add(downloadButton);

        // Close button
        var closeButton = new Button
        {
            Content = "✕",
            Padding = new Thickness(8, 4, 8, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(8, 0, 0, 0)
        };

        closeButton.Click += (s, e) => Visibility = System.Windows.Visibility.Collapsed;
        stackPanel.Children.Add(closeButton);

        Child = stackPanel;
    }
}
