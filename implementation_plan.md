# Rebuild UI Sistem Berita Acara — Sesuai Desain Rujukan

## Latar Belakang

Saat ini semua halaman menggunakan template Blazor default (sidebar gelap, layout generik, tanpa styling khusus). Perlu di-rebuild total agar **persis** seperti 23 desain UI/UX yang ada di folder `UI_UX Berita Acara/`.

**Yang sudah ada & berfungsi:**
- Entity models (11 tabel) ✅
- AppDbContext + EF Core ✅
- Seeder data (9 Pegawai, 54 MasterBarang, 5 Users) ✅
- Services: ExcelService, EmailService, NotificationService, BACounterService, DeaktivasiService, DocumentService ✅
- Business logic di halaman-halaman (login, BA CRUD, approval, PJ sign, import Excel) ✅

**Yang perlu di-rebuild total:**
- Seluruh tampilan UI (layout, sidebar, halaman) agar sesuai desain
- Halaman-halaman baru yang belum ada (Draft, Progress, Rejected, Archive per role, Tambah/Edit Pengguna, Data Barang terpisah)
- Role-based sidebar navigation
- Design system (CSS)

---

## Proposed Changes

### Phase 1: Foundation — Design System & Layout

#### [MODIFY] [App.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/App.razor)
- Tambah Google Fonts (Inter)
- Tambah Bootstrap Icons CDN
- Tambah link ke `app.css` yang sudah di-redesign

#### [MODIFY] [app.css](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/wwwroot/app.css)
Rebuild total sebagai design system:
- CSS Variables: warna primary `#1D4ED8`, background `#F8FAFC`, surface `#FFFFFF`, border `#E2E8F0`, text primary `#0F172A`, text secondary `#64748B`
- Status pill badges: Draft `#94A3B8`, WaitingPJSign `#3B82F6`, WaitingApproval `#F59E0B`, Approved `#22C55E`, Rejected `#EF4444`
- Typography: `Inter, sans-serif`
- Layout: sidebar 240px fixed kiri, topbar fixed atas, content area scroll
- Komponen reusable: `.stat-card`, `.data-table`, `.pill-badge`, `.search-box`, `.toggle-switch`, `.card-ba`, `.notification-item`, `.upload-area`, `.modal-popup`

#### [MODIFY] [MainLayout.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Layout/MainLayout.razor)
- Redesign: sidebar kiri (fixed 240px) + main content area (topbar + konten)
- Topbar: nama user + avatar kanan atas
- Baca role user saat ini untuk conditional rendering
- Login page pakai layout polos (tanpa sidebar)

#### [DELETE] [MainLayout.razor.css](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Layout/MainLayout.razor.css)
Semua styling pindah ke `app.css` global

#### [MODIFY] [NavMenu.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Layout/NavMenu.razor)
- Logo Pertamina Patra Niaga di atas
- Tombol "+ Buat Berita Acara" (hanya AdminGudangBarang)
- Menu berbeda per role:
  - **AdminGudangBarang**: Dashboard, Inbox (badge count), Draft (count), Progress (count), Rejected (count), Archive (count) — di bawah: Help, Logout
  - **Approver**: Dashboard, Inbox (badge count), Progress (count), Rejected (count), Archive (count) — di bawah: Help, Logout
  - **AdminIT**: Dashboard, Data Pegawai, Data Barang — di bawah: Help, Logout
- Active state: text biru + border kiri biru 3px
- Notification badge count dari database

#### [DELETE] [NavMenu.razor.css](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Layout/NavMenu.razor.css)
Styling pindah ke `app.css`

---

### Phase 2: Auth & Login

#### [MODIFY] [Login.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Login.razor)
Redesign sesuai desain A:
- Card centered di tengah layar, background `#F8FAFC`
- Logo Pertamina di atas card
- Garis biru di atas card
- Input Email (icon user) + Password (icon lock, toggle visibility)
- Checkbox "Ingat Saya"
- Tombol "Login" biru penuh
- Link "Lupa password? Hubungi Admin IT →"
- Tanpa sidebar (pakai layout khusus)

#### [MODIFY] [Home.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Home.razor)
- Redirect ke `/dashboard` jika sudah login, ke `/login` jika belum

---

### Phase 3: Dashboard per Role

#### [MODIFY] [Dashboard.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Dashboard.razor)
Redesign total berdasarkan role:

**AdminGudangBarang** (desain B):
- 4 stat cards: Total Draft, Total Progress, Total Disetujui, Total Ditolak (dengan icon & warna)
- Line chart "Tren Pembuatan Berita Acara" (6 bulan terakhir) — pakai Chart.js via JS interop
- Donut chart "Status BA Saat Ini" (Selesai, Proses, Ditolak, Draft)
- Progress bars "Jenis BA" (Alokasi %, Peminjaman %, Lainnya %)
- Tabel "Peminjaman Jatuh Tempo" (No. Surat, PJ, Jenis, Tgl Kembali, Status: Hari Ini/H-1/H-2)

**Approver** (desain C):
- 4 stat cards: Antrian Pending (merah), Total Review, Disetujui (% dari total), Ditolak (% dari total)
- Line chart "Tren Persetujuan Bulanan" (2 garis: Disetujui hijau, Ditolak merah)
- Donut chart "Approval/Rejection" (total + angka Disetujui/Ditolak)
- Tabel "Antrean Persetujuan" (No Surat, Jenis BA badge, PJ, Tanggal, tombol Review)

**AdminIT** (desain E — Daftar Pengguna):
- 4 stat cards: Total Pengguna, Admin IT, Admin Gudang & Barang, Approver
- Search by Name + filter dropdown "All Roles" + toggle "Tampilkan Data Non-Aktif"
- Tabel: Nama (+ NIP), Email, Role (badge warna), Tanggal Dibuat, Status (• Active/Inactive), icon edit
- Tombol "+ Tambah Pengguna" kanan atas

---

### Phase 4: Halaman AdminGudangBarang

#### [MODIFY] [BeritaAcara.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/BeritaAcara.razor)
Ganti nama route → `/buat-ba`. Redesign Form BA sesuai desain "Form BA":
- 5 seksi card:
  1. **Info Dokumen**: No Surat (auto-generate, readonly), Jenis BA (dropdown), Tanggal
  2. **Data Penanggung Jawab**: Search pegawai → auto-fill (Cost Center, Jabatan, Fungsi, Alamat, Email, No Pekerja, No Telp)
  3. **Tabel Perangkat**: tabel editable (Jenis Perangkat combobox, Jumlah, Nomor Serial, Keterangan, delete icon) + "+ Tambah Perangkat" + No. Tiket My SSC
  4. **Upload Foto**: drag & drop area
  5. **Tanda Tangan**: 3 kolom (Yang Menyerahkan search, Yang Mengetahui search, Yang Menerima auto-filled dari PJ)
- Tombol "Next" di kanan bawah

#### [NEW] [DraftDokumen.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/DraftDokumen.razor)
Route: `/draft`. Halaman Draft Dokumen sesuai desain:
- Judul "Draft Dokumen" + deskripsi
- Tabel: No. Surat (dengan icon dokumen), Jenis Berita Acara (pill badge), Terakhir Diupdate, Aksi (edit + hapus icon)

#### [NEW] [ProgressBA.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/ProgressBA.razor)
Route: `/progress`. Sesuai desain "Progress BA":
- Judul "Progress Berita Acara" + deskripsi
- Tabel: No. Surat, Jenis BA (badge), Tanggal Submit, Approver, Status (teks biru "Menunggu ttd PJ"/"Menunggu ttd Approver"), icon view

#### [NEW] [RejectedBA.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/RejectedBA.razor)
Route: `/rejected`. Sesuai desain "Rejected":
- Pencarian + Filter (chip Tahun, Jenis, "Bersihkan Semua")
- Card 2-panel per BA: kiri (badge DITOLAK merah, judul, jenis, approver + jabatan, tanggal), kanan (blockquote alasan merah)
- Link "Lihat Detail"

#### [NEW] [ArsipBA.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/ArsipBA.razor)
Route: `/archive`. Sesuai desain "Archive":
- Pencarian + Filter (chip Tahun, "Bersihkan Semua")
- Card 2-panel: kiri (badge DITERIMA hijau, judul, jenis, approver + jabatan, tanggal), kanan (icon PDF, nama file, ukuran, download icon, link "Pratinjau")

#### [MODIFY] [Notifikasi.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Notifikasi.razor)
Redesign sesuai desain "Notifikasi Adm":
- Tab "Semua" / "Belum Dibaca" + "✓ Tandai sudah baca"
- Card per notifikasi: icon warna kiri (hijau ceklis, merah warning, kuning jam, merah jam), judul bold, deskripsi, timestamp kanan atas + dot unread biru

---

### Phase 5: Halaman Approver

Halaman-halaman Approver reuse komponen yang sama tapi dengan data yang berbeda:
- **Dashboard**: render variant Approver
- **Progress** → halaman `/progress` dengan data `WaitingApproval` milik Approver ini
- **Rejected** → sama tapi filter data Approver saat ini
- **Archive** → sama
- **Inbox** → sama tapi notifikasi milik Approver

#### [NEW] [ProgressApprover.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/ProgressApprover.razor)
Route: `/progress` (shared). Halaman "Antrian Persetujuan" sesuai desain G:
- List panel: tiap item = No. Surat (link biru), jenis + nama barang, nama PJ, waktu relatif
- Badge "X Menunggu"

#### [MODIFY] [BeritaAcaraDetail.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/BeritaAcaraDetail.razor)
Redesign sesuai desain "Review BA di Progress - Approver":
- Split layout: kiri preview dokumen (iframe/image area), kanan panel "Review & Status"
- Panel kanan: No. Surat (link biru), jenis + barang, PJ, Fungsi, Tanggal
- Timeline riwayat status: Dibuat (Draft), Diajukan ke Approver, Menunggu Persetujuan Anda
- Tombol "Setujui (Approve)" hijau + "Tolak (Reject)" outline merah
- Modal rejection: textarea "Berikan alasan penolakan" + tombol "Tolak (Reject)" merah

---

### Phase 6: Halaman Admin IT

#### [NEW] [TambahPengguna.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/TambahPengguna.razor)
Route: `/tambah-pengguna`. Sesuai desain I:
- Card centered: search "Masukkan Nama atau NIP..."
- Hasil pencarian = card avatar inisial + nama + NIP
- Tombol "Tambah +"

#### [NEW] [EditPengguna.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/EditPengguna.razor)
Route: `/edit-pengguna/{Id}`. Sesuai desain J:
- Breadcrumb "← Kembali ke Dashboard"
- Card profil: avatar inisial biru besar, Nama, Jabatan, Fungsi/Direktorat, Email, NIP
- Card "Pengaturan Akun": dropdown Role
- Panel kanan:
  - "Keamanan": tombol "Reset Password"
  - "Status Akun": toggle Aktif/Nonaktif
  - "Tanda Tangan": drag & drop TTD
- Footer: Batal + "Simpan Perubahan"

#### [MODIFY] [Pegawai.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Pegawai.razor)
Redesign sesuai desain "Dashboard Data Pegawai":
- Route: `/data-pegawai`
- Judul "Daftar Pegawai" + deskripsi
- Stat card: Total Pegawai
- Search + toggle "Tampilkan Data Non-Aktif"
- Tabel: Nama (+NIP), Jabatan, Fungsi, Email, Status (• Active/Inactive)
- Tombol "+ Update Data Pegawai" (buka popup import Excel)

#### [MODIFY] [MasterBarang.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/MasterBarang.razor)
Redesign sesuai desain "Dashboard Data Pegawai-1" (Data Barang):
- Route: `/data-barang`
- Judul "Daftar Barang" + deskripsi
- Stat card: Total Barang
- Search + toggle "Tampilkan Data Non-Aktif"
- Tabel: Jenis Barang, Status (• Active/Inactive)
- Tombol "+ Update Data Barang" (popup import Excel)

---

### Phase 7: ONLYOFFICE Editor & Preview (PLACEHOLDER)

#### [NEW] [EditorBA.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/EditorBA.razor)
Route: `/editor/{Id}`. Placeholder halaman editor sesuai desain "Edit Manual":
- Header: No. Surat + "DOCUMENT PREVIEW & EDITOR" + "✓ Changes saved" + tombol "Submit for Approval"
- Area utama: placeholder untuk iframe ONLYOFFICE (tampilkan pesan bahwa ONLYOFFICE belum dikonfigurasi)

#### [MODIFY] [PjSign.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/PjSign.razor)
Redesign halaman publik PJ sign — lebih bersih, preview dokumen + upload TTD

---

### Phase 8: Pop-up & Komponen Shared

#### [NEW] [UploadPopup.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Layout/UploadPopup.razor)
Modal drag & drop Excel sesuai desain "upload data Pop Up"

#### [NEW] [RejectPopup.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Layout/RejectPopup.razor)
Modal rejection textarea sesuai desain "Rejection Pop Up"

#### [NEW] [PreviewDokumen.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/PreviewDokumen.razor)
Route: `/preview/{Id}`. Fullscreen preview PDF/DOCX sesuai desain "Preview"

---

### Phase 9: JS Interop & Assets

#### [NEW] [chart-interop.js](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/wwwroot/js/chart-interop.js)
Chart.js interop untuk dashboard charts (line chart, donut chart)

#### [NEW] Logo Pertamina
Copy/generate logo Pertamina Patra Niaga untuk sidebar

---

## Halaman yang Dihapus/Diganti

- [DELETE] [Counter.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Counter.razor) — template default, tidak dipakai
- [DELETE] [Weather.razor](file:///d:/sistem-berita-acara/SistemBeritaAcara.Web/Components/Pages/Weather.razor) — template default, tidak dipakai

---

## Verification Plan

### Build & Run
```bash
dotnet build SistemBeritaAcara.slnx
dotnet run --project SistemBeritaAcara.Web
```

### Manual Verification
- Buka browser, login sebagai masing-masing role
- Screenshot setiap halaman dan bandingkan dengan desain rujukan
- Test navigasi, search, filter, popup, import Excel

> [!IMPORTANT]
> **Scope**: Ini akan mengubah hampir seluruh file di folder `Components/` dan `wwwroot/`. Semua business logic yang sudah ada (create BA, approval, import Excel, dsb) akan **dipertahankan** dan di-refactor ke halaman yang sesuai.

> [!NOTE]
> **ONLYOFFICE & DOCX generation**: Dibuat sebagai placeholder — editor area akan menampilkan pesan bahwa ONLYOFFICE belum dikonfigurasi. Semua kode integrasi siap, tinggal aktifkan Docker.

> [!NOTE]
> **Chart.js**: Akan digunakan untuk line chart dan donut chart di dashboard via JS interop. Ditambahkan via CDN di `App.razor`.
