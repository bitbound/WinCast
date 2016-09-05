using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WinCast
{
    public static class User32
    {
        public const Int32 CURSOR_SHOWING = 0x00000001;
        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const int MOUSEEVENTF_RIGHTUP = 0x10;
        public const int MOUSEEVENTF_MOVE = 0x0001;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        public struct ICONINFO
        {
            public bool fIcon;
            public Int32 xHotspot;
            public Int32 yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public Int32 x;
            public Int32 y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        public static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool DrawIcon(IntPtr hdc, int x, int y, IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        public static void sendLeftMouseDown(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
        }
        public static void sendLeftMouseUp(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
        }
        public static void sendRightMouseDown(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_RIGHTDOWN, (uint)x, (uint)y, 0, 0);
        }
        public static void sendRightMouseUp(int x, int y)
        {
            User32.mouse_event(User32.MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
        }
        public static void sendMouseMove(int x, int y)
        {
            System.Windows.Forms.Cursor.Position = new System.Drawing.Point(x, y);
        }
    }
}
