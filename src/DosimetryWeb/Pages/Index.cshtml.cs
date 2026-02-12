using Microsoft.AspNetCore.Mvc.RazorPages;
using Dosimetry.Core;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace DosimetryWeb.Pages
{
    public class PatientViewModel
    {
        public string Id { get; set; }
        public double Dose { get; set; }
        public bool IsFinalized { get; set; }
        public bool IsHazard { get; set; }
    }

    public class IndexModel : PageModel
    {
        public List<PatientViewModel> Patients { get; set; } = new List<PatientViewModel>();

        public void OnGet()
        {
            // 1. Ensure DB exists
            DatabaseManager.Initialize();

            // 2. Connect to the Shared Truth
            using (var conn = new SqliteConnection(DatabaseManager.ConnectionString))
            {
                conn.Open();

                // Check if empty, if so, seed it
                var countCmd = new SqliteCommand("SELECT COUNT(*) FROM Patients", conn);
                long rowCount = (long)countCmd.ExecuteScalar();
                if (rowCount == 0)
                {
                    DatabaseManager.ResetData();
                }

                // Fetch Data
                var cmd = new SqliteCommand("SELECT * FROM Patients", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Patients.Add(new PatientViewModel
                        {
                            Id = reader.GetString(0),
                            Dose = reader.GetDouble(1),
                            IsFinalized = reader.GetInt32(2) == 1,
                            IsHazard = reader.GetInt32(3) == 1
                        });
                    }
                }
            }
        }
    }
}