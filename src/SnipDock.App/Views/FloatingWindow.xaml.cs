using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SnipDock.App.ViewModels;
using Serilog;

namespace SnipDock.App.Views
{
    public partial class FloatingWindow : Window
    {
        private System.Windows.Point _startMouseScreenPos;
        private double _startWindowLeft;
        private double _startWindowTop;
        private bool _isMouseDown;
        private bool _isDragging;

        public FloatingWindow(FloatingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private System.Windows.Point GetMouseScreenPositionInDips(System.Windows.Input.MouseEventArgs e)
        {
            // Get mouse position relative to this window in WPF DIP units
            System.Windows.Point relativePos = e.GetPosition(this);
            
            // Convert relative DIP position to physical screen pixels
            System.Windows.Point physicalScreenPos = PointToScreen(relativePos);
            
            // Retrieve system DPI scale factor of this window
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            
            // Convert physical screen pixels back to standard screen DIP units to match window Left/Top
            return new System.Windows.Point(physicalScreenPos.X / dpi.DpiScaleX, physicalScreenPos.Y / dpi.DpiScaleY);
        }

        public System.Windows.Point ClampPosition(double left, double top)
        {
            Rect workArea = SystemParameters.WorkArea;
            double minX = workArea.Left;
            double maxX = workArea.Left + workArea.Width - this.Width;
            double minY = workArea.Top;
            double maxY = workArea.Top + workArea.Height - this.Height;

            double clampedLeft = Math.Clamp(left, minX, maxX);
            double clampedTop = Math.Clamp(top, minY, maxY);

            if (clampedLeft != left || clampedTop != top)
            {
                Log.Warning("Floating position clamped from ({Left}, {Top}) to ({ClampedLeft}, {ClampedTop})", left, top, clampedLeft, clampedTop);
            }

            return new System.Windows.Point(clampedLeft, clampedTop);
        }

        private void Border_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _startMouseScreenPos = GetMouseScreenPositionInDips(e);
            _startWindowLeft = this.Left;
            _startWindowTop = this.Top;
            _isDragging = false;

            Log.Information("Floating button mouse down at {Point}", e.GetPosition(this));

            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void Border_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isMouseDown && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentMouseScreenPos = GetMouseScreenPositionInDips(e);
                double diffX = currentMouseScreenPos.X - _startMouseScreenPos.X;
                double diffY = currentMouseScreenPos.Y - _startMouseScreenPos.Y;

                if (!_isDragging)
                {
                    if (Math.Abs(diffX) >= SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diffY) >= SystemParameters.MinimumVerticalDragDistance)
                    {
                        _isDragging = true;
                        Log.Information("Floating drag start");
                    }
                }

                if (_isDragging)
                {
                    double targetLeft = _startWindowLeft + diffX;
                    double targetTop = _startWindowTop + diffY;

                    // Keep window fully inside the screen work area
                    System.Windows.Point clamped = ClampPosition(targetLeft, targetTop);
                    this.Left = clamped.X;
                    this.Top = clamped.Y;

                    Log.Information("Floating dragging: left={Left}, top={Top}", this.Left, this.Top);
                }
            }
        }

        private void Border_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMouseDown)
            {
                _isMouseDown = false;
                ((UIElement)sender).ReleaseMouseCapture();

                if (_isDragging)
                {
                    _isDragging = false;
                    Log.Information("Floating drag end and saved position. Left={Left}, Top={Top}", this.Left, this.Top);

                    // Save position instantly on drag-end
                    SaveCurrentPosition();
                }
                else
                {
                    Log.Information("Floating button clicked. Toggling panel.");
                    if (DataContext is FloatingViewModel vm)
                    {
                        vm.TogglePanelCommand.Execute(null);
                    }
                }

                e.Handled = true;
            }
        }

        private void SaveCurrentPosition()
        {
            try
            {
                if (System.Windows.Application.Current is App app)
                {
                    app.SaveFloatingWindowPosition(this.Left, this.Top);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to persist AppSettings coordinates on drag end.");
            }
        }
    }
}
