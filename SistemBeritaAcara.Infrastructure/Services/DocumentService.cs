using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;

namespace SistemBeritaAcara.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly string _outputRoot;
    private readonly string _templatePath;

    public DocumentService(AppDbContext db)
    {
        _db = db;
        _outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "documents");
        _templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "templates", "BA_Template.docx");
        Directory.CreateDirectory(_outputRoot);
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "signatures"));
    }

    public Task<string> GenerateDocxAsync(BeritaAcara ba)
    {
        if (!File.Exists(_templatePath))
        {
            throw new FileNotFoundException("Template DOCX tidak ditemukan. Pastikan file BA_Template.docx ada di wwwroot/files/templates/");
        }

        string relativePath = GetRelativePath($"ba-{ba.Id}-draft.docx");
        string physicalPath = GetPhysicalPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        // Copy template ke file draft
        File.Copy(_templatePath, physicalPath, true);

        using (var wordDoc = WordprocessingDocument.Open(physicalPath, true))
        {
            var mainPart = wordDoc.MainDocumentPart!;

            // 1. Replace Simple Placeholders
            var idCulture = new CultureInfo("id-ID");

            // Jenis perangkat: join semua nama barang dari list (untuk heading tabel)
            var jenisPerangkatStr = (ba.Perangkat != null && ba.Perangkat.Any())
                ? string.Join(", ", ba.Perangkat.Select(p => p.Barang?.NamaBarang ?? "-").Distinct())
                : "-";

            // Tanggal kembali: kosong untuk Alokasi, isi untuk Peminjaman
            var tanggalKembaliStr = (ba.Jenis == "Peminjaman" && ba.TanggalKembali.HasValue)
                ? ba.TanggalKembali.Value.ToString("dd MMMM yyyy", idCulture)
                : string.Empty;

            // ── PRE-PROCESS: Hapus konten sel "Tanggal Pengembalian" untuk non-Peminjaman ──
            if (ba.Jenis != "Peminjaman")
            {
                var tanggalCells = mainPart.Document.Body!
                    .Descendants<TableCell>()
                    .Where(cell => cell.Descendants<Text>().Any(t => t.Text.Contains("{{TanggalKembali}}")))
                    .ToList();

                foreach (var cell in tanggalCells)
                {
                    cell.RemoveAllChildren<Paragraph>();
                    cell.Append(new Paragraph());
                }

                var tanggalParas = mainPart.Document.Body!
                    .Descendants<Paragraph>()
                    .Where(p => !p.Ancestors<TableCell>().Any()
                             && p.Descendants<Text>().Any(t => t.Text.Contains("{{TanggalKembali}}")))
                    .ToList();

                foreach (var para in tanggalParas)
                    para.Remove();
            }

            var replacements = new Dictionary<string, string>
            {
                { "{{NomorSurat}}", ba.NomorSurat ?? "Draft" },
                { "{{Tanggal}}", ba.Tanggal.ToString("dd MMMM yyyy", idCulture) },
                { "{{NamaPJ}}", ba.Pj?.Nama ?? "-" },
                { "{{CostCenter}}", ba.Pj?.CostCenter ?? "-" },
                { "{{JabatanPJ}}", ba.Pj?.Jabatan ?? "-" },
                { "{{FungsiPJ}}", ba.Pj?.FungsiDirektorat ?? "-" },
                { "{{EmailPJ}}", ba.Pj?.Email ?? "-" },
                { "{{NoPekerjaPJ}}", ba.Pj?.NoPekerja ?? "-" },
                { "{{NoTelpPJ}}", ba.Pj?.NoTelp ?? "-" },
                { "{{Menyerahkan}}", ba.Menyerahkan?.Nama ?? "-" },
                { "{{Approver}}", ba.Mengetahui?.Nama ?? "-" },
                { "{{JenisPerangkat}}", jenisPerangkatStr },
                { "{{TanggalKembali}}", tanggalKembaliStr },
            };

            foreach (var text in mainPart.Document.Body!.Descendants<Text>())
            {
                foreach (var r in replacements)
                {
                    if (text.Text.Contains(r.Key))
                    {
                        text.Text = text.Text.Replace(r.Key, r.Value);
                    }
                }
            }

            // 2. Replace Table Rows for Perangkat
            var templateRow = mainPart.Document.Body.Descendants<TableRow>()
                .FirstOrDefault(r => r.Descendants<Text>().Any(t => t.Text.Contains("{{PerangkatNama}}")));

            if (templateRow != null && ba.Perangkat != null)
            {
                int idx = 1;
                foreach (var p in ba.Perangkat)
                {
                    var newRow = (TableRow)templateRow.CloneNode(true);
                    
                    var firstCellText = newRow.Elements<TableCell>().FirstOrDefault()?.Descendants<Text>().FirstOrDefault(t => t.Text == "1");
                    if (firstCellText != null) firstCellText.Text = idx.ToString();

                    foreach (var text in newRow.Descendants<Text>())
                    {
                        if (text.Text.Contains("{{PerangkatNama}}"))
                            text.Text = text.Text.Replace("{{PerangkatNama}}", string.IsNullOrWhiteSpace(p.Keterangan) ? "-" : p.Keterangan);
                        if (text.Text.Contains("{{PerangkatSN}}"))
                            text.Text = text.Text.Replace("{{PerangkatSN}}", string.IsNullOrWhiteSpace(p.NoSerial) ? "-" : p.NoSerial);
                        if (text.Text.Contains("{{PerangkatJumlah}}"))
                            text.Text = text.Text.Replace("{{PerangkatJumlah}}", p.Jumlah.ToString());
                        if (text.Text.Contains("{{PerangkatTerbilang}}"))
                            text.Text = text.Text.Replace("{{PerangkatTerbilang}}", Terbilang(p.Jumlah));
                        if (text.Text.Contains("{{PerangkatSatuan}}"))
                            text.Text = text.Text.Replace("{{PerangkatSatuan}}", p.Satuan ?? "Pcs");
                    }
                    
                    templateRow.InsertBeforeSelf(newRow);
                    idx++;
                }
                templateRow.Remove();
            }

            // 3. Ganti placeholder foto (kotak abu-abu = Drawing shape di template)
            // HANYA hapus paragraph drawing yang TIDAK memiliki teks di dalamnya (untuk menghindari penghapusan Logo Pertamina)
            var drawingParas = mainPart.Document.Body!.Descendants<Paragraph>()
                .Where(p => p.Descendants<Drawing>().Any() && string.IsNullOrWhiteSpace(p.InnerText))
                .ToList();

            if (drawingParas.Any())
            {
                foreach (var drawPara in drawingParas)
                    drawPara.Remove();

                if (ba.BuktiFotos != null && ba.BuktiFotos.Any())
                {
                    uint imgId = 100U;
                    foreach (var foto in ba.BuktiFotos)
                    {
                        var fotoPhysical = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(fotoPhysical)) continue;

                        var ext = Path.GetExtension(fotoPhysical).ToLowerInvariant();
                        var imgPart = ext switch
                        {
                            ".png"  => mainPart.AddImagePart(ImagePartType.Png),
                            ".jpg" or ".jpeg" => mainPart.AddImagePart(ImagePartType.Jpeg),
                            _ => mainPart.AddImagePart(ImagePartType.Jpeg)
                        };
                        using (var fs = File.OpenRead(fotoPhysical)) { imgPart.FeedData(fs); }

                        var drawing = CreateImageDrawingWithId(mainPart.GetIdOfPart(imgPart), 5400000L, 3960000L, Path.GetFileName(fotoPhysical), imgId++);
                        var imgPara = new Paragraph(new Run(drawing));
                        imgPara.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                        mainPart.Document.Body!.Append(imgPara);
                    }
                }
            }
            else if (ba.BuktiFotos != null && ba.BuktiFotos.Any())
            {
                mainPart.Document.Body!.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                mainPart.Document.Body!.Append(new Paragraph(new Run(new Text("Bukti Foto Serah Terima")))
                {
                    ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center })
                });

                uint imgId = 200U;
                foreach (var foto in ba.BuktiFotos)
                {
                    var fotoPhysical = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", foto.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(fotoPhysical)) continue;

                    var ext = Path.GetExtension(fotoPhysical).ToLowerInvariant();
                    var imgPart = ext switch
                    {
                        ".png"  => mainPart.AddImagePart(ImagePartType.Png),
                        ".jpg" or ".jpeg" => mainPart.AddImagePart(ImagePartType.Jpeg),
                        _ => mainPart.AddImagePart(ImagePartType.Jpeg)
                    };
                    using (var fs = File.OpenRead(fotoPhysical)) { imgPart.FeedData(fs); }

                    var drawing = CreateImageDrawingWithId(mainPart.GetIdOfPart(imgPart), 5400000L, 3960000L, Path.GetFileName(fotoPhysical), imgId++);
                    var imgPara = new Paragraph(new Run(drawing));
                    imgPara.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
                    mainPart.Document.Body!.Append(imgPara);
                }
            }

            mainPart.Document.Save();
        }

        // Convert ke PDF menggunakan Spire.Doc untuk keperluan Preview di browser
        try
        {
            string pdfPhysicalPath = physicalPath.Replace(".docx", ".pdf");
            var spireDoc = new Spire.Doc.Document();
            spireDoc.LoadFromFile(physicalPath);
            spireDoc.SaveToFile(pdfPhysicalPath, Spire.Doc.FileFormat.PDF);
            spireDoc.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Gagal membuat PDF preview: {ex.Message}");
        }

        return Task.FromResult(relativePath);
    }

    public async Task<string> EmbedTtdMenyerahkanAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        EmbedSignatureSpire(physicalPath, ttdPath, "{{SIG_MENYERAHKAN}}");

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdPreviewApproverAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        EmbedSignatureSpire(physicalPath, ttdPath, "{{SIG_APPROVER}}");

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdPjAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        EmbedSignatureSpire(physicalPath, ttdPath, "{{SIG_PJ}}");

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

        EmbedSignatureSpire(finalPhysicalPath, ttdPath, "{{SIG_APPROVER}}");

        ba.DocxFinalPath = finalRelativePath;
        await _db.SaveChangesAsync();

        return finalRelativePath;
    }

    private static void EmbedSignatureSpire(string docPath, string imagePath, string placeholder)
    {
        if (!File.Exists(imagePath)) throw new FileNotFoundException("File tanda tangan tidak ditemukan.", imagePath);

        using var document = new Spire.Doc.Document();
        document.LoadFromFile(docPath);

        Spire.Doc.Documents.TextSelection[] selections = document.FindAllString(placeholder, false, true);
        if (selections != null && selections.Length > 0)
        {
            foreach (var selection in selections)
            {
                var textRange = selection.GetAsOneRange();
                var para = textRange.OwnerParagraph;
                var pic = para.AppendPicture(imagePath);
                pic.Width = 100;
                pic.Height = 50;
                para.ChildObjects.Insert(para.ChildObjects.IndexOf(textRange), pic);
                para.ChildObjects.Remove(textRange);
            }
            document.SaveToFile(docPath, Spire.Doc.FileFormat.Docx);
            string pdfPath = docPath.Replace(".docx", ".pdf");
            document.SaveToFile(pdfPath, Spire.Doc.FileFormat.PDF);
        }
    }

    private string GetRelativePath(string fileName) => Path.Combine("files", "documents", fileName).Replace("\\", "/");
    private string GetPhysicalPath(string relativePath) => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File DOCX tidak ditemukan.", path);
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
}
