using SistemBeritaAcara.Core.Entities;

namespace SistemBeritaAcara.Core.Interfaces;

public interface IDocumentService
{
    Task<string> GenerateDocxAsync(BeritaAcara ba);
    Task PatchNomorSuratAsync(int baId, string nomorSurat);
    Task<string> EmbedTtdMenyerahkanAsync(int baId, string ttdPath);
    Task<string> EmbedTtdPreviewApproverAsync(int baId, string ttdPath);
    Task<string> EmbedTtdPjAsync(int baId, string ttdPath);
    Task<string> EmbedTtdMengetahuiAsync(int baId, string ttdPath);
    Task ConvertDocxToPdfAsync(string docxPhysicalPath);
}
