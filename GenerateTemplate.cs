using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

class Program
{
    static void Main()
    {
        string filepath = @""d:\Sistem_Berita_Acara\SistemBeritaAcara.Web\wwwroot\files\templates\Template_BA_LAINNYA.docx"";
        using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(filepath, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document();
            Body body = mainPart.Document.AppendChild(new Body());

            // 1. BERITA ACARA
            Paragraph p1 = new Paragraph(new ParagraphProperties(
                new Justification() { Val = JustificationValues.Center },
                new SpacingBetweenLines() { After = ""200"" }
            ));
            Run r1 = new Run(
                new RunProperties(
                    new RunFonts() { Ascii = ""Arial"", HighAnsi = ""Arial"" },
                    new Bold(),
                    new FontSize() { Val = ""48"" } // 24pt
                ),
                new Text(""BERITA ACARA"")
            );
            p1.Append(r1);
            body.Append(p1);

            // 2. Jenis Custom
            Paragraph p2 = new Paragraph(new ParagraphProperties(
                new Justification() { Val = JustificationValues.Center },
                new SpacingBetweenLines() { After = ""200"" }
            ));
            Run r2 = new Run(
                new RunProperties(
                    new RunFonts() { Ascii = ""Arial"", HighAnsi = ""Arial"" },
                    new Bold(),
                    new FontSize() { Val = ""40"" } // 20pt
                ),
                new Text(""BERITA ACARA ALOKASI BARANG"")
            );
            p2.Append(r2);
            body.Append(p2);

            // 3. Nomor Surat
            Paragraph p3 = new Paragraph(new ParagraphProperties(
                new Justification() { Val = JustificationValues.Center }
            ));
            Run r3 = new Run(
                new RunProperties(
                    new RunFonts() { Ascii = ""Arial"", HighAnsi = ""Arial"" },
                    new FontSize() { Val = ""28"" } // 14pt
                ),
                new Text(""{{NomorSurat}}"")
            );
            p3.Append(r3);
            body.Append(p3);

            // 4. Placeholder for signature table to avoid errors if any code expects it
            Paragraph p4 = new Paragraph();
            Run r4 = new Run(
                new RunProperties(new Color() { Val = ""FFFFFF"" }),
                new Text(""{{SIG_Penerima}}"")
            );
            p4.Append(r4);
            body.Append(p4);
        }
        Console.WriteLine(""Done generating template!"");
    }
}
