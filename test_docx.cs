using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

class Program {
    static void Main() {
        var path = @"c:\Users\DELL\OneDrive\Documents\GitHub\Sistem_Berita_Acara\SistemBeritaAcara.Web\wwwroot\files\templates\BA_Template_Peminjaman.docx";
        using (var wordDoc = WordprocessingDocument.Open(path, false)) {
            var mainPart = wordDoc.MainDocumentPart;
            Console.WriteLine("Document opened.");
            
            try {
                var templateRow = mainPart.Document.Body.Descendants<TableRow>()
                    .FirstOrDefault(r => r.Descendants<Text>().Any(t => t.Text != null && t.Text.Contains("{{PerangkatJumlah}}")));
                Console.WriteLine($"Template row found: {templateRow != null}");
            } catch (Exception ex) {
                Console.WriteLine("Error finding templateRow: " + ex.Message);
            }
            
            try {
                foreach (var para in mainPart.Document.Body.Descendants<Paragraph>()) {
                    var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                    if (fullText.Contains("{{SIG_")) {
                        foreach (var run in para.Descendants<Run>()) {
                            if (run.RunProperties == null) run.RunProperties = new RunProperties();
                            run.RunProperties.Color = new Color { Val = "FFFFFF" };
                        }
                    }
                }
                Console.WriteLine("Sig placeholder logic OK.");
            } catch(Exception ex) {
                Console.WriteLine("Error sig: " + ex);
            }
        }
    }
}
