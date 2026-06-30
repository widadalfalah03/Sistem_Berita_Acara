using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Microsoft.Extensions.Logging;
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

    public DocumentService(AppDbContext db, ILogger<DocumentService> logger)
    {
        _db = db;
        _logger = logger;
        _outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "documents");
        _templatesDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "templates");
        Directory.CreateDirectory(_outputRoot);
        Directory.CreateDirectory(_templatesDir);
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "signatures"));
    }

    private string GetTemplatePath(string jenis, string? pengunaAlihDaya)
    {
        string jenisUpper = (jenis ?? "ALOKASI").ToUpper();
        if (jenisUpper == "LAINNYA")
        {
            return Path.Combine(_templatesDir, "Template_BA_LAINNYA.docx");
        }

        string roleSuffix = string.IsNullOrWhiteSpace(pengunaAlihDaya) ? "Pekerja" : "TAD";
        if (jenisUpper != "ALOKASI" && jenisUpper != "PEMINJAMAN" && jenisUpper != "PENARIKAN")
        {
            jenisUpper = "ALOKASI";
        }
        var fileName = $"Template_BA_{jenisUpper}_{roleSuffix}.docx";
        return Path.Combine(_templatesDir, fileName);
    }

    public async Task<string> GenerateDocxAsync(BeritaAcara ba)
    {
        var templatePath = GetTemplatePath(ba.Jenis ?? "Alokasi", ba.PengunaAlihDaya);
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template DOCX tidak ditemukan: {Path.GetFileName(templatePath)}. Pastikan file ada di wwwroot/files/templates/");
        }

        string relativePath = GetRelativePath($"ba-{ba.Id}-draft.docx");
        string physicalPath = GetPhysicalPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        // Copy template ke file draft
        File.Copy(templatePath, physicalPath, true);

        using (var wordDoc = WordprocessingDocument.Open(physicalPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;

            // 1. Replace Simple Placeholders
            var idCulture = new CultureInfo("id-ID");

            // Jenis perangkat: join semua nama barang dari list (untuk heading tabel)
            var jenisPerangkatStr = (ba.Perangkat != null && ba.Perangkat.Any())
                ? string.Join(", ", ba.Perangkat.Select(p => p.Barang?.NamaBarang ?? "-").Distinct())
                : "-";

            // Tanggal kembali (Peminjaman only) â€” tampilkan "-" jika tidak diisi
            var tanggalKembaliStr = ba.Jenis == "Peminjaman"
                ? (ba.TanggalKembali.HasValue
                    ? ba.TanggalKembali.Value.ToString("dd MMMM yyyy", idCulture)
                    : "-")
                : string.Empty;

            // Jabatan approver â€” prefer Pegawai.Jabatan (loaded via DB if ApplicationUser.Jabatan is null)
            string jabatanApprover;
            if (!string.IsNullOrWhiteSpace(ba.Mengetahui?.Jabatan))
            {
                jabatanApprover = ba.Mengetahui.Jabatan;
            }
            else if (ba.MengetahuiId > 0)
            {
                var mengetahuiUser = _db.Users
                    .Join(_db.Pegawai, u => u.PegawaiId, p => p.Id, (u, p) => new { u, p })
                    .Where(x => x.u.Id == ba.MengetahuiId)
                    .Select(x => new { x.u.Jabatan, PegawaiJabatan = x.p.Jabatan })
                    .FirstOrDefault();
                jabatanApprover = mengetahuiUser?.Jabatan
                               ?? mengetahuiUser?.PegawaiJabatan
                               ?? "-";
            }
            else
            {
                jabatanApprover = "-";
            }

            // Tujuan BA: approver's jabatan (where the BA is addressed to)
            var tujuanStr = !string.IsNullOrWhiteSpace(jabatanApprover) ? jabatanApprover : "-";

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
                { "{{JabatanReviewer}}", jabatanApprover },
                { "{{NamaPengguna}}", ba.PengunaAlihDaya ?? "-" },
                { "{{TujuanBA}}", tujuanStr },
                { "{{JenisPerangkat}}", jenisPerangkatStr },
                { "{{TiketSscNo}}", ba.TiketSscNo ?? "-" },
                { "{{TanggalKembali}}", tanggalKembaliStr },
            };

            // Ganti placeholder â€” gunakan paragraph-level replacement agar
            // placeholder yang terpecah oleh spellcheck Word (proofErr) ikut terganti.
            foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>())
            {
                NormalizeParagraphPlaceholders(para, replacements);
            }

            // Sembunyikan teks placeholder tanda tangan ({{SIG_...}}) dengan warna putih
            // agar tidak terlihat di dokumen setelah gambar TTD di-embed
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

            // Kunci lebar kolom semua tabel agar Word tidak auto-resize saat file dibuka
            // dan Sembunyikan garis tabel jika memuat tanda tangan
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

            // 2. Replace Table Rows for Perangkat
            // Template row identified by {{PerangkatJumlah}} placeholder
            var templateRow = mainPart.Document.Body.Descendants<TableRow>()
                .FirstOrDefault(r => r.Descendants<Text>().Any(t => t.Text != null && t.Text.Contains("{{PerangkatJumlah}}")));

            if (templateRow != null && ba.Perangkat != null)
            {
                int idx = 1;
                foreach (var p in ba.Perangkat)
                {
                    var newRow = (TableRow)templateRow.CloneNode(true);

                    // Row number: template cell has "{{PerangkatNo}}." â€” replace the placeholder part
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

            // 3. Ganti placeholder foto â€” hapus Drawing di luar tabel (bukti foto sample),
            //    tapi JANGAN hapus Drawing di dalam <w:tc> (logo Pertamina di header tabel)
            var drawingParas = mainPart.Document.Body!.Descendants<Paragraph>()
                .Where(p => p.Descendants<Drawing>().Any()
                         && string.IsNullOrWhiteSpace(p.InnerText)
                         && !p.Ancestors<TableCell>().Any())
                .ToList();

            if (drawingParas.Any())
            {
                foreach (var drawPara in drawingParas)
                    drawPara.Remove();

                if (ba.BuktiFotos != null && ba.BuktiFotos.Any())
                {
                    uint imgId = 100U;
                    var containerPara = new Paragraph();
                    containerPara.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    foreach (var foto in ba.BuktiFotos)
                    {
                        var fotoPhysical = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(fotoPhysical)) continue;

                        var ext = Path.GetExtension(fotoPhysical).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                        {
                            _logger.LogWarning("[GenerateDocx] Format gambar tidak didukung ({ext}), melewati foto: {path}", ext, fotoPhysical);
                            continue;
                        }
                        var imgPartType = ext == ".png" ? ImagePartType.Png : ImagePartType.Jpeg;
                        var imgPart = mainPart.AddImagePart(imgPartType);
                        using (var fs = File.OpenRead(fotoPhysical)) { imgPart.FeedData(fs); }

                        var dims = GetImageDimensions(fotoPhysical);
                        long cx = 2700000L;
                        long cy = 1980000L;
                        if (dims.width > 0 && dims.height > 0)
                        {
                            double imgRatio = (double)dims.width / dims.height;
                            if (imgRatio > 1.0) { // Landscape
                                cx = 2700000L;
                                cy = (long)(2700000L / imgRatio);
                            } else { // Portrait or Square
                                cy = 2700000L;
                                cx = (long)(2700000L * imgRatio);
                            }
                        }

                        var drawing = CreateImageDrawingWithId(mainPart.GetIdOfPart(imgPart), cx, cy, Path.GetFileName(fotoPhysical), imgId++);
                        containerPara.Append(new Run(drawing));
                        containerPara.Append(new Run(new Text("  ")));
                    }
                    mainPart.Document.Body!.Append(containerPara);
                }
            }
            else if (ba.Jenis != "Lainnya" && ba.BuktiFotos != null && ba.BuktiFotos.Any())
            {
                mainPart.Document.Body!.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                mainPart.Document.Body!.Append(new Paragraph(new Run(new Text("Bukti Foto Serah Terima")))
                {
                    ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center })
                });

                uint imgId = 200U;
                var containerPara = new Paragraph();
                containerPara.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                foreach (var foto in ba.BuktiFotos)
                {
                    var fotoPhysical = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(fotoPhysical)) continue;

                    var ext = Path.GetExtension(fotoPhysical).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        _logger.LogWarning("[GenerateDocx] Format gambar tidak didukung ({ext}), melewati foto: {path}", ext, fotoPhysical);
                        continue;
                    }
                    var imgPartType = ext == ".png" ? ImagePartType.Png : ImagePartType.Jpeg;
                    var imgPart = mainPart.AddImagePart(imgPartType);
                    using (var fs = File.OpenRead(fotoPhysical)) { imgPart.FeedData(fs); }

                    var dims = GetImageDimensions(fotoPhysical);
                    long cx = 2700000L;
                    long cy = 1980000L;
                    if (dims.width > 0 && dims.height > 0)
                    {
                        double imgRatio = (double)dims.width / dims.height;
                        if (imgRatio > 1.0) { // Landscape
                            cx = 2700000L;
                            cy = (long)(2700000L / imgRatio);
                        } else { // Portrait or Square
                            cy = 2700000L;
                            cx = (long)(2700000L * imgRatio);
                        }
                    }

                    var drawing = CreateImageDrawingWithId(mainPart.GetIdOfPart(imgPart), cx, cy, Path.GetFileName(fotoPhysical), imgId++);
                    containerPara.Append(new Run(drawing));
                    containerPara.Append(new Run(new Text("  ")));
                }
                mainPart.Document.Body!.Append(containerPara);
            }

            mainPart.Document.Save();
        }

        // Convert ke PDF â€” dibungkus Task.Run agar Spire.Doc (CPU-bound, ~3-8 detik) tidak
        // memblokir thread pool thread Blazor Server. Tanpa Task.Run, circuit tidak bisa
        // mengirim render update (termasuk loading overlay) selama konversi berlangsung.
        try
        {
            string pdfPhysicalPath = physicalPath.Replace(".docx", ".pdf");
            await Task.Run(() => ConvertDocxToPdfInternal(physicalPath, pdfPhysicalPath));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gagal membuat PDF preview");
        }

        return relativePath;
    }

    /// <summary>
    /// Mengganti teks nomor surat lama ("Draft") dengan nomor surat final
    /// di dalam DOCX yang sudah ada â€” tanpa meregenerasi dokumen dari template.
    /// Digunakan saat BA yang sudah diedit via ONLYOFFICE disetujui.
    /// </summary>
    public async Task PatchNomorSuratAsync(int baId, string nomorSurat)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId)
            ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        // Ganti semua teks "Draft" (yang merupakan nilai placeholder {{NomorSurat}}
        // sebelum nomor surat di-generate) dengan nomor surat final.
        using (var wordDoc = WordprocessingDocument.Open(physicalPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;
            bool changed = false;

            foreach (var text in mainPart.Document.Body!.Descendants<Text>())
            {
                if (text.Text != null && text.Text.Contains("Draft"))
                {
                    text.Text = text.Text.Replace("Draft", nomorSurat);
                    changed = true;
                }
            }

            if (changed)
                mainPart.Document.Save();
        }

        // Regenerasi PDF agar preview diperbarui
        try
        {
            await Task.Run(() => ConvertDocxToPdfInternal(physicalPath, physicalPath.Replace(".docx", ".pdf")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PatchNomorSurat] Gagal regenerasi PDF");
        }
    }

    public async Task<string> EmbedTtdMenyerahkanAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        await EmbedSignatureSpireAsync(physicalPath, ttdPath, "{{SIG_MENYERAHKAN}}");

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdPreviewApproverAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        await EmbedSignatureSpireAsync(physicalPath, ttdPath, "{{SIG_REVIEWER}}");

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdPjAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        await EmbedSignatureSpireAsync(physicalPath, ttdPath, "{{SIG_PJ}}");

        ba.TtdPjPath = Path.Combine("files", "signatures", Path.GetFileName(ttdPath)).Replace("\\", "/");
        await _db.SaveChangesAsync();

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdMengetahuiAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string draftPhysicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(draftPhysicalPath);

        string finalRelativePath = GetRelativePath($"ba-{baId}-final.docx");
        string finalPhysicalPath = GetPhysicalPath(finalRelativePath);
        File.Copy(draftPhysicalPath, finalPhysicalPath, true);

        await EmbedSignatureSpireAsync(finalPhysicalPath, ttdPath, "{{SIG_REVIEWER}}");

        ba.DocxFinalPath = finalRelativePath;
        await _db.SaveChangesAsync();

        return finalRelativePath;
    }

    private async Task EmbedSignatureSpireAsync(string docPath, string imagePath, string placeholder)
    {
        if (!File.Exists(imagePath)) throw new FileNotFoundException("File tanda tangan tidak ditemukan.", imagePath);

        // Embed image dengan OpenXML SDK agar distL/distR = 0 sehingga TTD selalu align left
        // sejajar tepat di atas nama penandatangan.
        // Struktur template (3 row): Row1=label, Row2=SIG placeholder, Row3=nama
        // â†’ Gambar disisipkan langsung ke paragraf di Row2 (sel yang berisi {{SIG_*}}),
        //   menggantikan teks placeholder tersebut.
        using (var wordDoc = WordprocessingDocument.Open(docPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;

            var ext = Path.GetExtension(imagePath).ToLowerInvariant();
            var imgPartType = ext == ".png" ? ImagePartType.Png : ImagePartType.Jpeg;
            var imgPart = mainPart.AddImagePart(imgPartType);
            using (var fs = File.OpenRead(imagePath)) imgPart.FeedData(fs);
            var imgId = mainPart.GetIdOfPart(imgPart);

            // Calculate dimensions preserving aspect ratio
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

            var drawing = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = cx, Cy = cy },
                    new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties { Id = 500U, Name = "signature" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "sig.png" },
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
                    DistanceFromLeft   = 0U,   // â† tidak ada offset kiri agar sejajar kiri sel
                    DistanceFromRight  = 0U
                });

            foreach (var para in mainPart.Document.Body!.Descendants<Paragraph>())
            {
                var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                if (!fullText.Contains(placeholder)) continue;

                // â”€â”€ Strategi baru (template 3-row) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                // Paragraf placeholder berada di Row2 (sel SIG).
                // Ganti SELURUH run di paragraf ini dengan satu run berisi gambar.
                // Atur alignment eksplisit Left + Indentation 0 agar gambar selalu
                // tepat sejajar kiri sel (di atas nama penandatangan di baris bawah).

                var pPr = para.ParagraphProperties;
                if (pPr != null)
                {
                    pPr.Remove();
                }

                // Buat ParagraphProperties baru yang bersih
                // Hitung sisa tinggi (dalam point) yang hilang akibat scaling, lalu tambahkan sebagai Spacing After (dalam twips)
                long cy_pts = cy / 12700L;
                long missing_cy_pts = 70L - cy_pts;
                long spacing_after_twips = missing_cy_pts * 20L;

                para.ParagraphProperties = new ParagraphProperties(
                    new Indentation { Left = "0" },
                    new Justification { Val = JustificationValues.Left },
                    new SpacingBetweenLines { After = spacing_after_twips.ToString() }
                );

                // Hapus semua run & text yang ada (termasuk run placeholder)
                foreach (var run in para.Elements<Run>().ToList())
                    run.Remove();

                // Sisipkan run baru berisi gambar dengan RunProperties yang bersih
                var imgRun = new Run((Drawing)drawing.CloneNode(true));
                para.Append(imgRun);

                // Force the parent table to have a Fixed layout so columns don't auto-resize based on signature width
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

        // Konversi DOCX â†’ PDF dalam Task.Run agar Spire.Doc tidak memblokir
        // circuit thread Blazor Server selama konversi berlangsung.
        try
        {
            await Task.Run(() => ConvertDocxToPdfInternal(docPath, docPath.Replace(".docx", ".pdf")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EmbedSignatureSpire] Gagal konversi PDF setelah embed TTD");
        }
    }

    public async Task ConvertDocxToPdfAsync(string docxPhysicalPath)
    {
        await Task.Run(() => ConvertDocxToPdfInternal(docxPhysicalPath, docxPhysicalPath.Replace(".docx", ".pdf")));
    }

    // Spire.Doc LoadFromFile pada Windows bisa menahan lock file setelah Close().
    // Solusi: load dari SALINAN temp sehingga file asli (DOCX) tetap bebas untuk ditulis.
    private static void ConvertDocxToPdfInternal(string docxPath, string pdfPath)
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        // Temp file harus berekstensi .docx agar Spire.Doc bisa mendeteksi format file
        var tempPath = Path.Combine(Path.GetDirectoryName(docxPath)!, $"_tmp_{uniqueId}_{Path.GetFileName(docxPath)}");
        var tempPdfPath = Path.Combine(Path.GetDirectoryName(pdfPath)!, $"_tmp_{uniqueId}_{Path.GetFileName(pdfPath)}");
        File.Copy(docxPath, tempPath, overwrite: true);
        try
        {
            var doc = new Spire.Doc.Document();
            doc.LoadFromFile(tempPath);
            doc.SaveToFile(tempPdfPath, Spire.Doc.FileFormat.PDF);
            doc.Close();

            // Pindahkan file temp PDF ke PDF tujuan dengan retry jika ada sisa lock
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    File.Move(tempPdfPath, pdfPath, overwrite: true);
                    break;
                }
                catch (IOException) when (attempt < 9)
                {
                    System.Threading.Thread.Sleep(300);
                }
            }
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
            try { File.Delete(tempPdfPath); } catch { }
        }
    }

    private string GetRelativePath(string fileName) => Path.Combine("files", "documents", fileName).Replace("\\", "/");
    private string GetPhysicalPath(string relativePath) => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File DOCX tidak ditemukan.", path);
    }

    private static void NormalizeParagraphPlaceholders(Paragraph para, Dictionary<string, string> replacements)
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
            combined = combined.Replace(r.Key, r.Value);

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

            // Check PNG
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                fs.Seek(16, SeekOrigin.Begin);
                var buf = new byte[8];
                fs.Read(buf, 0, 8);
                int width = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
                int height = (buf[4] << 24) | (buf[5] << 16) | (buf[6] << 8) | buf[7];
                return (width, height);
            }
            // Check JPEG
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
                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)id, Name = name },
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

    // â”€â”€ Template simpel untuk BA Lainnya â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public async Task<string> GenerateLainnyaDocxAsync(BeritaAcara ba)
    {
        var docFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "documents");
        Directory.CreateDirectory(docFolder);
        var fileName = $"BA-Lainnya-{ba.Id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.docx";
        var fullPath = Path.Combine(docFolder, fileName);
        var relPath = $"files/documents/{fileName}";

        string judul = ba.JenisCustom ?? "Lainnya";
        string nomorSurat = ba.NomorSurat ?? $"BA-{ba.Id}";

        await Task.Run(() =>
        {
            using var doc = WordprocessingDocument.Create(fullPath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Margin kecil untuk kertas A4
            var sectPr = new SectionProperties(
                new PageMargin { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440 }
            );

            // Helper: buat paragraph teks centered besar
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

            // Spacer di atas (Â±30% dari halaman)
            body.Append(new Paragraph(new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new SpacingBetweenLines { Before = "4000" }
            )));

            // Baris 1: BERITA ACARA
            AddCenteredText("BERITA ACARA", sizeHalfPt: 72, bold: true, spaceBeforePt: 0);

            // Baris 2: JENIS BERITA ACARA
            AddCenteredText(judul.ToUpperInvariant(), sizeHalfPt: 56, bold: true, spaceBeforePt: 20);

            // Baris 3: Nomor BA
            AddCenteredText(nomorSurat, sizeHalfPt: 40, bold: false, spaceBeforePt: 24);

            body.Append(sectPr);
            mainPart.Document.Save();
        });

        // Convert ke PDF
        await Task.Run(() => ConvertDocxToPdfInternal(fullPath, fullPath.Replace(".docx", ".pdf")));

        return relPath;
    }
}

