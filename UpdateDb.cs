using System;
using Microsoft.Data.SqlClient;

class Program {
    static void Main() {
        var connStr = ""Server=localhost\\SQLEXPRESS;Database=SistemBeritaAcara;Trusted_Connection=True;TrustServerCertificate=True;"";
        using var conn = new SqlConnection(connStr);
        conn.Open();
        
        var cmds = new[] {
            ""ALTER TABLE [BeritaAcara] DROP CONSTRAINT IF EXISTS [FK_BeritaAcara_Pegawai_PjId];"",
            ""ALTER TABLE [BeritaAcara] DROP CONSTRAINT IF EXISTS [FK_BeritaAcara_Pegawai_MenyerahkanId];"",
            ""ALTER TABLE [BeritaAcara] DROP CONSTRAINT IF EXISTS [FK_BeritaAcara_AspNetUsers_MengetahuiId];"",

            ""ALTER TABLE [BeritaAcara] ALTER COLUMN [PjId] int NULL;"",
            ""ALTER TABLE [BeritaAcara] ALTER COLUMN [MenyerahkanId] int NULL;"",
            ""ALTER TABLE [BeritaAcara] ALTER COLUMN [MengetahuiId] int NULL;"",

            ""ALTER TABLE [BeritaAcara] ADD CONSTRAINT [FK_BeritaAcara_Pegawai_PjId] FOREIGN KEY ([PjId]) REFERENCES [Pegawai]([Id]);"",
            ""ALTER TABLE [BeritaAcara] ADD CONSTRAINT [FK_BeritaAcara_Pegawai_MenyerahkanId] FOREIGN KEY ([MenyerahkanId]) REFERENCES [Pegawai]([Id]);"",
            ""ALTER TABLE [BeritaAcara] ADD CONSTRAINT [FK_BeritaAcara_AspNetUsers_MengetahuiId] FOREIGN KEY ([MengetahuiId]) REFERENCES [AspNetUsers]([Id]);""
        };
        
        foreach (var sql in cmds) {
            try {
                using var cmd = new SqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                Console.WriteLine(""Success: "" + sql);
            } catch (Exception ex) {
                Console.WriteLine(""Error on: "" + sql + ""\n"" + ex.Message);
            }
        }
    }
}
