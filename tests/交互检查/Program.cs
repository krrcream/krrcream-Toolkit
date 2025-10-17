// See https://aka.ms/new-console-template for more information
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace HotkeyTest
{
    class Program
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_N = 0x4E;
        private const byte VK_D = 0x44;
        private const byte VK_K = 0x4B;

        // static void Main()
        // {
        //     Console.WriteLine("Testing global hotkeys...");
        //
        //     // Wait for application to start
        //     Thread.Sleep(3000);
        //
        //     // Test Ctrl+Shift+N (N2NC)
        //     Console.WriteLine("Pressing Ctrl+Shift+N...");
        //     PressHotkey(VK_CONTROL, VK_SHIFT, VK_N);
        //
        //     Thread.Sleep(1000);
        //
        //     // Test Ctrl+Shift+D (DP)
        //     Console.WriteLine("Pressing Ctrl+Shift+D...");
        //     PressHotkey(VK_CONTROL, VK_SHIFT, VK_D);
        //
        //     Thread.Sleep(1000);
        //
        //     // Test Ctrl+Shift+K (KRRLN)
        //     Console.WriteLine("Pressing Ctrl+Shift+K...");
        //     PressHotkey(VK_CONTROL, VK_SHIFT, VK_K);
        //
        //     Console.WriteLine("Hotkey test completed.");
        // }

        private static void PressHotkey(byte ctrl, byte shift, byte key)
        {
            // Press Ctrl
            keybd_event(ctrl, 0, 0, UIntPtr.Zero);
            Thread.Sleep(100);

            // Press Shift
            keybd_event(shift, 0, 0, UIntPtr.Zero);
            Thread.Sleep(100);

            // Press key
            keybd_event(key, 0, 0, UIntPtr.Zero);
            Thread.Sleep(100);

            // Release key
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(100);

            // Release Shift
            keybd_event(shift, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(100);

            // Release Ctrl
            keybd_event(ctrl, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(100);
        }
    }
}
