using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ReLPC.Views
{
    public partial class LoginWindowView : Window
    {
        private GradientStop[] _pageStops = null!;
        private GradientStop[] _panelStops = null!;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private DispatcherTimer _timer = null!;

        public LoginWindowView()
        {
            InitializeComponent();

            InitializeGradients();
            StartAnimation();
        }

        private void InitializeGradients()
        {
            // === PAGE BACKGROUND ===
            var pageBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };

            _pageStops =
            [
                new GradientStop(Color.Parse("#FF2D32"), 0),
                new GradientStop(Color.Parse("#FF4438"), 0),
                new GradientStop(Color.Parse("#FF8650"), 0),
                new GradientStop(Color.Parse("#FF4B3A"), 0),
                new GradientStop(Color.Parse("#FF7442"), 1),
            ];

            foreach (var stop in _pageStops)
                pageBrush.GradientStops.Add(stop);

            PageRoot.Background = pageBrush;

            // === PANEL BACKGROUND ===
            var panelBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };

            _panelStops =
            [
                new GradientStop(Color.Parse("#FF2D32"), 0),
                new GradientStop(Color.Parse("#FF3A34"), 0),
                new GradientStop(Color.Parse("#FF7550"), 0),
                new GradientStop(Color.Parse("#FF553C"), 0),
                new GradientStop(Color.Parse("#FF7442"), 1),
            ];

            foreach (var stop in _panelStops)
                panelBrush.GradientStops.Add(stop);

            LoginPanel.Background = panelBrush;
        }

        private void StartAnimation()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0)
            };

            _timer.Tick += OnRendering;
            _timer.Start();
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            var t = _stopwatch.Elapsed.TotalSeconds;

            var pageFlow = 0.5 * (1 + Math.Sin(t * 0.9 + Math.Sin(t * 0.2)));
            var panelFlow = 0.5 * (1 + Math.Sin(t * 1.15 + 1.2 + Math.Cos(t * 0.3)));

            UpdateGradient(_pageStops, pageFlow);
            UpdateGradient(_panelStops, panelFlow);
        }

        private static void UpdateGradient(GradientStop[] stops, double flow)
        {
            var highlightStart = Math.Clamp(flow - 0.18, 0, 1);
            var highlightMiddle = Math.Clamp(flow, 0, 1);
            var highlightEnd = Math.Clamp(flow + 0.18, 0, 1);

            stops[1].Offset = highlightStart;
            stops[2].Offset = highlightMiddle;
            stops[3].Offset = highlightEnd;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _timer.Stop();
            _timer.Tick -= OnRendering;

            base.OnDetachedFromVisualTree(e);
        }

        private void HidePassword_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;

            if (toggleButton.FindAncestorOfType<TextBox>() is not { } textBox) return;

            if (toggleButton.IsChecked ?? false)
            {
                textBox.PasswordChar = '\u25cf';
            }
            else
            {
                textBox.PasswordChar = '\0';
            }
        }
    }
}