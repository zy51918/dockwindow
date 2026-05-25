// Offline tool — generates src/DockWindow/Resources/tray.ico.
// Re-run when you want to tweak the icon design.
//
//     dotnet run --project tools/IconGen

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

const string OutputPath  = @"src\DockWindow\Resources\tray.ico";
const string PreviewPath = @"docs\tray-preview.png";

int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
var pngs = sizes.Select(RenderPng).ToArray();
File.WriteAllBytes(OutputPath, PackIco(sizes, pngs));
Console.WriteLine($"Wrote {OutputPath} ({new FileInfo(OutputPath).Length} bytes, {sizes.Length} sizes)");

Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath)!);
File.WriteAllBytes(PreviewPath, pngs[^1]);
Console.WriteLine($"Wrote {PreviewPath} ({new FileInfo(PreviewPath).Length} bytes)");
return 0;

static byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.PixelOffsetMode   = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);
        Draw(g, size);
    }
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static void Draw(Graphics g, int size)
{
    // Single accent: a saturated mid-blue, readable on both light and dark Win11 taskbars.
    var accent = Color.FromArgb(255, 37, 99, 235); // #2563EB

    float margin    = MathF.Max(1f, size * 0.09f);
    float radius    = MathF.Max(1f, size * 0.14f);
    float thickness = MathF.Max(1f, size * 0.085f);

    var screen = new RectangleF(margin, margin, size - 2 * margin, size - 2 * margin);

    using var screenPath = RoundedRect(screen, radius);

    // Window peeking out the right edge of the screen.
    // Its left edge sits inside the screen at ~60%, its right edge extends past the
    // screen's right edge — clipped, so only the "still on screen" portion is drawn.
    float winH = size * 0.46f;
    float winY = (size - winH) / 2f;
    float winLeft  = margin + (screen.Width * 0.62f);
    float winRight = screen.Right + size * 0.12f;
    var window = new RectangleF(winLeft, winY, winRight - winLeft, winH);

    // 1. Fill the window first, clipped to the screen interior, so it looks docked-and-hiding.
    var prevClip = g.Clip;
    g.SetClip(screenPath);
    using (var brush = new SolidBrush(accent))
        g.FillRectangle(brush, window);
    g.Clip = prevClip;

    // 2. Then draw the screen frame on top, so the window's right edge is clipped by it.
    using (var pen = new Pen(accent, thickness) { Alignment = PenAlignment.Inset, LineJoin = LineJoin.Round })
        g.DrawPath(pen, screenPath);
}

static GraphicsPath RoundedRect(RectangleF r, float radius)
{
    radius = MathF.Min(radius, MathF.Min(r.Width, r.Height) / 2f);
    float d = radius * 2f;
    var path = new GraphicsPath();
    path.AddArc(r.Left,        r.Top,         d, d, 180, 90);
    path.AddArc(r.Right - d,   r.Top,         d, d, 270, 90);
    path.AddArc(r.Right - d,   r.Bottom - d,  d, d,   0, 90);
    path.AddArc(r.Left,        r.Bottom - d,  d, d,  90, 90);
    path.CloseFigure();
    return path;
}

static byte[] PackIco(int[] sizes, byte[][] pngs)
{
    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);

    bw.Write((ushort)0);             // Reserved
    bw.Write((ushort)1);             // Type: 1 = ICO
    bw.Write((ushort)pngs.Length);   // Count

    int offset = 6 + 16 * pngs.Length;
    for (int i = 0; i < pngs.Length; i++)
    {
        int s = sizes[i];
        bw.Write((byte)(s >= 256 ? 0 : s));   // Width  (0 means 256)
        bw.Write((byte)(s >= 256 ? 0 : s));   // Height
        bw.Write((byte)0);                     // Palette colors
        bw.Write((byte)0);                     // Reserved
        bw.Write((ushort)1);                   // Color planes
        bw.Write((ushort)32);                  // Bits per pixel
        bw.Write((uint)pngs[i].Length);        // Image data length
        bw.Write((uint)offset);                // Image data offset
        offset += pngs[i].Length;
    }

    foreach (var png in pngs)
        bw.Write(png);

    return ms.ToArray();
}
