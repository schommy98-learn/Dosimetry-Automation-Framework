using System;
using Microsoft.Data.Sqlite;
using System.IO;

namespace Dosimetry.Core
{
    public static class DatabaseManager
    {
        // FIX: Use a shared absolute path so both apps see the SAME file.
        // This puts 'medical.db' in your Documents folder.
        public static string DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "medical_shared.db");
        public static string ConnectionString = $"Data Source={DbPath}";

        public static void Initialize()
        {
            // Debugging Help: Print where the DB is living
            Console.WriteLine($"[Core] Connecting to Database at: {DbPath}");

            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                string sql = @"
                    CREATE TABLE IF NOT EXISTS Patients (
                        Id TEXT PRIMARY KEY, 
                        DoseValue REAL, 
                        IsFinalized INTEGER, 
                        IsHighRisk INTEGER
                    )";
                
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void ResetData()
        {
            Initialize();
            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                
                var cmd = new SqliteCommand("DELETE FROM Patients", conn);
                cmd.ExecuteNonQuery();

                var seedCmd = new SqliteCommand("INSERT INTO Patients (Id, DoseValue, IsFinalized, IsHighRisk) VALUES ('Patient_Normal', 0, 0, 0)", conn);
                seedCmd.ExecuteNonQuery();
                
                var hazardCmd = new SqliteCommand("INSERT INTO Patients (Id, DoseValue, IsFinalized, IsHighRisk) VALUES ('Patient_Hazard', 0, 0, 1)", conn);
                hazardCmd.ExecuteNonQuery();
            }
        }
    }
}