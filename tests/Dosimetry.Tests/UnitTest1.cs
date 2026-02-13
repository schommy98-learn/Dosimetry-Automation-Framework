using NUnit.Framework;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using FlaUI.Core;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using Microsoft.Playwright;
using Dosimetry.Core;
using System;
using System.Runtime.InteropServices; // Required for OS Detection
using AventStack.ExtentReports;
using AventStack.ExtentReports.Reporter;

namespace Dosimetry.Tests
{
    [TestFixture]
    public class VerificationSuite
    {
        // REPORTING ENGINES
        private ExtentReports _extent;
        private ExtentTest _currentTest;
        private string _wpfAppPath;
        private string _webAppPath;
        private string _reportFolder;

        [OneTimeSetUp]
        public void SetupAudit()
        {
            // 1. SETUP REPORTING DIRECTORY
            string rootDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../"));
            _reportFolder = Path.Combine(rootDir, "Reports");
            Directory.CreateDirectory(_reportFolder);

            // 2. CONFIGURE HTML REPORT
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string reportPath = Path.Combine(_reportFolder, $"Verification_Run_{timestamp}.html");
            
            var htmlReporter = new ExtentSparkReporter(reportPath);
            htmlReporter.Config.DocumentTitle = "Medical Device Verification";
            htmlReporter.Config.ReportName = "Dosimetry Verification Suite"; 
            htmlReporter.Config.Theme = AventStack.ExtentReports.Reporter.Config.Theme.Standard;

            _extent = new ExtentReports();
            _extent.AttachReporter(htmlReporter);

            // 3. LOCATE APPS
            _wpfAppPath = Path.Combine(rootDir, "src/DosimetryWPF/bin/Debug/net8.0-windows/DosimetryWPF.exe");
            _webAppPath = Path.Combine(rootDir, "src/DosimetryWeb");

            // ---------------------------------------------------------
            // DYNAMIC ENVIRONMENT CAPTURE (THE "MATRIX" LOGIC)
            // ---------------------------------------------------------
            string osDescription = RuntimeInformation.OSDescription;
            string machineName = Environment.MachineName;
            
            // Check if we are running in GitHub Actions or Local
            string runId = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER") ?? "Local Run";
            string workflow = Environment.GetEnvironmentVariable("GITHUB_WORKFLOW") ?? "Manual Debug";
            string branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? "Local Branch";

            // Safely get version
            string wpfVersion = "1.0.0";
            if(File.Exists(_wpfAppPath))
            {
                 wpfVersion = FileVersionInfo.GetVersionInfo(_wpfAppPath).FileVersion;
            }

            // INJECT INTO DASHBOARD HEADER
            _extent.AddSystemInfo("Host OS", osDescription);
            _extent.AddSystemInfo("Machine Name", machineName);
            _extent.AddSystemInfo("Run Source", workflow);
            _extent.AddSystemInfo("Build/Run ID", runId);
            _extent.AddSystemInfo("Branch", branch);
            _extent.AddSystemInfo("WPF App Version", wpfVersion);
            _extent.AddSystemInfo("SRS Manifest", "v1.5.0");

            DatabaseManager.ResetData();
        }

        [OneTimeTearDown]
        public void GenerateReport()
        {
            _extent.Flush();
            Console.WriteLine($"\n[AUDIT] Dashboard generated at: {_reportFolder}");
        }

        // ---------------------------------------------------------
        // TEST CASE 1: HAPPY PATH
        // ---------------------------------------------------------
        [Test, Order(1)]
        [Category("Functional")]
        [Property("SRS", "REQ-1.2: Billing Integration")]
        public async Task TC01_GoldenPath_BillingIntegration()
        {
            StartTest("TC-01: Billing Integration", "REQ-1.2");
            
            var webProcess = StartWebApp(_webAppPath);
            System.Threading.Thread.Sleep(4000); 

            try
            {
                var app = Application.Launch(_wpfAppPath);
                using (var automation = new UIA3Automation())
                {
                    var window = app.GetMainWindow(automation);
                    var doseInput = window.FindFirstDescendant(cf => cf.ByAutomationId("DoseInput")).AsTextBox();
                    doseInput.Enter("50.0");
                    
                    var finalizeBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("FinalizeButton")).AsButton();
                    finalizeBtn.Invoke();
                    
                    System.Threading.Thread.Sleep(1000); 
                    app.Close();
                }

                using var playwright = await Playwright.CreateAsync();
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();
                await page.GotoAsync("http://localhost:5070");
                
                var statusText = await page.Locator("table tr:has-text('Patient_Normal') td:last-child").InnerTextAsync();
                
                Assert.That(statusText, Does.Contain("Ready to Bill"));
                LogPass("Web Portal showed 'Ready to Bill'");
            }
            catch (Exception ex)
            {
                LogFail(ex.Message);
                throw;
            }
            finally
            {
                if (!webProcess.HasExited) webProcess.Kill();
            }
        }

        // ---------------------------------------------------------
        // TEST CASE 2: SAFETY INTERLOCK
        // ---------------------------------------------------------
        [Test, Order(2)]
        [Category("Safety")]
        [Property("Hazard", "HAZ-01: Overdose Protection")]
        public void TC02_SafetyInterlock_PreventOverdose()
        {
            StartTest("TC-02: Safety Interlock", "HAZ-01");

            var app = Application.Launch(_wpfAppPath);
            using (var automation = new UIA3Automation())
            {
                var window = app.GetMainWindow(automation);
                window.FindFirstDescendant(cf => cf.ByAutomationId("DoseInput")).AsTextBox().Enter("56.0");

                var btn = window.FindFirstDescendant(cf => cf.ByAutomationId("FinalizeButton")).AsButton();
                bool isEnabled = btn.IsEnabled;
                
                var label = window.FindFirstDescendant(cf => cf.ByAutomationId("SafetyLabel")).AsLabel();
                string warningText = label.Text;
                
                app.Close();

                if (!isEnabled && warningText.Contains("HAZARD"))
                {
                    LogPass("Button Disabled & Warning Displayed");
                }
                else
                {
                    LogFail($"Safety Check Failed. Button Enabled: {isEnabled}, Text: {warningText}");
                    Assert.Fail("Safety Interlock Failed");
                }
            }
        }

        // ---------------------------------------------------------
        // TEST CASE 3: DATA PERSISTENCE
        // ---------------------------------------------------------
        [Test, Order(3)]
        [Category("Reliability")]
        [Property("SRS", "REQ-1.0: Data Persistence")]
        [Property("TestVersion", "1.2")] 
        public void TC03_DataPersistence_Restart()
        {
            StartTest("TC-03: Data Persistence", "REQ-1.0");

            var app1 = Application.Launch(_wpfAppPath);
            using (var automation = new UIA3Automation())
            {
                var window = app1.GetMainWindow(automation);
                window.FindFirstDescendant(cf => cf.ByAutomationId("PatientDropdown")).AsComboBox().Select("Patient_Hazard");
                window.FindFirstDescendant(cf => cf.ByAutomationId("DoseInput")).AsTextBox().Enter("55.0");
                app1.Close();
            }

            DatabaseManager.Initialize(); 
            
            bool isHazardFinalized = false;
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(DatabaseManager.ConnectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                //For testing bad test step: change Patients WHERE Id = Patient_Normal, is pass and Patient_Hazard is fail
                cmd.CommandText = "SELECT IsFinalized FROM Patients WHERE Id = 'Patient_Hazard'";
                long result = (long)cmd.ExecuteScalar();
                isHazardFinalized = (result == 1);
            }

            if(isHazardFinalized)
            {
                LogPass("Database retained status after restart");
            }
            else
            {
                LogFail($"Persistence Failed! Expected 'Patient_Hazard' to be Finalized, but DB said: {isHazardFinalized}");
                Assert.Fail("Persistence Failed - Outdated Test Expectation");
            }
        }

        // ---------------------------------------------------------
        // DASHBOARD HELPERS
        // ---------------------------------------------------------
        private void StartTest(string testName, string requirement)
        {
            var version = TestContext.CurrentContext.Test.Properties.Get("TestVersion") as string ?? "1.0";
            _currentTest = _extent.CreateTest($"{testName} (v{version})").AssignCategory(requirement);
        }

        private void LogPass(string message)
        {
            _currentTest.Pass(message);
        }

        private void LogFail(string message)
        {
            _currentTest.Fail(message);
        }

        private Process StartWebApp(string projectPath)
        {
            // Simplified syntax to prevent compiler errors
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = "dotnet";
            
            // Note: We use the forward slash which works in dotnet CLI argument strings
            startInfo.Arguments = $"run --project \"{projectPath}/DosimetryWeb.csproj\" --urls=http://localhost:5070";
            
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            return Process.Start(startInfo);
        }
    }
}