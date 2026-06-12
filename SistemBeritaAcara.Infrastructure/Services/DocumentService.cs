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
            var replacements = new Dictionary<string, string>
            {
                { "{{NomorSurat}}", ba.NomorSurat ?? "Draft" },
                { "{{Tanggal}}", ba.Tanggal.ToString("dd MMMM yyyy") },
                { "{{NamaPJ}}", ba.Pj?.Nama ?? "-" },
                { "{{CostCenter}}", ba.Pj?.CostCenter ?? "-" },
                { "{{JabatanPJ}}", ba.Pj?.Jabatan ?? "-" },
                { "{{FungsiPJ}}", ba.Pj?.FungsiDirektorat ?? "-" },
                { "{{EmailPJ}}", ba.Pj?.Email ?? "-" },
                { "{{NoPekerjaPJ}}", ba.Pj?.NoPekerja ?? "-" },
                { "{{NoTelpPJ}}", ba.Pj?.NoTelp ?? "-" },
                { "{{Menyerahkan}}", ba.Menyerahkan?.Nama ?? "-" },
                { "{{Approver}}", ba.Mengetahui?.Nama ?? "-" }
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
                    
                    // Ganti kolom NO. (Angka 1 di template dummy ada di kolom pertama, tapi saya tidak set placeholder untuk NO)
                    // Cari Text yang isinya "1" dan ganti jadi index
                    var firstCellText = newRow.Elements<TableCell>().FirstOrDefault()?.Descendants<Text>().FirstOrDefault(t => t.Text == "1");
                    if (firstCellText != null) firstCellText.Text = idx.ToString();

                    foreach (var text in newRow.Descendants<Text>())
                    {
                        if (text.Text.Contains("{{PerangkatNama}}"))
                            text.Text = text.Text.Replace("{{PerangkatNama}}", $"{(p.Barang?.NamaBarang ?? "-")} {(p.Keterangan ?? string.Empty)}".Trim());
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
            // Jika gagal generate PDF, minimal DOCX tetap ada
            Console.WriteLine($"Gagal membuat PDF preview: {ex.Message}");
        }

        return Task.FromResult(relativePath);
    }

    public async Task<string> EmbedTtdMenyerahkanAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        using var wordDoc = WordprocessingDocument.Open(physicalPath, true);
        AppendSignatureImage(wordDoc, ttdPath, "Tanda Tangan Yang Menyerahkan");
        wordDoc.MainDocumentPart!.Document.Save();

        return ba.DocxPath!;
    }

    public async Task<string> EmbedTtdPjAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId) ?? throw new InvalidOperationException($"BA {baId} tidak ditemukan.");
        string physicalPath = GetPhysicalPath(ba.DocxPath!);
        EnsureFileExists(physicalPath);

        using var wordDoc = WordprocessingDocument.Open(physicalPath, true);
        AppendSignatureImage(wordDoc, ttdPath, "Tanda Tangan PJ");
        wordDoc.MainDocumentPart!.Document.Save();

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

        using var wordDoc = WordprocessingDocument.Open(finalPhysicalPath, true);
        AppendSignatureImage(wordDoc, ttdPath, "Tanda Tangan Approver");
        wordDoc.MainDocumentPart!.Document.Save();

        ba.DocxFinalPath = finalRelativePath;
        await _db.SaveChangesAsync();

        return finalRelativePath;
    }

    private string GetRelativePath(string fileName) => Path.Combine("files", "documents", fileName).Replace("\\", "/");
    private string GetPhysicalPath(string relativePath) => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File DOCX tidak ditemukan.", path);
    }

    private static void AppendSignatureImage(WordprocessingDocument wordDoc, string imagePath, string caption)
    {
        if (!File.Exists(imagePath)) throw new FileNotFoundException("File tanda tangan tidak ditemukan.", imagePath);

        var mainPart = wordDoc.MainDocumentPart ?? wordDoc.AddMainDocumentPart();
        var imagePart = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png),
            ".jpg" or ".jpeg" => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Jpeg),
            _ => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png)
        };
        using (var stream = File.OpenRead(imagePath)) { imagePart.FeedData(stream); }

        var element = CreateImageDrawing(mainPart.GetIdOfPart(imagePart), 990000L, 330000L, Path.GetFileName(imagePath));
        mainPart.Document.Body!.Append(new Paragraph(new Run(new Text(caption))) { ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }) });
        mainPart.Document.Body!.Append(new Paragraph(new Run(element)));
    }

    private static Drawing CreateImageDrawing(string relationshipId, long widthEmu, long heightEmu, string name)
    {
        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)0U, Name = name },
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
            new DW.DocProperties { Id = (UInt32Value)1U, Name = name },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = (UInt32Value)0U, DistanceFromBottom = (UInt32Value)0U,
            DistanceFromLeft = (UInt32Value)0U, DistanceFromRight = (UInt32Value)0U
        };

        return new Drawing(inline);
    }

    private static string Terbilang(int angka)
    {
        string[] huruf = { "", "Satu", "Dua", "Tiga", "Empat", "Lima", "Enam", "Tujuh", "Delapan", "Sembilan", "Sepuluh", "Sebelas" };
        if (angka < 12) return huruf[angka];
        if (angka < 20) return Terbilang(angka - 10) + " Belas";
        if (angka < 100) return Terbilang(angka / 10) + " Puluh " + Terbilang(angka % 10);
        return angka.ToString(); // Simplified for basic amounts
    }
}
