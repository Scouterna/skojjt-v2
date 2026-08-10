using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

// Produces downscaled WebP thumbnails used by the Märkeskarta map view.
// The map renders badges at <= 56 px; 128 px covers retina (2x) with headroom.
// Thumbnails are written to a "thumbs" sub-folder next to their source images:
//   /img/interest-badges/varme.png  ->  /img/interest-badges/thumbs/varme.webp

const int MaxSize = 128;
const int Quality = 80;

// Resolve the web project's wwwroot relative to this tool (repo/tools/BadgeThumbnailGenerator).
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var wwwroot = Path.Combine(repoRoot, "src", "Skojjt.Web", "wwwroot");

string[] sourceDirs =
[
    Path.Combine(wwwroot, "img", "interest-badges"),
    Path.Combine(wwwroot, "img", "troop-types"),
];

var encoder = new WebpEncoder
{
    Quality = Quality,
    FileFormat = WebpFileFormatType.Lossy,
};

var total = 0;
long savedBytes = 0;

foreach (var dir in sourceDirs)
{
    if (!Directory.Exists(dir))
    {
        Console.WriteLine($"SKIP (missing): {dir}");
        continue;
    }

    var thumbsDir = Path.Combine(dir, "thumbs");
    Directory.CreateDirectory(thumbsDir);

    var pngFiles = Directory.EnumerateFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
    foreach (var png in pngFiles)
    {
        var name = Path.GetFileNameWithoutExtension(png);
        var outPath = Path.Combine(thumbsDir, name + ".webp");

        using var image = Image.Load(png);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(MaxSize, MaxSize),
        }));
        image.Save(outPath, encoder);

        var srcLen = new FileInfo(png).Length;
        var dstLen = new FileInfo(outPath).Length;
        savedBytes += srcLen - dstLen;
        total++;
        Console.WriteLine($"{name,-32} {srcLen / 1024,5} KB -> {dstLen / 1024,4} KB");
    }
}

Console.WriteLine();
Console.WriteLine($"Done. {total} thumbnails generated. Saved ~{savedBytes / 1024 / 1024.0:F2} MB.");
