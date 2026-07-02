using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemBeritaAcara.Infrastructure.Services;
using SistemBeritaAcara.Core.Interfaces;

// Setup Configuration to read appsettings.Development.json
var configBuilder = new ConfigurationBuilder()
    .SetBasePath(Environment.CurrentDirectory + "\\SistemBeritaAcara.Web")
    .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();
var config = configBuilder.Build();

var emailService = new EmailService(config);

// Mock Data
string toEmail = "widadalfalah03@gmail.com"; // Ganti dengan email Anda yang aktif
string recipientName = "Admin Test";
string nomorSurat = "BA-123/PN/2026";
string pjNama = "Bapak Budi";
string tanggalKembali = DateTime.Now.AddDays(-1).ToString("dd MMMM yyyy");
int daysUntilDue = -1; // -1 = Overdue, 1 = H-1, 0 = Hari ini
bool isForPj = false; // false = untuk admin gudang, true = untuk PJ
int baId = 123;
string baseUrl = "http://localhost:5249";
var barangList = new List<string> 
{ 
    "Laptop Lenovo Thinkpad T14 — 1 Unit", 
    "Proyektor Epson — 1 Unit" 
};

Console.WriteLine("Mengirim email tes...");
try 
{
    // Tes kirim email pengingat
    await emailService.SendDueDateReminderAsync(
        toEmail, recipientName, nomorSurat, pjNama, 
        tanggalKembali, daysUntilDue, isForPj, baId, baseUrl, barangList
    );
    Console.WriteLine("Email berhasil dikirim!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
