using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SistemBeritaAcara.Infrastructure.Services;

public class EwsEmailService(IConfiguration config, ILogger<EwsEmailService> logger)
    : EmailService(config)
{
    private readonly string _ewsUrl  = config["Email:EwsUrl"]  ?? "https://mail.lc.pertamina.com/EWS/Exchange.asmx";
    private readonly string _ewsUser = config["Email:Username"] ?? "";
    private readonly string _ewsPass = config["Email:Password"] ?? "";

    protected override async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var fullHtml  = BuildFullHtml(htmlBody);
        var soap      = BuildSoap(toEmail, subject, fullHtml);
        var response  = await PostSoapAsync(soap);

        if (response.Contains("<m:ResponseCode>NoError</m:ResponseCode>"))
        {
            logger.LogInformation("EWS: email terkirim ke {To} — {Subject}", toEmail, subject);
        }
        else
        {
            var code = ExtractResponseCode(response);
            logger.LogError("EWS: gagal kirim ke {To}. ResponseCode: {Code}", toEmail, code);
            throw new Exception($"EWS send failed: {code}");
        }
    }

    private string BuildSoap(string to, string subject, string htmlBody) => $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
  xmlns:t=""http://schemas.microsoft.com/exchange/services/2006/types""
  xmlns:m=""http://schemas.microsoft.com/exchange/services/2006/messages"">
  <soap:Header>
    <t:RequestServerVersion Version=""Exchange2016""/>
  </soap:Header>
  <soap:Body>
    <m:CreateItem MessageDisposition=""SendAndSaveCopy"">
      <m:SavedItemFolderId>
        <t:DistinguishedFolderId Id=""sentitems""/>
      </m:SavedItemFolderId>
      <m:Items>
        <t:Message>
          <t:Subject>{EscapeXml(subject)}</t:Subject>
          <t:Body BodyType=""HTML"">{EscapeXml(htmlBody)}</t:Body>
          <t:ToRecipients>
            <t:Mailbox>
              <t:EmailAddress>{EscapeXml(to)}</t:EmailAddress>
            </t:Mailbox>
          </t:ToRecipients>
        </t:Message>
      </m:Items>
    </m:CreateItem>
  </soap:Body>
</soap:Envelope>";

    private async Task<string> PostSoapAsync(string soap)
    {
        // Gunakan NTLM — Exchange Pertamina tidak menerima Basic auth
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            Credentials = new NetworkCredential(_ewsUser, _ewsPass)
        };

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var content  = new StringContent(soap, Encoding.UTF8, "text/xml");
        var response = await client.PostAsync(_ewsUrl, content);
        return await response.Content.ReadAsStringAsync();
    }

    private static string EscapeXml(string text) =>
        WebUtility.HtmlEncode(text ?? string.Empty);

    private static string ExtractResponseCode(string xml)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            xml, @"<m:ResponseCode>([^<]+)</m:ResponseCode>");
        return m.Success ? m.Groups[1].Value : "Unknown";
    }
}
