# Panduan Deploy — Sistem Berita Acara Digital
**PT Pertamina Patra Niaga SSC ICT · Regional Sumbagsel · Dev Server**

---

## Daftar Isi

1. [Gambaran Umum](#1-gambaran-umum)
2. [Prasyarat Server](#2-prasyarat-server)
3. [Instalasi Prasyarat](#3-instalasi-prasyarat)
4. [Persiapan Aplikasi (Publish)](#4-persiapan-aplikasi-publish)
5. [Setup SQL Server](#5-setup-sql-server)
6. [Setup IIS](#6-setup-iis)
7. [Konfigurasi appsettings.Production.json](#7-konfigurasi-appsettingsproductionjson)
8. [Konfigurasi Email @pertamina.com](#8-konfigurasi-email-pertaminacom)
9. [Konfigurasi HTTPS / SSL](#9-konfigurasi-https--ssl)
10. [Setup OnlyOffice (Opsional)](#10-setup-onlyoffice-opsional)
11. [Konfigurasi Reverse Proxy (Nginx / IIS ARR)](#11-konfigurasi-reverse-proxy-nginx--iis-arr)
12. [Konfigurasi Firewall & Port](#12-konfigurasi-firewall--port)
13. [Jalankan & Verifikasi Aplikasi](#13-jalankan--verifikasi-aplikasi)
14. [Setup Pertama Kali (First-Run)](#14-setup-pertama-kali-first-run)
15. [Import Data Master](#15-import-data-master)
16. [Perawatan & Monitoring](#16-perawatan--monitoring)
17. [Troubleshooting](#17-troubleshooting)
18. [Checklist Deploy](#18-checklist-deploy)

---

## 1. Gambaran Umum

```
[Client Browser]
       │  HTTPS 443
       ▼
  [IIS / Nginx]  ← reverse proxy
       │  HTTP 5000 (internal)
       ▼
 [Kestrel / .NET 10]
       │
       ├── SQL Server (port 1433)
       ├── SMTP Exchange (port 587 / 25)
       └── OnlyOffice (port 8081, opsional)
```

Aplikasi berjalan sebagai **ASP.NET Core Blazor Server** di atas Kestrel, dibalik IIS sebagai reverse proxy. Database menggunakan SQL Server yang ada di server yang sama atau server DB terpisah.

---

## 2. Prasyarat Server

| Komponen | Versi Minimum | Keterangan |
|---|---|---|
| OS | Windows Server 2019 | 2022 lebih disarankan |
| IIS | 10.0 | Role harus diaktifkan |
| ASP.NET Core Module v2 | — | Bundled dengan Hosting Bundle |
| .NET 10 Hosting Bundle | 10.0.x | Wajib di server |
| SQL Server | 2019 Express/Standard | Express cukup untuk dev |
| RAM | minimal 4 GB | 8 GB disarankan |
| Disk | minimal 20 GB bebas | untuk DB + generated files |

> **Catatan:** Jika server sudah memiliki SQL Server dari sistem lain (SAP, dll.), cukup buat database baru di instance yang sama.

---

## 3. Instalasi Prasyarat

### 3.1 Aktifkan IIS di Windows Server

Buka **Server Manager** → **Add Roles and Features** → pilih:

```
Web Server (IIS)
  ├── Common HTTP Features
  │     ├── Static Content ✓
  │     ├── Default Document ✓
  │     └── HTTP Errors ✓
  ├── Application Development
  │     ├── ASP.NET 4.x ✓
  │     └── WebSocket Protocol ✓   ← WAJIB untuk SignalR
  └── Security
        └── Basic Authentication ✓
```

Atau via PowerShell (run as Administrator):

```powershell
Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-WebSockets, Web-Default-Doc, Web-Http-Errors, Web-Static-Content -IncludeManagementTools
```

### 3.2 Install .NET 10 Hosting Bundle

Download dari: https://dotnet.microsoft.com/download/dotnet/10.0  
Pilih: **ASP.NET Core Runtime 10.x.x — Windows Hosting Bundle**

Jalankan installer, restart IIS setelah selesai:

```cmd
iisreset /restart
```

Verifikasi:
```cmd
dotnet --version
# Output: 10.x.x
```

### 3.3 Verifikasi ASP.NET Core Module

Buka IIS Manager → klik server → **Modules** → pastikan `AspNetCoreModuleV2` ada di daftar.

---

## 4. Persiapan Aplikasi (Publish)

Lakukan di **komputer developer**, bukan di server.

### 4.1 Publish ke folder

```bash
# Di root project
dotnet publish SistemBeritaAcara.Web -c Release -r win-x64 --self-contained false -o ./publish
```

### 4.2 Isi folder publish yang dihasilkan

```
publish/
├── SistemBeritaAcara.Web.exe
├── SistemBeritaAcara.Web.dll
├── appsettings.json
├── web.config              ← dibuat otomatis oleh dotnet publish
├── wwwroot/
│   ├── app.css
│   ├── js/
│   ├── images/
│   └── files/
│       └── templates/      ← template .docx (PENTING: harus ada)
└── [dll lainnya]
```

### 4.3 Transfer ke server

Salin seluruh folder `publish/` ke server, misalnya:

```
\\dev-server\C$\inetpub\wwwroot\SistemBeritaAcara\
```

Atau gunakan **WinSCP**, **Robocopy**, atau **xcopy**:

```cmd
robocopy ".\publish" "\\dev-server\C$\inetpub\SistemBeritaAcara" /E /PURGE
```

---

## 5. Setup SQL Server

### 5.1 Buat Database

Buka **SQL Server Management Studio (SSMS)**, sambungkan ke instance SQL Server di dev server, lalu jalankan:

```sql
CREATE DATABASE [SistemBeritaAcara]
    COLLATE Latin1_General_CI_AS;
GO
```

> Collation `Latin1_General_CI_AS` penting agar karakter Indonesia (huruf dengan tanda) tersimpan dengan benar.

### 5.2 Buat SQL Login untuk Aplikasi

Jika tidak menggunakan Windows Authentication:

```sql
-- Buat login
CREATE LOGIN [sba_app] WITH PASSWORD = 'P@ssw0rd_SBA2025!';
GO

-- Buat user di database
USE [SistemBeritaAcara];
CREATE USER [sba_app] FOR LOGIN [sba_app];
ALTER ROLE [db_owner] ADD MEMBER [sba_app];
GO
```

> Ganti `P@ssw0rd_SBA2025!` dengan password yang kuat sesuai kebijakan keamanan Pertamina.

### 5.3 Connection String

**Opsi A — Windows Authentication** (disarankan di domain Pertamina):
```
Server=NAMA_SERVER\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

**Opsi B — SQL Authentication**:
```
Server=NAMA_SERVER\SQLEXPRESS;Database=SistemBeritaAcara;User Id=sba_app;Password=P@ssw0rd_SBA2025!;TrustServerCertificate=True;MultipleActiveResultSets=true
```

> Ganti `NAMA_SERVER` dengan hostname atau IP server SQL, contoh: `DEVSRV01\SQLEXPRESS`

---

## 6. Setup IIS

### 6.1 Buat Application Pool

Buka IIS Manager → **Application Pools** → **Add Application Pool**:

| Setting | Nilai |
|---|---|
| Name | `SistemBeritaAcara` |
| .NET CLR Version | **No Managed Code** |
| Managed Pipeline | Integrated |

Setelah dibuat, klik kanan pool → **Advanced Settings**:

| Setting | Nilai |
|---|---|
| Identity | `ApplicationPoolIdentity` atau akun domain khusus |
| Start Mode | `AlwaysRunning` |
| Idle Time-out | `0` (disable) |
| Regular Time Interval | `0` (disable recycle) |

> Untuk production/dev server Pertamina yang perlu akses jaringan internal (SMTP, SQL), gunakan **akun domain** sebagai Identity agar memiliki akses ke resource domain.

### 6.2 Buat Website / Application

Buka IIS Manager → **Sites** → **Add Website**:

| Setting | Nilai |
|---|---|
| Site name | `SistemBeritaAcara` |
| Application pool | `SistemBeritaAcara` (yang baru dibuat) |
| Physical path | `C:\inetpub\SistemBeritaAcara\` |
| Binding — Type | `http` (dulu) |
| Binding — Port | `80` atau port yang tersedia |
| Binding — Host name | `sba.pertamina.com` (jika sudah ada domain internal) |

### 6.3 Konfigurasi web.config

File `web.config` dibuat otomatis oleh `dotnet publish`. Verifikasi isinya di server:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*"
             modules="AspNetCoreModuleV2"
             resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\SistemBeritaAcara.Web.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

Buat folder `logs` di direktori aplikasi agar stdout log bisa ditulis:
```cmd
mkdir C:\inetpub\SistemBeritaAcara\logs
```

### 6.4 Permission Folder

Identity Application Pool harus bisa **membaca, menulis, dan menghapus** file di folder aplikasi:

```cmd
icacls "C:\inetpub\SistemBeritaAcara" /grant "IIS AppPool\SistemBeritaAcara:(OI)(CI)(M)" /T
```

Folder yang paling penting bisa ditulis:
```cmd
icacls "C:\inetpub\SistemBeritaAcara\wwwroot\files" /grant "IIS AppPool\SistemBeritaAcara:(OI)(CI)(F)" /T
```

---

## 7. Konfigurasi appsettings.Production.json

Buat file `appsettings.Production.json` di folder aplikasi di server. File ini **tidak di-commit ke Git** (sudah ada di `.gitignore`).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DEVSRV01\\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Email": {
    "SmtpHost": "smtp.pertamina.com",
    "SmtpPort": "587",
    "Username": "sba-noreply@pertamina.com",
    "Password": "PASSWORD_EMAIL_SERVICE",
    "From": "sba-noreply@pertamina.com"
  },
  "App": {
    "BaseUrl": "http://sba.dev.pertamina.com",
    "PublicUrl": "http://sba.dev.pertamina.com"
  },
  "OnlyOffice": {
    "ServerUrl": "http://onlyoffice.dev.pertamina.com",
    "CallbackSecret": ""
  },
  "NomorSurat": {
    "FormatUmum": "BA {counter}/PPNEG1000/{year}-S0",
    "FormatLainnya": "BA {counter}/PPNEG1000/{year}-S8"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

> **PENTING:** `App:PublicUrl` harus diisi dengan URL yang bisa diakses dari browser pengguna dan dari email client. URL ini digunakan untuk link tanda tangan PJ yang dikirim via email.

---

## 8. Konfigurasi Email @pertamina.com

Pertamina Patra Niaga menggunakan server email Microsoft Exchange / Microsoft 365 dengan domain `@pertamina.com`. Ada dua pendekatan:

### Opsi A — SMTP Relay Internal Exchange (Tanpa Autentikasi)

Cara ini cocok jika server dev berada di jaringan internal Pertamina dan Exchange dikonfigurasi sebagai relay:

```json
"Email": {
  "SmtpHost": "mail.pertamina.com",
  "SmtpPort": "25",
  "Username": "",
  "Password": "",
  "From": "sba-noreply@pertamina.com"
}
```

Di EmailService.cs, koneksi SMTP akan menggunakan `SecureSocketOptions.None` jika port 25 tanpa SSL. Hubungi tim IT Pertamina untuk:
- IP address SMTP relay internal
- Apakah perlu whitelist IP server dev
- Apakah perlu konfigurasi connector di Exchange

### Opsi B — Akun Service Microsoft 365 (Dengan Autentikasi)

Jika Exchange menggunakan SMTP submission authenticated:

```json
"Email": {
  "SmtpHost": "smtp.office365.com",
  "SmtpPort": "587",
  "Username": "sba-noreply@pertamina.com",
  "Password": "PASSWORD_AKUN_SERVICE",
  "From": "sba-noreply@pertamina.com"
}
```

> **Catatan:** Untuk akun @pertamina.com di Microsoft 365, kemungkinan besar perlu **App Password** atau konfigurasi **OAuth2**. Koordinasikan dengan tim IT/ICT Pertamina untuk pembuatan akun service email dan izin SMTP AUTH.

### Opsi C — SMTP Relay via IP Server Exchange

Koordinasikan dengan tim ICT untuk mendapatkan IP SMTP relay:

```json
"Email": {
  "SmtpHost": "192.168.x.x",
  "SmtpPort": "25",
  "Username": "",
  "Password": "",
  "From": "sba-noreply@pertamina.com"
}
```

### Verifikasi Email

Setelah konfigurasi, test pengiriman email dari aplikasi dengan cara:
1. Login sebagai Admin IT
2. Buka **Tambah Pengguna** → isi form → kirim undangan
3. Periksa apakah email undangan masuk ke inbox penerima

---

## 9. Konfigurasi HTTPS / SSL

### 9.1 Menggunakan Sertifikat Internal Pertamina (Disarankan)

Pertamina kemungkinan memiliki **Internal Certificate Authority (CA)** sendiri. Koordinasikan dengan tim IT untuk:
1. Meminta sertifikat SSL untuk domain `sba.dev.pertamina.com`
2. Mendapatkan file `.pfx` atau `.cer + .key`

Install sertifikat di Windows Server:
1. Buka **IIS Manager** → klik nama server → **Server Certificates**
2. Klik **Import** → pilih file `.pfx` → masukkan password
3. Kembali ke site `SistemBeritaAcara` → **Bindings** → **Add**:
   - Type: `https`
   - Port: `443`
   - Host name: `sba.dev.pertamina.com`
   - SSL certificate: pilih sertifikat yang baru diimport

### 9.2 Update KnownProxies untuk Reverse Proxy

Jika HTTPS di-handle oleh reverse proxy (load balancer, Nginx, atau IIS ARR) yang berbeda server, tambahkan IP proxy ke `Program.cs`:

Edit `Program.cs` — tambahkan IP proxy:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor 
                     | ForwardedHeaders.XForwardedProto 
                     | ForwardedHeaders.XForwardedHost,
    KnownProxies = {
        System.Net.IPAddress.Loopback,
        System.Net.IPAddress.IPv6Loopback,
        System.Net.IPAddress.Parse("192.168.X.X") // ← IP reverse proxy
    }
});
```

Setelah edit, publish ulang dan deploy.

### 9.3 Redirect HTTP → HTTPS

Setelah HTTPS aktif, tambahkan HTTP Redirect di IIS:
1. Pilih site → **HTTP Redirect** → centang **Redirect requests to this destination**
2. Isi: `https://sba.dev.pertamina.com`
3. Centang **Redirect all requests to exact destination** dan status `301`

---

## 10. Setup OnlyOffice (Opsional)

OnlyOffice digunakan untuk fitur **Edit Manual** dokumen DOCX. Jika tidak dibutuhkan, fitur ini bisa dinonaktifkan dan aplikasi tetap berjalan normal (generate + preview PDF tetap jalan via Spire.Doc).

### 10.1 Install via Docker (Disarankan)

Pastikan Docker Desktop atau Docker Engine terinstall di server:

```bash
docker pull onlyoffice/documentserver
docker run -d -p 8081:80 \
  --name onlyoffice \
  --restart always \
  -v /var/www/onlyoffice/Data:/var/www/onlyoffice/Data \
  onlyoffice/documentserver
```

### 10.2 Install via Windows (Tanpa Docker)

Download installer dari: https://www.onlyoffice.com/download-docs.aspx  
Pilih **ONLYOFFICE Docs Community Edition for Windows**.

### 10.3 Konfigurasi local.json

Salin file `local.json` (ada di root project) ke direktori konfigurasi OnlyOffice:

- **Docker:** `docker cp local.json onlyoffice:/etc/onlyoffice/documentserver/local.json`
- **Windows:** `C:\Program Files\ONLYOFFICE\DocumentServer\config\local.json`

Isi file `local.json`:
```json
{
  "services": {
    "CoAuthoring": {
      "request-filtering-agent": {
        "allowPrivateIPAddress": true,
        "allowMetaIPAddress": true
      },
      "token": {
        "enable": {
          "request": { "inbox": false, "outbox": false },
          "browser": false
        }
      }
    }
  }
}
```

Restart OnlyOffice setelah konfigurasi.

### 10.4 Update appsettings

```json
"OnlyOffice": {
  "ServerUrl": "http://IP_SERVER:8081",
  "CallbackSecret": ""
}
```

---

## 11. Konfigurasi Reverse Proxy (Nginx / IIS ARR)

Jika menggunakan **IIS sebagai reverse proxy ke Kestrel** (recommended untuk Windows Server):

### 11.1 Install IIS Application Request Routing (ARR)

Download ARR 3.0 dari: https://www.iis.net/downloads/microsoft/application-request-routing

Setelah install, di IIS Manager:
1. Klik nama server → **Application Request Routing Cache** → **Server Proxy Settings**
2. Centang **Enable proxy** → Apply

### 11.2 Jalankan Kestrel sebagai Windows Service

Agar aplikasi berjalan otomatis tanpa login, daftarkan sebagai Windows Service:

```cmd
sc create "SistemBeritaAcara" binPath= "C:\inetpub\SistemBeritaAcara\SistemBeritaAcara.Web.exe" start= auto
sc description "SistemBeritaAcara" "Sistem Berita Acara Digital - Pertamina Patra Niaga SSC ICT"
sc start "SistemBeritaAcara"
```

Atau gunakan IIS sebagai host langsung (in-process) — ini lebih sederhana dan sudah dikonfigurasi di `web.config` dengan `hostingModel="inprocess"`.

### 11.3 Konfigurasi Nginx (Alternatif)

Jika menggunakan Nginx sebagai reverse proxy:

```nginx
server {
    listen 443 ssl;
    server_name sba.dev.pertamina.com;

    ssl_certificate     /etc/ssl/certs/sba.crt;
    ssl_certificate_key /etc/ssl/private/sba.key;

    # WebSocket untuk SignalR (WAJIB)
    location /hubs/ {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400s;
        proxy_send_timeout 86400s;
    }

    # Semua request lain
    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host $host;
        proxy_cache_bypass $http_upgrade;
        client_max_body_size 50M;
    }
}

server {
    listen 80;
    server_name sba.dev.pertamina.com;
    return 301 https://$host$request_uri;
}
```

> **SignalR memerlukan WebSocket** — pastikan `Upgrade` dan `Connection upgrade` dikonfigurasi di blok `/hubs/`.

---

## 12. Konfigurasi Firewall & Port

### Port yang Perlu Dibuka di Server

| Port | Protokol | Keterangan |
|---|---|---|
| 80 | TCP Inbound | HTTP (redirect ke HTTPS) |
| 443 | TCP Inbound | HTTPS (akses utama) |
| 1433 | TCP Local | SQL Server (internal only) |
| 8081 | TCP Local | OnlyOffice (internal only) |
| 587 / 25 | TCP Outbound | SMTP ke mail server |

### Windows Firewall (PowerShell)

```powershell
# Izinkan HTTP dan HTTPS dari intranet
New-NetFirewallRule -DisplayName "SBA HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
New-NetFirewallRule -DisplayName "SBA HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow

# Blokir port internal dari luar (opsional, jika belum diblokir)
New-NetFirewallRule -DisplayName "Block OnlyOffice External" -Direction Inbound -Protocol TCP -LocalPort 8081 -RemoteAddress Internet -Action Block
```

### DNS Internal Pertamina

Koordinasikan dengan tim IT/ICT Pertamina untuk mendaftarkan hostname di DNS internal:

```
sba.dev.pertamina.com → 192.168.X.X (IP server dev)
```

---

## 13. Jalankan & Verifikasi Aplikasi

### 13.1 Jalankan via IIS

1. Buka IIS Manager → Sites → `SistemBeritaAcara` → **Start**
2. Application Pool `SistemBeritaAcara` → **Start**

### 13.2 Verifikasi Log

Cek log di `C:\inetpub\SistemBeritaAcara\logs\stdout*.log`:

```
# Tanda aplikasi berjalan normal:
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started.
```

### 13.3 Test Akses Browser

Buka browser dari komputer di jaringan internal:
```
http://sba.dev.pertamina.com
```

Seharusnya diarahkan ke halaman `/setup` (pertama kali) atau `/login`.

### 13.4 Test SignalR (WebSocket)

Buka browser DevTools (F12) → Network → filter `WS`:
- Seharusnya ada koneksi WebSocket ke `/hubs/ba` dengan status `101 Switching Protocols`
- Jika tidak ada atau error, periksa konfigurasi WebSocket di IIS dan Nginx

---

## 14. Setup Pertama Kali (First-Run)

Saat pertama kali aplikasi diakses dan database masih kosong, sistem otomatis redirect ke `/setup`.

### Langkah-langkah:

1. Buka `http://sba.dev.pertamina.com` → akan redirect ke `/setup`
2. Isi form Setup Admin IT:
   - **Nama Lengkap**: nama admin IT
   - **Email**: alamat email @pertamina.com admin (contoh: `nama@pertamina.com`)
   - **Password**: password kuat (min. 8 karakter)
   - **Tanda Tangan**: gambar tanda tangan (upload atau gambar langsung)
3. Klik **Selesai & Login**
4. Sistem membuat:
   - Roles: `AdminIT`, `AdminGudangBarang`, `Reviewer`
   - Data Pegawai untuk Admin IT
   - Akun user Admin IT
5. Login otomatis ke Dashboard

### Database Schema

Database dibuat otomatis oleh aplikasi saat pertama dijalankan (`EnsureCreated()`). Tidak perlu menjalankan migration manual.

---

## 15. Import Data Master

### 15.1 Import Data Pegawai

1. Login sebagai **Admin IT**
2. Menu sidebar → **Data Pegawai**
3. Klik tombol **Import Excel**
4. Upload file `.xlsx` dengan format kolom:

| Kolom | Keterangan |
|---|---|
| Nama | Nama lengkap pegawai |
| NoPekerja | Nomor pekerja |
| Jabatan | Jabatan/posisi |
| CostCenter | Kode cost center |
| FungsiDirektorat | Fungsi atau direktorat |
| Alamat | Alamat unit kerja |
| Email | Email @pertamina.com |
| NoTelp | Nomor telepon |

### 15.2 Import Data Barang (Master Barang IT)

1. Menu sidebar → **Data Barang**
2. Klik **Import Excel**
3. Upload file `.xlsx` dengan kolom nama barang

### 15.3 Tambah Pengguna Sistem

1. Menu sidebar → **Daftar Pengguna** → **Tambah Pengguna**
2. Isi data:
   - Email harus ada di data Pegawai yang sudah diimport
   - Pilih role: `AdminGudangBarang` atau `Reviewer`
3. Sistem kirim email undangan ke alamat email yang diisi
4. Pengguna buka link undangan → setup password & tanda tangan → aktif

---

## 16. Perawatan & Monitoring

### 16.1 Backup Database

Jadwalkan backup otomatis di SQL Server Agent:

```sql
BACKUP DATABASE [SistemBeritaAcara]
TO DISK = 'D:\Backup\SistemBeritaAcara_' + FORMAT(GETDATE(), 'yyyyMMdd') + '.bak'
WITH COMPRESSION, STATS = 10;
```

### 16.2 Backup File Generated

Salin periodik folder:
```
C:\inetpub\SistemBeritaAcara\wwwroot\files\documents\
C:\inetpub\SistemBeritaAcara\wwwroot\files\signatures\
```

### 16.3 Hangfire Dashboard

Monitor background job di:
```
http://sba.dev.pertamina.com/hangfire
```

Hanya bisa diakses oleh user yang login sebagai **Admin IT**.

Background jobs yang berjalan otomatis:
- **DueDateCheckerJob** — cek BA yang jatuh tempo
- **AutoApproveJob** — auto-approve BA jika dikonfigurasi

### 16.4 Log Aplikasi

```
C:\inetpub\SistemBeritaAcara\logs\stdout*.log
```

Untuk melihat log real-time via PowerShell:
```powershell
Get-Content "C:\inetpub\SistemBeritaAcara\logs\stdout*.log" -Tail 50 -Wait
```

### 16.5 Update Aplikasi

```cmd
# 1. Stop Application Pool
%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:SistemBeritaAcara

# 2. Salin file publish baru (kecuali appsettings.Production.json dan wwwroot/files/)
robocopy ".\publish" "C:\inetpub\SistemBeritaAcara" /E /XF appsettings.Production.json /XD wwwroot\files

# 3. Start Application Pool
%windir%\system32\inetsrv\appcmd start apppool /apppool.name:SistemBeritaAcara
```

---

## 17. Troubleshooting

### ❌ HTTP Error 500.30 — ASP.NET Core app failed to start

**Penyebab:** Hosting Bundle tidak terinstall atau versi salah.

**Solusi:**
```cmd
dotnet --version
# Pastikan output 10.x.x
iisreset /restart
```

---

### ❌ HTTP Error 500.19 — web.config error

**Penyebab:** ASP.NET Core Module (ANCM) tidak ditemukan.

**Solusi:**
1. Install ulang .NET 10 Hosting Bundle
2. `iisreset /restart`

---

### ❌ Aplikasi jalan tapi tidak bisa login / blank page

**Penyebab:** Database tidak bisa diakses.

**Solusi:**
1. Cek connection string di `appsettings.Production.json`
2. Pastikan SQL Server service berjalan: `services.msc` → SQL Server
3. Test koneksi dari server: `sqlcmd -S NAMA_SERVER\SQLEXPRESS -Q "SELECT 1"`

---

### ❌ Notifikasi real-time tidak berfungsi (SignalR)

**Penyebab:** WebSocket tidak diaktifkan di IIS.

**Solusi:**
```powershell
Install-WindowsFeature Web-WebSockets
iisreset /restart
```

---

### ❌ Email tidak terkirim

**Penyebab:** Konfigurasi SMTP salah atau server email memblokir.

**Solusi:**
1. Cek log aplikasi untuk pesan error SMTP
2. Test telnet ke SMTP server: `telnet smtp.pertamina.com 587`
3. Koordinasikan dengan tim IT untuk whitelist IP server di Exchange
4. Pastikan port 587 atau 25 outbound dibuka di firewall

---

### ❌ Link tanda tangan PJ di email tidak bisa dibuka

**Penyebab:** `App:PublicUrl` di konfigurasi masih `localhost`.

**Solusi:**
Update `appsettings.Production.json`:
```json
"App": {
  "PublicUrl": "http://sba.dev.pertamina.com"
}
```

---

### ❌ Preview PDF tidak muncul

**Penyebab:** Spire.Doc gagal konversi (lisensi atau file corrupt).

**Solusi:**
1. Cek log: cari `[DocumentService]`
2. Coba download file `.docx` dan buka manual untuk verifikasi
3. Pastikan file template ada di `wwwroot/files/templates/`

---

### ❌ OnlyOffice "Dokumen tidak bisa dimuat"

**Penyebab:** OnlyOffice tidak bisa callback ke server aplikasi.

**Solusi:**
1. Pastikan `App:PublicUrl` bisa diakses dari mesin OnlyOffice
2. Cek `local.json` — `allowPrivateIPAddress: true` harus ada
3. Restart container OnlyOffice: `docker restart onlyoffice`

---

## 18. Checklist Deploy

### Persiapan Server
- [ ] Windows Server 2019/2022 aktif dan bisa diakses
- [ ] IIS terinstall dengan WebSocket Protocol
- [ ] .NET 10 Hosting Bundle terinstall
- [ ] SQL Server instance berjalan

### Aplikasi
- [ ] `dotnet publish` berhasil tanpa error
- [ ] Folder publish tersalin ke server
- [ ] Template `.docx` ada di `wwwroot/files/templates/`
- [ ] Folder `wwwroot/files/` bisa ditulis oleh Application Pool

### Database
- [ ] Database `SistemBeritaAcara` dibuat
- [ ] Connection string di `appsettings.Production.json` benar
- [ ] Test koneksi dari server berhasil

### IIS
- [ ] Application Pool `SistemBeritaAcara` dibuat (No Managed Code)
- [ ] Website/Application mengarah ke folder yang benar
- [ ] Permission folder sudah diset
- [ ] `web.config` ada dan valid

### Konfigurasi
- [ ] `appsettings.Production.json` dibuat di server (bukan di Git)
- [ ] `App:PublicUrl` diisi dengan URL yang bisa diakses dari luar
- [ ] Connection string production benar
- [ ] Email SMTP dikonfigurasi (@pertamina.com)

### Email
- [ ] SMTP host dan port benar
- [ ] Akun email service tersedia
- [ ] Test kirim email berhasil
- [ ] Link undangan di email bisa dibuka

### DNS & Network
- [ ] DNS `sba.dev.pertamina.com` sudah didaftarkan
- [ ] Port 80 dan 443 terbuka dari intranet Pertamina
- [ ] Port 587/25 outbound ke mail server terbuka
- [ ] Bisa akses `http://sba.dev.pertamina.com` dari client

### HTTPS (Opsional untuk Dev)
- [ ] Sertifikat SSL dari CA internal Pertamina tersedia
- [ ] Binding HTTPS 443 dikonfigurasi di IIS
- [ ] Redirect HTTP → HTTPS aktif

### First-Run
- [ ] Buka URL → diarahkan ke `/setup`
- [ ] Akun Admin IT dibuat
- [ ] Login berhasil masuk ke Dashboard
- [ ] Import data Pegawai berhasil
- [ ] Import data MasterBarang berhasil
- [ ] Tambah pengguna (AdminGB + Reviewer) berhasil
- [ ] Email undangan terkirim dan bisa dibuka
- [ ] Alur lengkap BA berhasil (buat → kirim → review → arsip)

### Monitoring
- [ ] Log aplikasi aktif dan bisa dibaca
- [ ] Hangfire Dashboard bisa diakses di `/hangfire`
- [ ] Backup database dijadwalkan
- [ ] Backup folder files dijadwalkan

---

*Dokumen ini disiapkan untuk deploy Sistem Berita Acara Digital*  
*PT Pertamina Patra Niaga SSC ICT — Regional Sumbagsel*  
*Versi Aplikasi: .NET 10 · SQL Server 2019+ · Blazor Server*
