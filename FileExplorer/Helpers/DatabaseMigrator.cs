using FileExplorer.Forms;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using System.Text;
using System.Text.RegularExpressions;

namespace FileExplorer.Helpers
{
    /// <summary>
    /// Clase estática con métodos para probar conexión, crear la base de datos,
    /// migrar datos y leer tablas desde SQL Server o MariaDB/MySQL.
    /// Funciona en red local sin necesidad de internet.
    /// </summary>
    public static class DatabaseMigrator
    {
        /// <summary>
        /// Prueba la conexión con las opciones indicadas.
        /// Devuelve true si la conexión fue exitosa.
        /// </summary>
        public static async Task<bool> TestConnectionAsync(MigrationOptions opts)
        {
            try
            {
                switch (opts.Engine)
                {
                    case DbEngine.SqlServer:
                        await using (var c = new SqlConnection(opts.BuildConnectionString()))
                        { await c.OpenAsync(); return true; }
                    case DbEngine.MariaDB:
                        await using (var c = new MySqlConnection(opts.BuildConnectionString()))
                        { await c.OpenAsync(); return true; }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Crea la base de datos destino si no existe, conectándose a master/mysql.
        /// </summary>
        public static async Task EnsureDatabaseExistsAsync(
            MigrationOptions opts, CancellationToken ct = default)
        {
            string db = opts.Database.Trim();
            if (string.IsNullOrEmpty(db)) return;

            switch (opts.Engine)
            {
                case DbEngine.SqlServer:
                    await using (var conn = new SqlConnection(opts.BuildMasterConnectionString()))
                    {
                        await conn.OpenAsync(ct);
                        await ExecSql(conn,
                            $"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name=N'{db}')" +
                            $" CREATE DATABASE [{db}]", ct);
                    }
                    break;
                case DbEngine.MariaDB:
                    await using (var conn = new MySqlConnection(opts.BuildMasterConnectionString()))
                    {
                        await conn.OpenAsync(ct);
                        await ExecMy(conn,
                            $"CREATE DATABASE IF NOT EXISTS `{db}` " +
                            $"CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci", ct);
                    }
                    break;
            }
        }

        /// <summary>
        /// Migra los datos al motor de base de datos seleccionado.
        /// Reporta progreso mediante <paramref name="progress"/>.
        /// </summary>
        public static async Task MigrateAsync(
            MigrationOptions opts, List<string> headers, List<List<string>> rows,
            IProgress<(int done, int total, string msg)>? progress = null,
            CancellationToken ct = default)
        {
            switch (opts.Engine)
            {
                case DbEngine.SqlServer: await MigrateSqlAsync(opts, headers, rows, progress, ct);     break;
                case DbEngine.MariaDB:   await MigrateMariaDbAsync(opts, headers, rows, progress, ct); break;
            }
        }

        /// <summary>
        /// Lee hasta 1000 filas de la tabla indicada y devuelve headers y rows.
        /// </summary>
        public static async Task<(List<string> headers, List<List<string>> rows)>
            ReadTableAsync(MigrationOptions opts, CancellationToken ct = default)
        {
            var headers = new List<string>();
            var rows    = new List<List<string>>();
            string t    = Safe(opts.TableName);

            switch (opts.Engine)
            {
                case DbEngine.SqlServer:
                    await using (var conn = new SqlConnection(opts.BuildConnectionString()))
                    {
                        await conn.OpenAsync(ct);
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"SELECT TOP 1000 * FROM [{t}]";
                        using var reader = await cmd.ExecuteReaderAsync(ct);
                        for (int i = 0; i < reader.FieldCount; i++) headers.Add(reader.GetName(i));
                        while (await reader.ReadAsync(ct))
                        {
                            var row = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                                row.Add(reader.IsDBNull(i) ? "" : reader.GetValue(i)?.ToString() ?? "");
                            rows.Add(row);
                        }
                    }
                    break;
                case DbEngine.MariaDB:
                    await using (var conn = new MySqlConnection(opts.BuildConnectionString()))
                    {
                        await conn.OpenAsync(ct);
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"SELECT * FROM `{t}` LIMIT 1000";
                        await using var reader = await cmd.ExecuteReaderAsync(ct);
                        for (int i = 0; i < reader.FieldCount; i++) headers.Add(reader.GetName(i));
                        while (await reader.ReadAsync(ct))
                        {
                            var row = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                                row.Add(reader.IsDBNull(i) ? "" : reader.GetValue(i)?.ToString() ?? "");
                            rows.Add(row);
                        }
                    }
                    break;
            }
            return (headers, rows);
        }

        // ── SQL Server ────────────────────────────────────────────────

        /// <summary>Migra datos a SQL Server en lotes de 500 filas.</summary>
        static async Task MigrateSqlAsync(MigrationOptions opts,
            List<string> headers, List<List<string>> rows,
            IProgress<(int, int, string)>? prog, CancellationToken ct)
        {
            await using var conn = new SqlConnection(opts.BuildConnectionString());
            await conn.OpenAsync(ct);
            string t = Safe(opts.TableName);

            if (opts.DropIfExists)
                await ExecSql(conn, $"IF OBJECT_ID(N'{t}','U') IS NOT NULL DROP TABLE [{t}]", ct);
            if (opts.CreateTable)
            {
                string cols = string.Join(",\n  ", headers.Select(h => $"[{Safe(h)}] NVARCHAR(500)"));
                await ExecSql(conn, $"CREATE TABLE [{t}] (\n  {cols}\n)", ct);
                prog?.Report((0, rows.Count, $"Tabla [{t}] creada en SQL Server"));
            }

            int done = 0;
            foreach (var batch in rows.Chunk(500))
            {
                ct.ThrowIfCancellationRequested();
                using var cmd = conn.CreateCommand();
                var sb    = new StringBuilder();
                sb.Append($"INSERT INTO [{t}] ({string.Join(",", headers.Select(h => $"[{Safe(h)}]"))}) VALUES ");
                var parts = new List<string>(); int pi = 0;
                foreach (var row in batch)
                {
                    var ps = headers.Select((_, i) =>
                    {
                        string pn = $"@p{pi++}";
                        cmd.Parameters.AddWithValue(pn, i < row.Count ? (object)row[i] : DBNull.Value);
                        return pn;
                    }).ToList();
                    parts.Add($"({string.Join(",", ps)})");
                }
                cmd.CommandText = sb.Append(string.Join(",", parts)).ToString();
                await cmd.ExecuteNonQueryAsync(ct);
                done += batch.Length;
                prog?.Report((done, rows.Count, $"SQL Server: {done}/{rows.Count} filas"));
            }
            prog?.Report((rows.Count, rows.Count, "Completado en SQL Server"));
        }

        // ── MariaDB / MySQL ───────────────────────────────────────────

        /// <summary>Migra datos a MariaDB/MySQL en lotes de 500 filas.</summary>
        static async Task MigrateMariaDbAsync(MigrationOptions opts,
            List<string> headers, List<List<string>> rows,
            IProgress<(int, int, string)>? prog, CancellationToken ct)
        {
            await using var conn = new MySqlConnection(opts.BuildConnectionString());
            await conn.OpenAsync(ct);
            string t = Safe(opts.TableName);

            if (opts.DropIfExists)
                await ExecMy(conn, $"DROP TABLE IF EXISTS `{t}`", ct);
            if (opts.CreateTable)
            {
                string cols = string.Join(",\n  ", headers.Select(h => $"`{Safe(h)}` TEXT"));
                await ExecMy(conn,
                    $"CREATE TABLE `{t}` (\n  {cols}\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", ct);
                prog?.Report((0, rows.Count, $"Tabla `{t}` creada en MariaDB"));
            }

            int done = 0;
            foreach (var batch in rows.Chunk(500))
            {
                ct.ThrowIfCancellationRequested();
                await using var cmd = conn.CreateCommand();
                var sb    = new StringBuilder();
                sb.Append($"INSERT INTO `{t}` ({string.Join(",", headers.Select(h => $"`{Safe(h)}`"))}) VALUES ");
                var parts = new List<string>(); int pi = 0;
                foreach (var row in batch)
                {
                    var ps = headers.Select((_, i) =>
                    {
                        string pn = $"@p{pi++}";
                        cmd.Parameters.AddWithValue(pn, i < row.Count ? (object)row[i] : DBNull.Value);
                        return pn;
                    }).ToList();
                    parts.Add($"({string.Join(",", ps)})");
                }
                cmd.CommandText = sb.Append(string.Join(",", parts)).ToString();
                await cmd.ExecuteNonQueryAsync(ct);
                done += batch.Length;
                prog?.Report((done, rows.Count, $"MariaDB: {done}/{rows.Count} filas"));
            }
            prog?.Report((rows.Count, rows.Count, "Completado en MariaDB"));
        }

        static async Task ExecSql(SqlConnection c, string sql, CancellationToken ct)
        { using var cmd = c.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(ct); }

        static async Task ExecMy(MySqlConnection c, string sql, CancellationToken ct)
        { await using var cmd = c.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(ct); }

        /// <summary>Sanitiza un nombre de columna o tabla eliminando caracteres no alfanuméricos.</summary>
        public static string Safe(string name) => Regex.Replace(name.Trim(), @"[^\w]", "_");
    }
}
