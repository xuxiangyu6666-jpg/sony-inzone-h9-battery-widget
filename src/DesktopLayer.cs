using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace InzoneH9Widget {
    public sealed class DesktopLayer
    {
        readonly Window window;
        public bool IsDesktop { get; private set; }
        public DesktopLayer(Window window)
        {
            this.window = window;
            var source = HwndSource.FromHwnd(Handle);
            source.AddHook(KeepDesktopOrder);
            window.Closed += delegate { source.RemoveHook(KeepDesktopOrder); };
        }
        IntPtr KeepDesktopOrder(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!IsDesktop) return IntPtr.Zero;
            if (message == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
            if (message == 0x46)
            {
                var position = (WindowPosition)Marshal.PtrToStructure(lParam, typeof(WindowPosition));
                if ((position.Flags & 0x4) == 0)
                {
                    position.After = new IntPtr(1); // HWND_BOTTOM, above its desktop owner
                    position.Flags |= 0x10 | 0x200;
                    Marshal.StructureToPtr(position, lParam, false);
                }
            }
            return IntPtr.Zero;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct WindowPosition { public IntPtr Hwnd, After; public int X, Y, Width, Height; public uint Flags; }
        public IntPtr Handle { get { return new WindowInteropHelper(window).Handle; } }
        public static IntPtr FindDesktopHost()
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hwnd, IntPtr data) {
                var name = new StringBuilder(64); GetClassName(hwnd, name, name.Capacity);
                if ((name.ToString() == "Progman" || name.ToString() == "WorkerW") && IsWindowVisible(hwnd) &&
                    FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                { found = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }
        public void SetDesktop(bool desktop)
        {
            if (desktop == IsDesktop) return;
            var host = desktop ? FindDesktopHost() : IntPtr.Zero;
            if (desktop && host == IntPtr.Zero) throw new DataError("未找到 Windows 桌面，当前继续使用普通窗口。");
            var hwnd = Handle;
            Rect rect;
            if (!GetWindowRect(hwnd, out rect)) throw new DataError("无法读取窗口位置。");
            long originalStyle = GetStyle(hwnd, -16), originalEx = GetStyle(hwnd, -20);
            var originalParent = GetParent(hwnd);
            bool originalDesktop = IsDesktop;
            window.Topmost = false;
            window.ShowInTaskbar = !desktop;
            hwnd = Handle;
            try
            {
                // Keep the transparent WPF surface top-level. Turning it into an Explorer
                // child can leave a live but invisible layered window on Windows 11.
                SetStyle(hwnd, -16, (originalStyle & ~0x40000000L) | 0x80000000L);
                SetStyle(hwnd, -8, host.ToInt64());
                if (GetParent(hwnd) != host) throw new DataError("无法切换桌面显示模式，请使用普通窗口。");
                SetStyle(hwnd, -20, desktop ? (originalEx & ~0x40000L) | 0x80L | 0x08000000L : (originalEx & ~(0x80L | 0x08000000L)) | 0x40000L);
                IsDesktop = desktop;
                if (!SetWindowPos(hwnd, desktop ? new IntPtr(1) : IntPtr.Zero, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, 0x10 | 0x20 | 0x40 | 0x200))
                    throw new DataError("无法切换窗口层级。");
                window.InvalidateVisual();
            }
            catch
            {
                window.ShowInTaskbar = !originalDesktop;
                IsDesktop = originalDesktop;
                SetStyle(hwnd, -8, originalParent.ToInt64()); SetStyle(hwnd, -16, originalStyle); SetStyle(hwnd, -20, originalEx);
                throw;
            }
        }
        public void Rebind()
        {
            // Explorer may restart while the widget stays alive.
            IsDesktop = false;
            try { SetDesktop(true); }
            catch { IsDesktop = true; SetDesktop(false); throw; }
        }
        public void MoveToScreen(Point point)
        {
            var pos = new NativePoint { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) };
            SetWindowPos(Handle, IntPtr.Zero, pos.X, pos.Y, 0, 0, 0x1 | 0x4 | 0x10);
        }
        static long GetStyle(IntPtr hwnd, int index)
        { return IntPtr.Size == 8 ? GetWindowLongPtr(hwnd, index).ToInt64() : (long)(uint)GetWindowLong(hwnd, index); }
        static void SetStyle(IntPtr hwnd, int index, long style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr(hwnd, index, new IntPtr(style));
            else SetWindowLong(hwnd, index, unchecked((int)style));
        }
        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct NativePoint { public int X, Y; }
        delegate bool EnumProc(IntPtr hwnd, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    }
}
