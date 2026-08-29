# Reading game files with Lumina

Memory gives you numeric IDs (item id, dye id, icon id). Names, icons, and other
static data come from the game's `sqpack` files, read with **Lumina**.

- NuGet: `Lumina` + `Lumina.Excel`.
- `sqpack` path = `<game folder>/sqpack`. Derive it from the process:
  `Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "sqpack")`.

## Minimal setup

```csharp
using Lumina; using Lumina.Data; using Lumina.Excel; using Lumina.Excel.Sheets;

// Speed up the (slow) preload by dropping categories you don't touch.
foreach (var cat in Repository.CategoryNameToIdMap.Keys.ToList())
    if (cat != "ui" && cat != "exd")
        Repository.CategoryNameToIdMap.Remove(cat);

var lumina = new GameData(sqpackPath, new() {
    DefaultExcelLanguage = Language.Japanese,   // international servers; pick per data
    LoadMultithreaded = true,
});
var sItem = lumina.GetExcelSheet<Item>()!;
```

**Language matters.** International (Global) clients have no Chinese sheets;
requesting `ChineseSimplified` there stalls waiting on a console prompt. In a GUI,
always pass an explicit language the client actually ships (JP/EN/DE/FR/KR).

## Item name — one line

```csharp
string Name(uint id) => sItem.HasRow(id) ? sItem.GetRow(id).Name.ToString() : $"#{id}";
```

## Item icon → `<img>`-ready data URI

Icons are `.tex` files. Decode to raw BGRA pixels, prepend a BMP header, base64 it:

```csharp
string? IconUri(uint itemId, bool hq)
{
    if (!sItem.HasRow(itemId)) return null;
    var iconId  = sItem.GetRow(itemId).Icon.ToString().PadLeft(6, '0');
    var path    = $"ui/icon/{iconId[..3]}000/{(hq ? "hq/" : "")}{iconId}.tex";
    var uiPack  = lumina.Repositories["ffxiv"].Categories[6][0];
    var file    = uiPack.GetFile<Lumina.Data.Files.TexFile>(GameData.GetFileHash(path));
    if (file?.ImageData is not { } px) return null;      // ImageData = BGRA bytes
    var bmp = new byte[BmpHeader.Length + px.Length];
    BmpHeader.CopyTo(bmp, 0); px.CopyTo(bmp, BmpHeader.Length);
    return "data:image/bmp;base64," + Convert.ToBase64String(bmp);
}
```

`BmpHeader` is a fixed 32bpp top-down DIB header (with a dummy color-space field so
Firefox/WebView accepts the alpha mask). Copy it from `assets/`-style code or the
FFXIV dresser projects — it's a constant byte[]. Cache results; decoding a `.tex`
per render is wasteful.

## Custom sheet columns Lumina doesn't expose

Define a `[Sheet("Name")]` struct implementing `IExcelRow<T>` and read columns by
offset. Example for dye colors:

```csharp
[Sheet("Stain")]
readonly struct Stain(ExcelPage page, uint offset, uint row) : IExcelRow<Stain>
{
    public uint RowId => row;
    public uint Color => page.ReadUInt32(offset + 8);        // RGB, format as X6 for CSS
    static Stain IExcelRow<Stain>.Create(ExcelPage p, uint o, uint r) => new(p, o, r);
}
// var dyeColors = lumina.GetExcelSheet<Stain>()!.ToDictionary(s => s.RowId, s => s.Color.ToString("X6"));
```

For raw/unknown sheets use `lumina.GetExcelSheet<RawRow>(name: "SheetName")` and
`row.ReadColumn(i)`.

## Version skew

Lumina reads whatever `sqpack` is on disk, so it always matches the installed
client — no patch-day breakage like signatures have. If a sheet or column moved,
update the `Lumina.Excel` package (its generated `Sheets` track game updates).
