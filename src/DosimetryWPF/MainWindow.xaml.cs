using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dosimetry.Core; // Linking to your Core library
using Microsoft.Data.Sqlite;

namespace DosimetryWPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DatabaseManager.Initialize(); // Ensure DB exists when app starts
        }

        // ---------------------------------------------------------
        // HAZARD CONTROL: SAFETY INTERLOCK (HAZ-01)
        // ---------------------------------------------------------
        private void DoseInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(DoseInput.Text, out double currentDose))
            {
                double target = 50.0;
                double limit = target * 1.10; // 110% Tolerance (55.0)

                if (currentDose > limit)
                {
                    // UNSAFE CONDITION -> DISABLE BUTTON
                    SafetyLabel.Text = "Status: HAZARD - DOSE EXCEEDS 110%";
                    SafetyLabel.Foreground = Brushes.Red;
                    FinalizeButton.IsEnabled = false; // The Mitigation
                }
                else
                {
                    // SAFE CONDITION
                    SafetyLabel.Text = "Status: Safe";
                    SafetyLabel.Foreground = Brushes.Green;
                    FinalizeButton.IsEnabled = true;
                }
            }
            else
            {
                // If input is empty or text, disable the button
                FinalizeButton.IsEnabled = false;
            }
        }

        // ---------------------------------------------------------
        // WORKFLOW: FINALIZE PLAN
        // ---------------------------------------------------------
private void FinalizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (PatientDropdown.SelectedItem == null) return;

            string patientId = (PatientDropdown.SelectedItem as ComboBoxItem).Content.ToString();

            if (double.TryParse(DoseInput.Text, out double dose))
            {
                // Save to Shared Database (The "Truth")
                using (var conn = new SqliteConnection(DatabaseManager.ConnectionString))
                {
                    conn.Open();
                    
                    // =================================================================
                    // INTENTIONAL BUG FOR REGRESSION TESTING (REQ-1.2 FAILURE)
                    // We changed IsFinalized = 1 (True) to IsFinalized = 0 (False)
                    // This simulates a developer accidentally breaking the billing logic.
                    // =================================================================
                    var cmd = new SqliteCommand("UPDATE Patients SET IsFinalized = 0, DoseValue = @d WHERE Id = @p", conn);
                    
                    cmd.Parameters.AddWithValue("@d", dose);
                    cmd.Parameters.AddWithValue("@p", patientId);
                    cmd.ExecuteNonQuery();
                }

                // The UI still says "Success", which makes this a dangerous silent failure!
                MessageBox.Show("Plan Finalized Successfully!", "Success");
            }
        }

        private void PatientDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reset UI when patient changes
            if (DoseInput != null) DoseInput.Text = "";
            if (SafetyLabel != null)
            {
                SafetyLabel.Text = "Status: Pending Input";
                SafetyLabel.Foreground = Brushes.Black;
            }
        }
    }
}