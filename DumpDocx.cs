using System;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

class Program
{
    static void Main()
    {
        var path = @""D:\Sistem_Berita_Acara\SistemBeritaAcara.Web\wwwroot\files\templates\Template_BA_ALOKASI.docx"";
        using (WordprocessingDocument doc = WordprocessingDocument.Open(path, false))
        {
            var body = doc.MainDocumentPart.Document.Body;
            foreach(var para in body.Descendants<Paragraph>())
            {
                var text = string.Concat(para.Descendants<Text>().Select(t => t.Text));
                if (text.Contains(""Catatan"") || text.Contains(""Tiket""))
                {
                    Console.WriteLine(text);
                }
            }
        }
    }
}
