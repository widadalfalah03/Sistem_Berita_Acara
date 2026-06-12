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

    public DocumentService(AppDbContext db)
    {
        _db = db;
        _outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "documents");
        Directory.CreateDirectory(_outputRoot);
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files", "signatures"));
    }

    public Task<string> GenerateDocxAsync(BeritaAcara ba)
    {
        string relativePath = GetRelativePath($"ba-{ba.Id}-draft.docx");
        string physicalPath = GetPhysicalPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        using var wordDoc = WordprocessingDocument.Create(physicalPath, WordprocessingDocumentType.Document);
        var mainPart = wordDoc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        body.Append(CreateParagraph("BERITA ACARA", true, 24));
        body.Append(CreateParagraph($"Nomor Surat: {ba.NomorSurat ?? "-"}", false));
        body.Append(CreateParagraph($"Tanggal: {ba.Tanggal:dd MMMM yyyy}", false));
        body.Append(CreateParagraph($"Jenis: {ba.Jenis}{(string.IsNullOrWhiteSpace(ba.JenisCustom) ? string.Empty : $" ({ba.JenisCustom})")}", false));
        body.Append(CreateParagraph($"PJ: {ba.Pj?.Nama ?? "-"}", false));
        body.Append(CreateParagraph($"Yang Menyerahkan: {ba.Menyerahkan?.Nama ?? "-"}", false));
        body.Append(CreateParagraph($"Yang Mengetahui: {ba.Mengetahui?.Nama ?? "-"}", false));
        body.Append(CreateParagraph($"Tiket SSC: {ba.TiketSscNo ?? "-"}", false));

        body.Append(new Paragraph(new Run(new Break())));
        body.Append(CreateParagraph("Daftar Perangkat", true, 20));

        var table = new Table();
        table.Append(new TableProperties(
            new TableBorders(
                new TopBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new BottomBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new LeftBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new RightBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideHorizontalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 },
                new InsideVerticalBorder { Val = new EnumValue<BorderValues>(BorderValues.Single), Size = 4 }
            )));

        table.Append(CreateTableRow("No", "Barang", "Jumlah", "Satuan", "No. Serial", "Keterangan", true));

        int idx = 1;
        foreach (var perangkat in ba.Perangkat ?? Array.Empty<PerangkatBA>())
        {
            string barangNama = perangkat.Barang?.NamaBarang ?? "-";
            table.Append(CreateTableRow(idx.ToString(), barangNama, perangkat.Jumlah.ToString(), perangkat.Satuan, perangkat.NoSerial ?? string.Empty, perangkat.Keterangan ?? string.Empty));
            idx++;
        }

        body.Append(table);
        body.Append(new Paragraph(new Run(new Break())));
        body.Append(CreateParagraph("Dokumen ini dibuat otomatis oleh sistem Berita Acara.", false));

        mainPart.Document.Save();
        return Task.FromResult(relativePath);
    }

    public async Task<string> EmbedTtdMenyerahkanAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId)
            ?? throw new InvalidOperationException($"Berita Acara {baId} tidak ditemukan.");

        string relativePath = ba.DocxPath ?? throw new InvalidOperationException("DocxPath belum disimpan untuk BA ini.");
        string physicalPath = GetPhysicalPath(relativePath);
        EnsureFileExists(physicalPath);

        using var wordDoc = WordprocessingDocument.Open(physicalPath, true);
        AppendSignatureImage(wordDoc, ttdPath, "Tanda Tangan Yang Menyerahkan");
        wordDoc.MainDocumentPart!.Document.Save();

        return relativePath;
    }

    public async Task<string> EmbedTtdPjAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId)
            ?? throw new InvalidOperationException($"Berita Acara {baId} tidak ditemukan.");

        string relativePath = ba.DocxPath ?? throw new InvalidOperationException("DocxPath belum disimpan untuk BA ini.");
        string physicalPath = GetPhysicalPath(relativePath);
        EnsureFileExists(physicalPath);

        using var wordDoc = WordprocessingDocument.Open(physicalPath, true);
        AppendSignatureImage(wordDoc, ttdPath, "Tanda Tangan PJ");
        wordDoc.MainDocumentPart!.Document.Save();

        ba.TtdPjPath = Path.Combine("files", "signatures", Path.GetFileName(ttdPath)).Replace("\\", "/");
        await _db.SaveChangesAsync();

        return relativePath;
    }

    public async Task<string> EmbedTtdMengetahuiAsync(int baId, string ttdPath)
    {
        var ba = await _db.BeritaAcara.FindAsync(baId)
            ?? throw new InvalidOperationException($"Berita Acara {baId} tidak ditemukan.");

        string draftPath = ba.DocxPath ?? throw new InvalidOperationException("DocxPath belum disimpan untuk BA ini.");
        string draftPhysicalPath = GetPhysicalPath(draftPath);
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

    private string GetRelativePath(string fileName)
        => Path.Combine("files", "documents", fileName).Replace("\\", "/");

    private string GetPhysicalPath(string relativePath)
        => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("File DOCX tidak ditemukan.", path);
    }

    private static Paragraph CreateParagraph(string text, bool bold = false, int fontSize = 12)
    {
        var runProperties = new RunProperties();
        if (bold)
        {
            runProperties.Append(new Bold());
        }
        runProperties.Append(new FontSize { Val = (fontSize * 2).ToString() });

        return new Paragraph(new Run(runProperties, new Text(text)));
    }

    private static TableRow CreateTableRow(string c1, string c2, string c3, string c4, string c5, string c6, bool isHeader = false)
    {
        var row = new TableRow();
        row.Append(CreateTableCell(c1, isHeader));
        row.Append(CreateTableCell(c2, isHeader));
        row.Append(CreateTableCell(c3, isHeader));
        row.Append(CreateTableCell(c4, isHeader));
        row.Append(CreateTableCell(c5, isHeader));
        row.Append(CreateTableCell(c6, isHeader));
        return row;
    }

    private static TableCell CreateTableCell(string text, bool isHeader)
    {
        var cell = new TableCell();
        var paragraph = new Paragraph(new Run(new Text(text)));
        if (isHeader)
        {
            paragraph.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
        }
        cell.Append(paragraph);
        cell.Append(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto }));
        return cell;
    }

    private static void AppendSignatureImage(WordprocessingDocument wordDoc, string imagePath, string caption)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("File tanda tangan tidak ditemukan.", imagePath);

        var mainPart = wordDoc.MainDocumentPart ?? wordDoc.AddMainDocumentPart();
        var imagePart = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png),
            ".jpg" or ".jpeg" => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Jpeg),
            ".gif" => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Gif),
            ".bmp" => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Bmp),
            _ => mainPart.AddImagePart(DocumentFormat.OpenXml.Packaging.ImagePartType.Png)
        };
        using (var stream = File.OpenRead(imagePath))
        {
            imagePart.FeedData(stream);
        }

        var element = CreateImageDrawing(mainPart.GetIdOfPart(imagePart), 990000L, 330000L, Path.GetFileName(imagePath));
        mainPart.Document.Body.Append(new Paragraph(new Run(new Text(caption))) { ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }) });
        mainPart.Document.Body.Append(new Paragraph(new Run(element)));
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
            DistanceFromTop = (UInt32Value)0U,
            DistanceFromBottom = (UInt32Value)0U,
            DistanceFromLeft = (UInt32Value)0U,
            DistanceFromRight = (UInt32Value)0U
        };

        return new Drawing(inline);
    }

}
