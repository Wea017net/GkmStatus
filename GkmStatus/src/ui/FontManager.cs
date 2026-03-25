using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using static GkmStatus.src.AppConstants;

namespace GkmStatus.src
{
    [SupportedOSPlatform("windows")]
    public partial class FontManager : IDisposable
    {
        [LibraryImport("gdi32.dll")]
        private static partial IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

        private PrivateFontCollection? _pfc;
        private readonly List<IntPtr> _fontPointers = [];

        public Font AppFont { get; private set; } = null!;
        public Font AppFontBold { get; private set; } = null!;
        public Font AppFontMedium { get; private set; } = null!;
        public Font MenuFont { get; private set; } = null!;

        public void Initialize(float uiScale)
        {
            SetupFonts(uiScale);
        }

        private void SetupFonts(float uiScale)
        {
            try
            {
                _pfc ??= new PrivateFontCollection();
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                var manifestNames = assembly.GetManifestResourceNames();
                var ttfCandidates = manifestNames.Where(n =>
                    n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) &&
                    n.Contains("plex", StringComparison.OrdinalIgnoreCase)).ToArray();

                if (ttfCandidates.Length == 0)
                {
                    ttfCandidates = [
                        FONT_REGULAR,
                        FONT_BOLD,
                        FONT_MEDIUM
                    ];
                }

                foreach (string resourceName in ttfCandidates)
                {
                    using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        try
                        {
                            byte[] fontData = new byte[stream.Length];
                            stream.ReadExactly(fontData, 0, (int)stream.Length);
                            IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                            Marshal.Copy(fontData, 0, fontPtr, fontData.Length);

                            _pfc.AddMemoryFont(fontPtr, fontData.Length);
                            AddFontMemResourceEx(fontPtr, (uint)fontData.Length, IntPtr.Zero, out uint _);
                            _fontPointers.Add(fontPtr);
                        }
                        catch (Exception ex) { Debug.WriteLine($"Failed loading font '{resourceName}': {ex.Message}"); }
                    }
                }

                FontFamily? regular = null, bold = null, medium = null;
                foreach (var ff in _pfc.Families)
                {
                    string name = ff.Name;
                    if (name.Contains("IBM Plex Sans JP", StringComparison.OrdinalIgnoreCase))
                    {
                        if (name.Contains("Bold", StringComparison.OrdinalIgnoreCase)) bold = ff;
                        else if (name.Contains("Medium", StringComparison.OrdinalIgnoreCase)) medium = ff;
                        else regular = ff;
                    }
                }

                // フォントの生成
                float basePx = 13.3f * uiScale;
                regular ??= _pfc.Families.Length > 0 ? _pfc.Families[0] : null;

                if (regular != null)
                {
                    AppFont = new Font(regular, basePx, GraphicsUnit.Pixel);
                    AppFontBold = bold != null ? new Font(bold, basePx, GraphicsUnit.Pixel) : new Font(regular, basePx, FontStyle.Bold, GraphicsUnit.Pixel);
                    AppFontMedium = medium != null ? new Font(medium, basePx, GraphicsUnit.Pixel) : new Font(regular, basePx, GraphicsUnit.Pixel);
                    SetupMenuFont(uiScale);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Font Load Error: " + ex.Message);
            }

            // フォールバック
            float fallbackPx = 13.3f * uiScale;
            AppFont = new Font("Meiryo UI", fallbackPx, GraphicsUnit.Pixel);
            AppFontBold = new Font("Meiryo UI", fallbackPx, FontStyle.Bold, GraphicsUnit.Pixel);
            AppFontMedium = new Font("Meiryo UI", fallbackPx, GraphicsUnit.Pixel);
            SetupMenuFont(uiScale);
        }

        private void SetupMenuFont(float uiScale)
        {
            float menuPx = 12f * uiScale;
            try
            {
                using var testFont = new Font("Yu Gothic UI", menuPx, GraphicsUnit.Pixel);
                if (testFont.Name == "Yu Gothic UI")
                {
                    MenuFont = new Font("Yu Gothic UI", menuPx, GraphicsUnit.Pixel);
                }
                else
                {
                    var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
                    MenuFont = new Font(family, menuPx, GraphicsUnit.Pixel);
                }
            }
            catch
            {
                var family = SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif;
                MenuFont = new Font(family, menuPx, GraphicsUnit.Pixel);
            }
        }

        public void Dispose()
        {
            AppFont?.Dispose();
            AppFontBold?.Dispose();
            AppFontMedium?.Dispose();
            MenuFont?.Dispose();

            foreach (var ptr in _fontPointers) Marshal.FreeCoTaskMem(ptr);
            _fontPointers.Clear();
            _pfc?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
