using System;
using System.IO;
using DocumentFormat.OpenXml.Packaging;

class Program {
    static void Main() {
        var path = @"c:\Users\DELL\OneDrive\Documents\GitHub\Sistem_Berita_Acara\SistemBeritaAcara.Web\wwwroot\files\templates\BA_Template_Peminjaman.docx";
        var pathBak = @"c:\Users\DELL\OneDrive\Documents\GitHub\Sistem_Berita_Acara\SistemBeritaAcara.Web\wwwroot\files\templates\BA_Template_Peminjaman.docx.bak";
        
        using (var wordDoc = WordprocessingDocument.Open(path, false)) {
            Console.WriteLine($"Docx mainPart is null? {wordDoc.MainDocumentPart == null}");
        }
        
        using (var wordDocBak = WordprocessingDocument.Open(pathBak, false)) {
            Console.WriteLine($"Bak mainPart is null? {wordDocBak.MainDocumentPart == null}");
        }
    }
}
