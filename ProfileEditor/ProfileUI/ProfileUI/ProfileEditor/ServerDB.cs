using ApexCharts;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ProfileUI.ProfileEditor
{
    public class ServerDB
    {
        private readonly IConfiguration _config;

        public ServerDB(IConfiguration config)
        {
            _config = config;
        }
        public async Task<bool> DeleteTable(string name)
        { 
            if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsLetterOrDigit(c) && c != '_')) 
                return false;
            string dropTableSql = $"DROP TABLE IF EXISTS `{name}`;"; 
            string deleteProfileSql = "DELETE FROM profilelist WHERE name = @name;";
            try { 
                using var conn = new MySqlConnection(_config.GetConnectionString("MariaDb")); 
                await conn.OpenAsync();

                // 1. Remove the entry from profilelist
                using (var cmd = new MySqlCommand(deleteProfileSql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    await cmd.ExecuteNonQueryAsync();
                }
                if (_config == null) 
                    throw new Exception("_config is NULL");
                // 2. Drop the table
                using (var cmd = new MySqlCommand(dropTableSql, conn)) 
                { 
                    await cmd.ExecuteNonQueryAsync(); 
                }                 
                return true; 
            } 
            catch 
            { return false; } 
        }
        public async Task<bool> CreateNewTable(string name)
        {
            // Validate table name to avoid SQL injection
            if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsLetterOrDigit(c) && c != '_')) 
              return false;

            string createTableSql = $"CREATE TABLE `{name}` (" + "time DECIMAL, " + "mass DECIMAL, " + "speed DECIMAL" + ");";
            string insertProfileSql = "INSERT INTO profilelist (name) VALUES (@name);";
            try {
                using var conn = new MySqlConnection(_config.GetConnectionString("MariaDb")); 
                await conn.OpenAsync();
                // 1. Create the table
                using (var cmd = new MySqlCommand(createTableSql, conn)) 
                { 
                    await cmd.ExecuteNonQueryAsync(); 
                } 
                // 2. Insert the name into profilelist
                using (var cmd = new MySqlCommand(insertProfileSql, conn)) 
                { 
                    cmd.Parameters.AddWithValue("@name", name);
                    await cmd.ExecuteNonQueryAsync(); 
                }

                return true; 
            } 
            catch (Exception ex) 
            {  
               return false; 
            }
        }
        public async Task<List<string>> GetExistingProfileNames()
        {
            var result = new List<string>(); 
            using var conn = new MySqlConnection(_config.GetConnectionString("MariaDb")); 
            await conn.OpenAsync();

            var table = "profilelist";
            var sql = $"SELECT name FROM {table};";

            using var cmd = new MySqlCommand(sql, conn); 
            using var reader = await cmd.ExecuteReaderAsync(); 
            while (await reader.ReadAsync()) 
            { 
                result.Add(reader.GetString(0)); 
            }
            return result;
        }
        public async Task<List<MainGridRow>> GetProfileData(string table)
        {
            var result = new List<MainGridRow>();

            using var conn = new MySqlConnection(
                _config.GetConnectionString("MariaDb"));

            await conn.OpenAsync();

            var sql = $"SELECT time, mass, speed FROM {table};";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new MainGridRow
                {
                    Col1 = reader.GetDecimal("time"),
                    Col2 = reader.GetDecimal("mass"),
                    Col3 = reader.GetDecimal("speed"),
                });
            }

            return result;
        }

        public async Task UpdateData(string table, List<MainGridRow> newValues)
        {
            if (!Regex.IsMatch(table, @"^[a-zA-Z0-9_]+$"))
                throw new ArgumentException("Invalid table name");
            DateTime start = DateTime.UtcNow;
            using var conn = new MySqlConnection(_config.GetConnectionString("MariaDb"));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();
            try
            {
                var deleteCmd = new MySqlCommand($"DELETE FROM `{table}`;", conn, transaction);
                await deleteCmd.ExecuteNonQueryAsync();
                // Prepare INSERT
                var sql = new StringBuilder();
                sql.Append($"INSERT INTO `{table}` (time, mass, speed) VALUES ");
                for (int i = 0; i < newValues.Count; i++)
                {
                    sql.Append($"(@t{i}, @m{i}, @s{i}),");
                }
                sql.Length--;  // remove last comma
                var cmd = new MySqlCommand(sql.ToString(), conn, transaction);
                for (int i = 0; i < newValues.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@t{i}", newValues[i].Col1);
                    cmd.Parameters.AddWithValue($"@m{i}", newValues[i].Col2);
                    cmd.Parameters.AddWithValue($"@s{i}", newValues[i].Col3);
                }
                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

            }
            var ms = (DateTime.UtcNow - start).TotalMilliseconds;
        }

    }
}
