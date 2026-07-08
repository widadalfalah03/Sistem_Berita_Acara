using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DocumentService> _logger;
    private readonly string _outputRoot;
    private readonly string _templatesDir;
    private readonly IConfiguration _config;

    public DocumentService(AppDbContext db, ILogger<DocumentService> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _config = config;
        _outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles", "documents");
        _templatesDir = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles", "templates");
        Directory.CreateDirectory(_outputRoot);
        Directory.CreateDirectory(_templatesDir);
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "AppFiles", "signatures"));
    }

    private string GetTemplatePath(string jenis)
    {
        string jenisUpper = (jenis ?? "ALOKASI").ToUpper();
        if (jenisUpper == "LAINNYA")
        {
            return Path.Combine(_templatesDir, "Template_BA_LAINNYA.docx");
        }

        if (jenisUpper != "ALOKASI" && jenisUpper != "PEMINJAMAN" && jenisUpper != "PENARIKAN")
        {
            jenisUpper = "ALOKASI";
        }
        
        return Path.Combine(_templatesDir, $"Template_BA_{jenisUpper}.docx");
    }

    public async Task<string> GenerateDocxAsync(BeritaAcara ba)
    {
        var templatePath = GetTemplatePath(ba.Jenis ?? "Alokasi");
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template DOCX tidak ditemukan: {Path.GetFileName(templatePath)}. Pastikan file ada di AppFiles/templates/");
        }

        string relativePath = GetRelativePath($"ba-{ba.Id}-draft.docx");
        string physicalPath = GetPhysicalPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        File.Copy(templatePath, physicalPath, true);

        using (var wordDoc = WordprocessingDocument.Open(physicalPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;

            var idCulture = new CultureInfo("id-ID");

            var jenisPerangkatStr = (ba.Perangkat != null && ba.Perangkat.Any())
                ? string.Join(", ", ba.Perangkat.Select(p => p.Barang?.NamaBarang ?? "-").Distinct())
                : "-";

            var tanggalKembaliStr = ba.Jenis == "Peminjaman"
                ? (ba.TanggalKembali.HasValue
                    ? ba.TanggalKembali.Value.ToString("dd MMMM yyyy", idCulture)
                    : "-")
                : string.Empty;

            string jabatanReviewer;
            if (!string.IsNullOrWhiteSpace(ba.Mengetahui?.Jabatan))
            {
                jabatanReviewer = ba.Mengetahui.Jabatan;
            }
            else if (ba.MengetahuiId > 0)
            {
                var mengetahuiUser = _db.Users
                    .Join(_db.Pegawai, u => u.PegawaiId, p => p.Id, (u, p) => new { u, p })
                    .Where(x => x.u.Id == ba.MengetahuiId)
                    .Select(x => new { x.u.Jabatan, PegawaiJabatan = x.p.Jabatan })
                    .FirstOrDefault();
                jabatanReviewer = mengetahuiUser?.Jabatan
                               ?? mengetahuiUser?.PegawaiJabatan
                               ?? "-";
            }
            else
            {
                jabatanReviewer = "-";
            }

            var tujuanStr = !string.IsNullOrWhiteSpace(ba.Pj?.FungsiDirektorat) ? ba.Pj.FungsiDirektorat : "-";

            var replacements = new Dictionary<string, string>
            {
                { "BERITA ACARA ALOKASI BARANG", ba.Jenis == "Lainnya" ? (ba.JenisCustom?.ToUpper() ?? "BERITA ACARA LAINNYA") : "BERITA ACARA ALOKASI BARANG" },
                { "{{NomorSurat}}", ba.NomorSurat ?? "Draft" },
                { "{{Tanggal}}", ba.Tanggal.ToString("dd MMMM yyyy", idCulture) },
                { "{{HariTanggal}}", ba.Tanggal.ToString("dddd", idCulture) },
                { "{{TanggalAngka}}", ba.Tanggal.Day.ToString() },
                { "{{BulanTanggal}}", ba.Tanggal.ToString("MMMM", idCulture) },
                { "{{TahunTanggal}}", ba.Tanggal.Year.ToString() },
                { "{{NamaPJ}}", ba.Pj?.Nama ?? "-" },
                { "{{CostCenter}}", ba.Pj?.CostCenter ?? "-" },
                { "{{JabatanPJ}}", ba.Pj?.Jabatan ?? "-" },
                { "{{FungsiPJ}}", ba.Pj?.FungsiDirektorat ?? "-" },
                { "{{EmailPJ}}", ba.Pj?.Email ?? "-" },
                { "{{NoPekerjaPJ}}", ba.Pj?.NoPekerja ?? "-" },
                { "{{NoTelpPJ}}", ba.PjNoTelp ?? ba.Pj?.NoTelp ?? "-" },
                { "{{Menyerahkan}}", ba.Menyerahkan?.Nama ?? "-" },
                { "{{Reviewer}}", ba.Mengetahui?.Nama ?? "-" },
                { "{{JabatanReviewer}}", jabatanReviewer },
                { "{{NamaPengguna}}", ba.PengunaAlihDaya ?? "-" },
                { "{{TujuanBA}}", tujuanStr },
                { "{{JenisPerangkat}}", jenisPerangkatStr },
                { "{{TiketSscNo}}", ba.TiketSscNo ?? "-" },
                { "{{TanggalKembali}}", tanggalKembaliStr },
                { "{{Keterangan}}", ba.Keterangan ?? "" },
                { "Tiket di My SSC NO.", "Nota Dinas/Tiket di My SSC No." },
            };

            if (!string.IsNullOrWhiteSpace(ba.PengunaAlihDaya))
            {
                replacements.Add("Nama Penanggung Jawab", "Nama Pengguna/Penanggung Jawab");
            }

            int namaPjCount = 0;

            foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>().ToList())
            {
                if (string.IsNullOrWhiteSpace(ba.TiketSscNo))
                {
                    var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                    if (fullText.Contains("Catatan: Perangkat IT ini dialokasikan") ||
                        fullText.Contains("Catatan: Perangkat IT ini ditarik") ||
                        fullText.Contains("Nota Dinas/Tiket di My SSC") ||
                        fullText.Contains("Nota Dinas"))
                    {
                        para.RemoveAllChildren<Run>();
                        continue;
                    }
                }
                NormalizeParagraphPlaceholders(para, replacements, ref namaPjCount, ba);
            }

            
            foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>())
            {
                var firstRun = para.Elements<Run>().FirstOrDefault();
                if (firstRun == null) continue;
                var firstText = firstRun.GetFirstChild<Text>();
                if (firstText == null) continue;

                const string tanggalMarker = "Tanggal pengembalian:";
                string text = firstText.Text;
                if (!text.Contains(tanggalMarker)) continue;

                int markerIdx = text.IndexOf(tanggalMarker);
                string beforeMarker = text[..markerIdx].TrimEnd();
                string afterMarker = text[markerIdx..];

                firstText.Text = beforeMarker;
                if (beforeMarker.Length > 0 && beforeMarker[^1] == ' ')
                    firstText.Space = SpaceProcessingModeValues.Preserve;

                var tabRun = new Run();
                if (firstRun.RunProperties != null)
                    tabRun.RunProperties = (RunProperties)firstRun.RunProperties.CloneNode(true);
                tabRun.AppendChild(new TabChar());
                firstRun.InsertAfterSelf(tabRun);

                var dateRun = new Run();
                if (firstRun.RunProperties != null)
                    dateRun.RunProperties = (RunProperties)firstRun.RunProperties.CloneNode(true);
                dateRun.AppendChild(new Text(afterMarker));
                tabRun.InsertAfterSelf(dateRun);

                var pPr = para.ParagraphProperties ?? new ParagraphProperties();
                if (para.ParagraphProperties == null) para.InsertAt(pPr, 0);
                var tabs = pPr.GetFirstChild<Tabs>() ?? new Tabs();
                if (pPr.GetFirstChild<Tabs>() == null) pPr.Append(tabs);
                tabs.AppendChild(new TabStop { Val = TabStopValues.Right, Position = 9160 });

                break;
            }

            foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>())
            {
                var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                if (fullText.Contains("{{SIG_"))
                {
                    foreach (var run in para.Descendants<Run>())
                    {
                        if (run.RunProperties == null) run.RunProperties = new RunProperties();
                        run.RunProperties.Color = new Color { Val = "FFFFFF" };
                    }
                }
            }

            foreach (var table in mainPart.Document.Body!.Descendants<Table>())
            {
                var tblPr = table.Elements<TableProperties>().FirstOrDefault();
                if (tblPr == null)
                {
                    tblPr = new TableProperties();
                    table.InsertAt(tblPr, 0);
                }

                var layout = tblPr.Elements<TableLayout>().FirstOrDefault();
                if (layout == null)
                    tblPr.Append(new TableLayout { Type = TableLayoutValues.Fixed });
                else
                    layout.Type = TableLayoutValues.Fixed;

                var fullTableText = string.Concat(table.Descendants<Text>().Select(t => t.Text));
                if (fullTableText.Contains("{{SIG_"))
                {
                    var tblBorders = tblPr.Elements<TableBorders>().FirstOrDefault();
                    if (tblBorders == null)
                    {
                        tblBorders = new TableBorders();
                        tblPr.Append(tblBorders);
                    }
                    tblBorders.TopBorder = new TopBorder { Val = BorderValues.Nil };
                    tblBorders.BottomBorder = new BottomBorder { Val = BorderValues.Nil };
                    tblBorders.LeftBorder = new LeftBorder { Val = BorderValues.Nil };
                    tblBorders.RightBorder = new RightBorder { Val = BorderValues.Nil };
                    tblBorders.InsideHorizontalBorder = new InsideHorizontalBorder { Val = BorderValues.Nil };
                    tblBorders.InsideVerticalBorder = new InsideVerticalBorder { Val = BorderValues.Nil };


                }
            }

            var templateRow = mainPart.Document.Body.Descendants<TableRow>()
                .FirstOrDefault(r => r.Descendants<Text>().Any(t => t.Text != null && t.Text.Contains("{{PerangkatJumlah}}")));

            if (templateRow != null && ba.Perangkat != null)
            {
                int idx = 1;
                foreach (var p in ba.Perangkat)
                {
                    var newRow = (TableRow)templateRow.CloneNode(true);

                    var noCell = newRow.Elements<TableCell>().FirstOrDefault();
                    if (noCell != null)
                    {
                        foreach (var t in noCell.Descendants<Text>())
                        {
                            if (t.Text != null && t.Text.Contains("{{PerangkatNo}}"))
                                t.Text = t.Text.Replace("{{PerangkatNo}}", idx.ToString());
                        }
                    }

                    foreach (var text in newRow.Descendants<Text>())
                    {
                        if (text.Text == null) continue;
                        
                        if (text.Text.Contains("{{PerangkatJumlah}}"))
                            text.Text = text.Text.Replace("{{PerangkatJumlah}}", p.Jumlah.ToString());
                        if (text.Text.Contains("{{PerangkatTerbilang}}"))
                            text.Text = text.Text.Replace("{{PerangkatTerbilang}}", Terbilang(p.Jumlah));
                        if (text.Text.Contains("{{PerangkatSatuan}}"))
                            text.Text = text.Text.Replace("{{PerangkatSatuan}}", p.Satuan ?? "Pcs");
                        if (text.Text.Contains("{{PerangkatSN}}"))
                            text.Text = text.Text.Replace("{{PerangkatSN}}", string.IsNullOrWhiteSpace(p.NoSerial) ? "-" : p.NoSerial);
                        if (text.Text.Contains("{{PerangkatKeterangan}}"))
                            text.Text = text.Text.Replace("{{PerangkatKeterangan}}", p.Keterangan ?? "-");
                    }

                    templateRow.InsertBeforeSelf(newRow);
                    idx++;
                }
                templateRow.Remove();
            }

            var validFotos = new List<(string physical, string ext)>();
            if (ba.BuktiFotos != null)
            {
                foreach (var foto in ba.BuktiFotos)
                {
                    var physical = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles",
                        foto.FilePath.TrimStart('/').Replace("files/", "").Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(physical)) continue;
                    var ext = Path.GetExtension(physical).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        _logger.LogWarning("[GenerateDocx] Format gambar tidak didukung ({ext}), melewati foto: {path}", ext, physical);
                        continue;
                    }
                    validFotos.Add((physical, ext));
                }
            }

            var drawingParas = mainPart.Document.Body!.Descendants<Paragraph>()
                .Where(p => p.Descendants<Drawing>().Any()
                         && string.IsNullOrWhiteSpace(p.InnerText)
                         && !p.Ancestors<TableCell>().Any())
                .ToList();

            var sectPr = mainPart.Document.Body!.Elements<SectionProperties>().LastOrDefault();

            if (drawingParas.Any())
            {
                if (!validFotos.Any())
                {
                    
                    var pageBreakPara = drawingParas[0].PreviousSibling<Paragraph>();
                    if (pageBreakPara != null && pageBreakPara.Descendants<Break>()
                            .Any(b => b.Type == BreakValues.Page))
                        pageBreakPara.Remove();
                }

                foreach (var dp in drawingParas) dp.Remove();

                if (validFotos.Any())
                    EmbedFotosGrid(mainPart, validFotos, sectPr);
            }
            else if (ba.Jenis != "Lainnya" && validFotos.Any())
            {
                EmbedFotosGrid(mainPart, validFotos, sectPr);
            }

            mainPart.Document.Save();
        }

        try
        {
            string pdfPhysicalPath = physicalPath.Replace(".docx", ".pdf");
            await ConvertDocxToPdfInternalAsync(physicalPath, pdfPhysicalPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gagal membuat PDF preview");
        }

        return relativePath;
    }

    public async Task PatchNomorSuratAsync(int baId, string nomorSurat)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId)
            ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
            
        string targetRelativePath = !string.IsNullOrEmpty(ba.DocxFinalPath) ? ba.DocxFinalPath : ba.DocxPath!;
        string physicalPath = GetPhysicalPath(targetRelativePath);
        EnsureFileExists(physicalPath);

        using (var wordDoc = WordprocessingDocument.Open(physicalPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;
            bool changed = false;

            foreach (var text in mainPart.Document.Body!.Descendants<Text>())
            {
                if (text.Text == "Draft")
                {
                    text.Text = nomorSurat;
                    changed = true;
                }
            }

            if (changed)
                mainPart.Document.Save();
        }

        try
        {
            await ConvertDocxToPdfInternalAsync(physicalPath, physicalPath.Replace(".docx", ".pdf"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PatchNomorSurat] Gagal regenerasi PDF");
        }
    }

    public async Task<string> EmbedTtdMenyerahkanAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        if (string.IsNullOrEmpty(ba.DocxPath))
            throw new InvalidOperationException($"BA {baId} belum memiliki dokumen (DocxPath kosong). Generate dokumen terlebih dahulu.");
        string physicalPath = GetPhysicalPath(ba.DocxPath);
        EnsureFileExists(physicalPath);

        await EmbedSignatureSpireAsync(physicalPath, ttdPath, "{{SIG_MENYERAHKAN}}");

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdPreviewReviewerAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        if (string.IsNullOrEmpty(ba.DocxPath))
            throw new InvalidOperationException($"BA {baId} belum memiliki dokumen (DocxPath kosong).");
        string physicalPath = GetPhysicalPath(ba.DocxPath);
        EnsureFileExists(physicalPath);

        await EmbedSignatureSpireAsync(physicalPath, ttdPath, "{{SIG_REVIEWER}}");

        return ba.DocxPath;
    }

    public async Task<string> EmbedTtdPjAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        if (string.IsNullOrEmpty(ba.DocxPath) && string.IsNullOrEmpty(ba.DocxFinalPath))
            throw new InvalidOperationException($"BA {baId} belum memiliki dokumen (DocxPath/DocxFinalPath kosong).");
            
        string targetRelativePath = !string.IsNullOrEmpty(ba.DocxFinalPath) ? ba.DocxFinalPath : ba.DocxPath!;
        string physicalPath = GetPhysicalPath(targetRelativePath);
        EnsureFileExists(physicalPath);

        await EmbedSignatureSpireAsync(physicalPath, ttdPath, "{{SIG_PJ}}");
        await ConvertDocxToPdfAsync(physicalPath);

        if (ttdPath.Contains("auto_approve_stamp.png"))
        {
            ba.TtdPjPath = "images/auto_approve_stamp.png";
        }
        else
        {
            ba.TtdPjPath = Path.Combine("files", "signatures", Path.GetFileName(ttdPath)).Replace("\\", "/");
        }
        await _db.SaveChangesAsync();

        return ba.DocxPath;
    }

    public async Task<string> EmbedTtdMengetahuiAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        if (string.IsNullOrEmpty(ba.DocxPath))
            throw new InvalidOperationException($"BA {baId} belum memiliki dokumen (DocxPath kosong).");
        string draftPhysicalPath = GetPhysicalPath(ba.DocxPath);
        EnsureFileExists(draftPhysicalPath);

        string finalRelativePath = GetRelativePath($"ba-{baId}-final.docx");
        string finalPhysicalPath = GetPhysicalPath(finalRelativePath);
        File.Copy(draftPhysicalPath, finalPhysicalPath, true);

        await EmbedSignatureSpireAsync(finalPhysicalPath, ttdPath, "{{SIG_REVIEWER}}");
        await ConvertDocxToPdfAsync(finalPhysicalPath);

        ba.DocxFinalPath = finalRelativePath;
        await _db.SaveChangesAsync();

        return finalRelativePath;
    }

    private async Task EmbedSignatureSpireAsync(string docPath, string imagePath, string placeholder)
    {
        if (!File.Exists(imagePath)) throw new FileNotFoundException("File tanda tangan tidak ditemukan.", imagePath);

        using (var wordDoc = WordprocessingDocument.Open(docPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;

            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            var imgPartType = ext == ".png" ? ImagePartType.Png : ImagePartType.Jpeg;
            var imgPart = mainPart.AddImagePart(imgPartType);
            using (var fs = File.OpenRead(imagePath)) imgPart.FeedData(fs);
            var imgId = mainPart.GetIdOfPart(imgPart);

            long cx = 140L * 12700L;
            long cy =  70L * 12700L;
            var dims = GetImageDimensions(imagePath);
            if (dims.width > 0 && dims.height > 0)
            {
                double targetRatio = 140.0 / 70.0;
                double imageRatio = (double)dims.width / dims.height;

                if (imageRatio > targetRatio)
                {
                    cx = 140L * 12700L;
                    cy = (long)((140.0 / imageRatio) * 12700L);
                }
                else
                {
                    cy = 70L * 12700L;
                    cx = (long)((70.0 * imageRatio) * 12700L);
                }
            }

            uint randomId = (uint)Random.Shared.Next(1000, 100000);

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = randomId, Name = "signature" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = randomId + 1, Name = "sig.png" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = imgId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = cx, Cy = cy }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                        { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                {
                    DistanceFromTop    = 0U,
                    DistanceFromBottom = 0U,
                    DistanceFromLeft   = 0U,   
                    DistanceFromRight  = 0U
                });

            foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>())
            {
                var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                if (!fullText.Contains(placeholder)) continue;


                var pPr = para.ParagraphProperties;
                if (pPr != null)
                {
                    pPr.Remove();
                }

                long cy_pts = cy / 12700L;
                long missing_cy_pts = 70L - cy_pts;
                long spacing_after_twips = missing_cy_pts * 20L;

                para.ParagraphProperties = new ParagraphProperties(
                    new Indentation { Left = "0" },
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = spacing_after_twips.ToString() }
                );

                foreach (var run in para.Elements<Run>().ToList())
                    run.Remove();

                var imgRun = new Run((Drawing)drawing.CloneNode(true));
                para.Append(imgRun);

                var parentTable = para.Ancestors<Table>().FirstOrDefault();
                if (parentTable != null)
                {
                    var tblPr = parentTable.Elements<TableProperties>().FirstOrDefault();
                    if (tblPr != null)
                    {
                        var layout = tblPr.Elements<TableLayout>().FirstOrDefault();
                        if (layout == null)
                            tblPr.Append(new TableLayout { Type = TableLayoutValues.Fixed });
                        else
                            layout.Type = TableLayoutValues.Fixed;
                    }
                }

                break;
            }

            mainPart.Document.Save();
        }

        try
        {
            await ConvertDocxToPdfInternalAsync(docPath, docPath.Replace(".docx", ".pdf"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EmbedSignatureSpire] Gagal konversi PDF setelah embed TTD");
        }
    }

    public Task ConvertDocxToPdfAsync(string docxPhysicalPath)
        => ConvertDocxToPdfInternalAsync(docxPhysicalPath, docxPhysicalPath.Replace(".docx", ".pdf"));

    private Task ConvertDocxToPdfInternalAsync(string docxPath, string pdfPath)
    {
        try
        {
            using var doc = new Spire.Doc.Document();
            doc.LoadFromFile(docxPath);
            doc.SaveToFile(pdfPath, Spire.Doc.FileFormat.PDF);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FreeSpire.Doc PDF conversion failed for {path}", docxPath);
        }
        return Task.CompletedTask;
    }

    private void EmbedFotosGrid(MainDocumentPart mainPart, List<(string physical, string ext)> fotos, SectionProperties? insertBefore)
    {
        int n = fotos.Count;
        if (n == 0) return;

        
        int cols = n switch { 1 => 1, 2 => 2, <= 3 => 3, 4 => 2, <= 6 => 3, _ => 4 };
        int rows = (int)Math.Ceiling((double)n / cols);

        
        const long CONTENT_W    = 5_731_200L; 
        const long CONTENT_H    = 7_500_000L; 
        const long ROW_GAP      = 200_000L;   
        const int  H_PAD_DXA    = 80;         

        int tableW_dxa = (int)(CONTENT_W / 635);
        int cellW_dxa  = tableW_dxa / cols;
        long imgAreaW  = ((long)cellW_dxa - 2 * H_PAD_DXA) * 635L;
        long imgAreaH  = (CONTENT_H - ROW_GAP * (rows - 1)) / rows;
        int rowGap_dxa = (int)(ROW_GAP / 635);

        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = tableW_dxa.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableJustification { Val = TableRowAlignmentValues.Center },
            new TableBorders(
                new TopBorder              { Val = BorderValues.None, Size = 0 },
                new BottomBorder           { Val = BorderValues.None, Size = 0 },
                new LeftBorder             { Val = BorderValues.None, Size = 0 },
                new RightBorder            { Val = BorderValues.None, Size = 0 },
                new InsideHorizontalBorder { Val = BorderValues.None, Size = 0 },
                new InsideVerticalBorder   { Val = BorderValues.None, Size = 0 })));

        uint imgId = (uint)Random.Shared.Next(2000, 200000);
        int idx = 0;

        for (int r = 0; r < rows; r++)
        {
            var tr = new TableRow();
            string spacingAfter = (r < rows - 1 ? rowGap_dxa : 0).ToString();

            for (int c = 0; c < cols; c++)
            {
                var tc = new TableCell();
                tc.AppendChild(new TableCellProperties(
                    new TableCellWidth { Width = cellW_dxa.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableCellBorders(
                        new TopBorder    { Val = BorderValues.None, Size = 0 },
                        new BottomBorder { Val = BorderValues.None, Size = 0 },
                        new LeftBorder   { Val = BorderValues.None, Size = 0 },
                        new RightBorder  { Val = BorderValues.None, Size = 0 }),
                    new TableCellMargin(
                        new TopMargin    { Width = "0",                  Type = TableWidthUnitValues.Dxa },
                        new BottomMargin { Width = "0",                  Type = TableWidthUnitValues.Dxa },
                        new LeftMargin   { Width = H_PAD_DXA.ToString(), Type = TableWidthUnitValues.Dxa },
                        new RightMargin  { Width = H_PAD_DXA.ToString(), Type = TableWidthUnitValues.Dxa }),
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));

                if (idx < n)
                {
                    var (physical, ext) = fotos[idx++];
                    var partType = ext == ".png" ? ImagePartType.Png : ImagePartType.Jpeg;
                    var imgPart  = mainPart.AddImagePart(partType);
                    using (var fs = File.OpenRead(physical)) imgPart.FeedData(fs);

                    var (w, h) = GetImageDimensions(physical);
                    long cx = imgAreaW, cy = imgAreaH;
                    if (w > 0 && h > 0)
                    {
                        double ratio     = (double)w / h;
                        double areaRatio = (double)imgAreaW / imgAreaH;
                        if (ratio >= areaRatio) { cx = imgAreaW; cy = (long)(imgAreaW / ratio); }
                        else                    { cy = imgAreaH; cx = (long)(imgAreaH * ratio); }
                    }

                    var drawing = CreateImageDrawingWithId(mainPart.GetIdOfPart(imgPart), cx, cy, Path.GetFileName(physical), imgId);
                    imgId += 2;

                    tc.Append(new Paragraph(
                        new ParagraphProperties(
                            new Justification { Val = JustificationValues.Center },
                            new SpacingBetweenLines { Before = "0", After = spacingAfter }),
                        new Run(drawing)));
                }
                else
                {
                    tc.Append(new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { Before = "0", After = spacingAfter })));
                }
                tr.AppendChild(tc);
            }
            table.AppendChild(tr);
        }

        var body = mainPart.Document.Body!;
        if (insertBefore != null) body.InsertBefore(table, insertBefore);
        else body.Append(table);
    }

    private string GetRelativePath(string fileName) => Path.Combine("files", "documents", fileName).Replace("\\", "/");
    private string GetPhysicalPath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            throw new InvalidOperationException("Path dokumen belum tersimpan (DocxPath kosong). Pastikan dokumen berhasil di-generate terlebih dahulu.");
        return Path.Combine(Directory.GetCurrentDirectory(), "AppFiles", relativePath.Replace("files/", "").Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File DOCX tidak ditemukan.", path);
    }

    private static void NormalizeParagraphPlaceholders(Paragraph para, Dictionary<string, string> replacements, ref int namaPjCount, BeritaAcara ba)
    {
        var runs = para.Elements<Run>().ToList();
        if (runs.Count == 0) return;

        var textNodes = runs
            .SelectMany((r, ri) => r.Elements<Text>().Select(t => (RunIndex: ri, TextNode: t)))
            .ToList();

        if (textNodes.Count == 0) return;

        var combined = string.Concat(textNodes.Select(x => x.TextNode.Text));

        bool hasReplacement = replacements.Any(r => combined.Contains(r.Key));
        if (!hasReplacement) return;

        foreach (var r in replacements)
        {
            if (r.Key == "{{NamaPJ}}" && combined.Contains("{{NamaPJ}}"))
            {
                namaPjCount++;
                string replacementValue = r.Value;
                if (namaPjCount == 1 && !string.IsNullOrWhiteSpace(ba.PengunaAlihDaya))
                {
                    replacementValue = $"{ba.PengunaAlihDaya} / {ba.Pj?.Nama ?? "-"}";
                }
                combined = combined.Replace(r.Key, replacementValue);
            }
            else
            {
                combined = combined.Replace(r.Key, r.Value);
            }
        }

        var firstText = textNodes[0].TextNode;
        firstText.Text = combined;
        if (combined.Length > 0 && (combined[0] == ' ' || combined[^1] == ' '))
            firstText.Space = SpaceProcessingModeValues.Preserve;

        for (int i = 1; i < textNodes.Count; i++)
            textNodes[i].TextNode.Text = string.Empty;
    }

    private static (int width, int height) GetImageDimensions(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            var header = new byte[8];
            fs.Read(header, 0, 8);

            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                fs.Seek(16, SeekOrigin.Begin);
                var buf = new byte[8];
                fs.Read(buf, 0, 8);
                int width = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
                int height = (buf[4] << 24) | (buf[5] << 16) | (buf[6] << 8) | buf[7];
                return (width, height);
            }
            else if (header[0] == 0xFF && header[1] == 0xD8)
            {
                fs.Seek(2, SeekOrigin.Begin);
                while (fs.Position < fs.Length)
                {
                    int marker = fs.ReadByte();
                    if (marker != 0xFF) break;
                    marker = fs.ReadByte();
                    if (marker == 0xC0 || marker == 0xC1 || marker == 0xC2)
                    {
                        fs.Seek(3, SeekOrigin.Current);
                        var buf = new byte[4];
                        fs.Read(buf, 0, 4);
                        int height = (buf[0] << 8) | buf[1];
                        int width = (buf[2] << 8) | buf[3];
                        return (width, height);
                    }
                    else
                    {
                        var lenBuf = new byte[2];
                        fs.Read(lenBuf, 0, 2);
                        int len = (lenBuf[0] << 8) | lenBuf[1];
                        fs.Seek(len - 2, SeekOrigin.Current);
                    }
                }
            }
        }
        catch { }
        return (0, 0);
    }

    private static string Terbilang(int angka)
    {
        string[] huruf = { "", "Satu", "Dua", "Tiga", "Empat", "Lima", "Enam", "Tujuh", "Delapan", "Sembilan", "Sepuluh", "Sebelas" };
        if (angka < 12) return huruf[angka];
        if (angka < 20) return Terbilang(angka - 10) + " Belas";
        if (angka < 100) return Terbilang(angka / 10) + " Puluh " + Terbilang(angka % 10);
        if (angka < 200) return "Seratus " + Terbilang(angka - 100);
        if (angka < 1000) return Terbilang(angka / 100) + " Ratus " + Terbilang(angka % 100);
        if (angka < 2000) return "Seribu " + Terbilang(angka - 1000);
        if (angka < 1000000) return Terbilang(angka / 1000) + " Ribu " + Terbilang(angka % 1000);
        return angka.ToString();
    }

    private static Drawing CreateImageDrawingWithId(string relationshipId, long widthEmu, long heightEmu, string name, uint id)
    {
        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)(id + 1), Name = name },
                new PIC.NonVisualPictureDrawingProperties()),
            new PIC.BlipFill(
                new A.Blip { Embed = relationshipId },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0L, Y = 0L },
                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })
        );

        var graphicData = new A.GraphicData(picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        var graphic = new A.Graphic(graphicData);
        var inline = new DW.Inline(
            new DW.Extent { Cx = widthEmu, Cy = heightEmu },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = (UInt32Value)id, Name = name },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = (UInt32Value)0U, DistanceFromBottom = (UInt32Value)0U,
            DistanceFromLeft = (UInt32Value)0U, DistanceFromRight = (UInt32Value)0U
        };

        return new Drawing(inline);
    }

    public async Task<string> GenerateLainnyaDocxAsync(BeritaAcara ba)
    {
        var docFolder = Path.Combine(Directory.GetCurrentDirectory(), "AppFiles", "documents");
        Directory.CreateDirectory(docFolder);
        var fileName = $"BA-Lainnya-{ba.Id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.docx";
        var fullPath = Path.Combine(docFolder, fileName);
        var relPath = $"files/documents/{fileName}";

        string judul = ba.JenisCustom ?? "Lainnya";
        string nomorSurat = "Draft";

        await Task.Run(() =>
        {
            using var doc = WordprocessingDocument.Create(fullPath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            var sectPr = new SectionProperties(
                new PageMargin { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440 }
            );

            void AddCenteredText(string text, int sizeHalfPt, bool bold = false, int spaceBeforePt = 0)
            {
                var para = new Paragraph();
                var pPr = new ParagraphProperties();
                pPr.Justification = new Justification { Val = JustificationValues.Center };
                if (spaceBeforePt > 0)
                    pPr.SpacingBetweenLines = new SpacingBetweenLines { Before = (StringValue)(spaceBeforePt * 20).ToString() };
                para.Append(pPr);

                var run = new Run();
                var rPr = new RunProperties();
                rPr.FontSize = new FontSize { Val = sizeHalfPt.ToString() };
                rPr.FontSizeComplexScript = new FontSizeComplexScript { Val = sizeHalfPt.ToString() };
                if (bold) rPr.Bold = new Bold();
                rPr.RunFonts = new RunFonts { Ascii = "Arial", HighAnsi = "Arial" };
                run.Append(rPr);
                run.Append(new Text(text));
                para.Append(run);
                body.Append(para);
            }

            body.Append(new Paragraph(new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { Before = "4000" }
            )));

            AddCenteredText("BERITA ACARA", sizeHalfPt: 72, bold: true, spaceBeforePt: 0);

            AddCenteredText(judul.ToUpperInvariant(), sizeHalfPt: 56, bold: true, spaceBeforePt: 20);

            AddCenteredText(nomorSurat, sizeHalfPt: 40, bold: false, spaceBeforePt: 24);

            body.Append(sectPr);
            mainPart.Document.Save();
        });

        await ConvertDocxToPdfInternalAsync(fullPath, fullPath.Replace(".docx", ".pdf"));

        return relPath;
    }
}

