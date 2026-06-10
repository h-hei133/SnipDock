using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Serilog;

namespace SnipDock.App.Services
{
    public class GlobalHotkeyService : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 9000;

        // Modifiers
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;

        private IntPtr _hWnd;
        private HwndSource? _hwndSource;
        private bool _isRegistered = false;

        public event EventHandler? HotkeyTriggered;

        public void Register(Window window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            // Ensure window handle is created
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.EnsureHandle();
            Log.Information("开始注册全局热键 Ctrl+Alt+P (Hotkey registering)...");

            try
            {
                // Register Ctrl + Alt + P
                // fsModifiers: MOD_ALT (0x0001) | MOD_CONTROL (0x0002) = 0x0003
                // vk: 'P' is 0x50
                _isRegistered = RegisterHotKey(_hWnd, HotkeyId, MOD_ALT | MOD_CONTROL, 0x50);

                if (_isRegistered)
                {
                    _hwndSource = HwndSource.FromHwnd(_hWnd);
                    _hwndSource.AddHook(HwndHook);                    Log.Information("全局热键 Ctrl+Alt+P 注册成功 (Hotkey registered).");
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();                    Log.Error("全局热键注册失败 (Hotkey registration failed). Win32 Error Code: {Code}", errorCode);
                    throw new InvalidOperationException($"Failed to register global hotkey. Error code: {errorCode}");
                }
            }
            catch (Exception ex)
            {                Log.Error(ex, "全局热键注册异常 (Hotkey registration failed)");
                throw;
            }
        }

        public void Unregister()
        {
            if (!_isRegistered) return;
            Log.Information("开始取消注册全局热键 (Hotkey unregistering)...");
            try
            {
                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(HwndHook);
                    _hwndSource = null;
                }

                bool success = UnregisterHotKey(_hWnd, HotkeyId);
                if (success)
                {                    Log.Information("全局热键已取消注册 (Hotkey unregistered).");
                }
                else
                {                    Log.Warning("全局热键取消注册失败，可能已解注册");
                }
            }
            catch (Exception ex)
            {                Log.Error(ex, "全局热键取消注册异常");
            }
            finally
            {
                _isRegistered = false;
                _hWnd = IntPtr.Zero;
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {                Log.Information("全局热键被触发 (Hotkey triggered).");
                HotkeyTriggered?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            GC.SuppressFinalize(this);
        }
    }
}
