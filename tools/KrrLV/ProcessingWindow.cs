using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using krrTools.tools.Shared;

namespace krrTools.tools.KrrLV
{
    public class ProcessingWindow : Window
    {
        private readonly ProgressBar? _progressBar;

        public ProcessingWindow()
        {
            Title = "处理中...";
            Width = 300;
            Height = 100;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;

            var grid = new Grid { Margin = new Thickness(20, 30, 20, 20) };

            _progressBar = new ProgressBar { Height = 25, Minimum = 0, Maximum = 100 };
            grid.Children.Add(_progressBar);
            Content = grid;

            SourceInitialized += OnSourceInitialized;

            SharedUIComponents.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => SharedUIComponents.LanguageChanged -= OnLanguageChanged;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            ApplyBlur();
        }

        public void UpdateProgress(int current, int total)
        {
            if (_progressBar == null) return;

            Dispatcher.Invoke(() =>
            {
                _progressBar.Maximum = Math.Max(1, total);
                _progressBar.Value = Math.Min(current, (int)_progressBar.Maximum);
            });
        }

        private void ApplyBlur()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // Try acrylic-like blur first; if not supported, try DWM blur; otherwise fallback silently.
                if (!TryEnableAcrylicBlur(hwnd))
                {
                    TryEnableDwmBlur(hwnd);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyBlur failed: {ex.Message}");
            }
        }

        // --- DWM blur (older, widely supported) ---
        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_BLURBEHIND
        {
            public DwmBbFlags dwFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fEnable;
            public IntPtr hRgnBlur;
            [MarshalAs(UnmanagedType.Bool)] public bool fTransitionOnMaximized;
        }

        [Flags]
        private enum DwmBbFlags : uint
        {
            DWM_BB_ENABLE = 0x00000001,
            DWM_BB_BLURREGION = 0x00000002,
            DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004
        }

        private static bool TryEnableDwmBlur(IntPtr hwnd)
        {
            try
            {
                var bb = new DWM_BLURBEHIND
                {
                    dwFlags = DwmBbFlags.DWM_BB_ENABLE,
                    fEnable = true,
                    hRgnBlur = IntPtr.Zero,
                    fTransitionOnMaximized = false
                };
                return DwmEnableBlurBehindWindow(hwnd, ref bb) == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryEnableDwmBlur failed: {ex.Message}");
                return false;
            }
        }

        // --- SetWindowCompositionAttribute / AccentPolicy (Windows 10+) ---
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private enum WindowCompositionAttribute
        {
            WCA_UNDEFINED = 0,
            WCA_ACCENT_POLICY = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private static bool TryEnableAcrylicBlur(IntPtr hwnd)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                    AccentFlags = 2,
                    // GradientColor is AARRGGBB. Keep alpha small for subtle effect.
                    GradientColor = unchecked((int)0xCCFFFFFF),
                    AnimationId = 0
                };

                var size = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, accentPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                        SizeOfData = size,
                        Data = accentPtr
                    };

                    return SetWindowCompositionAttribute(hwnd, ref data) == 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryEnableAcrylicBlur failed: {ex.Message}");
                return false;
            }
        }

        private void OnLanguageChanged()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        Title = SharedUIComponents.IsChineseLanguage() ? "\u5904\u7406\u4e2d..." : "Processing...";
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ProcessingWindow inner OnLanguageChanged failed: {ex.Message}"); }
                }));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ProcessingWindow OnLanguageChanged invoke failed: {ex.Message}"); }
        }
    }
}
