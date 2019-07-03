using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
	public partial class frmMain : Form
    {
        private string REGISTRY_SUBKEY_STRING = @"SOFTWARE\UnitTestDatabaseGenerator";
        private string REGISTRY_DESTINATION_DIRECTORY_STRING = "DestinationDirectory";

		public frmMain()
		{
			InitializeComponent();
		}

		private void frmMain_Load(object sender, EventArgs e)
		{
			// lookup all the data severs on this network and allow the user to select one


            txtDestinationDirectory.Text = ReadDirectoryFromRegistry();
        }

        private void SaveDirectoryInRegistry(string directory)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(REGISTRY_SUBKEY_STRING))
            {
                key?.SetValue(REGISTRY_DESTINATION_DIRECTORY_STRING, directory);
            }
        }

        private string ReadDirectoryFromRegistry()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(REGISTRY_SUBKEY_STRING))
            {
                if (key != null)
                {
                    return key.GetValue(REGISTRY_DESTINATION_DIRECTORY_STRING).ToString();
                }
            }

            return @"c:\temp\testoutput";
        }

		private string GetConnectionString
		{
            get
            {
                if (cbUseSQLAuthentication.Checked)
                {
                   return $"server={txtServerName.Text};Trusted_Connection=yes;database=master;User ID={txtUserId.Text};Password={txtPassword.Text}";
                }

                return $"server={txtServerName.Text};Trusted_Connection=yes;database=master;Integrated Security=true;";
            }
        }

		// fill the check list box of databases from the server selected
		private void PopulateDatabaseList()
		{
			// clear the list first
			lstDatabases.Items.Clear();

			var query = "SELECT name, database_id, create_date FROM sys.databases WHERE name NOT IN ('master','tempdb','model','msdb') ORDER BY name";

			using (var db = new ADODatabaseContext(GetConnectionString))
			{
				var reader = db.ReadQuery(query);
				while (reader.Read())
				{
					lstDatabases.Items.Add(reader["name"].ToString());
				}
			}

			// disable the generate button
			btnGenerate.Enabled = false;
			lblResult.Visible = false;
		}

		private void lstDatabases_SelectedValueChanged(object sender, EventArgs e)
		{
			// check to see if at least one item is checked, enable the generate button.  Otherwise disable button.
			btnGenerate.Enabled = (lstDatabases.CheckedIndices.Count > 0);
			lblResult.Visible = false;
		}
        
		private void btnGenerate_Click(object sender, EventArgs e)
		{
            if (btnGenerate.Text == "Stop")
            {
                MasterProcessor.Instance.Stop();
                lblResult.Text = "Process Cancelled";
                return;
            }

            SaveDirectoryInRegistry(txtDestinationDirectory.Text);

            var databaseList = new List<string>();
            foreach (int index in lstDatabases.CheckedIndices)
            {
                databaseList.Add(lstDatabases.Items[index].ToString());
            }

            MasterProcessor.Instance.Start(databaseList, GetConnectionString, cbRelationalIntegrityMappings.Checked,
                cbStoreProcMappings.Checked, cbViewMappings.Checked,
                cbFunctionMappings.Checked, txtDestinationDirectory.Text);

            btnGenerate.Text = "Stop";
            lblResult.Text = "Process Complete";
            lblResult.Visible = false;
            doneTimer.Enabled = true;
        }

		private void btnConnect_Click(object sender, EventArgs e)
		{
			PopulateDatabaseList();
		}

		private void cbUseSQLAuthentication_CheckedChanged(object sender, EventArgs e)
		{
			if (cbUseSQLAuthentication.Checked)
			{
				txtUserId.Enabled = true;
				txtPassword.Enabled = true;
			}
			else
			{
				txtUserId.Enabled = false;
				txtPassword.Enabled = false;
			}
		}

        private void btnDirectorySelector_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                var result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    txtDestinationDirectory.Text = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void DoneTimer_Tick(object sender, EventArgs e)
        {
            if (MasterProcessor.Instance.Stopped)
            {
                // indicate that the operation has completed
                if (lblResult.Text == "Process Cancelled")
                {
                    progressBar.Value = 0;
                }
                else
                {
                    progressBar.Value = progressBar.Maximum;
                }

                btnGenerate.Text = "Generate";
                lblResult.Visible = true;
                doneTimer.Enabled = false;
            }

            if (!lblResult.Visible)
            {
                progressBar.Value = MasterProcessor.Instance.PercentComplete;
                progressBar.Minimum = 0;
                progressBar.Maximum = MasterProcessor.Instance.TotalObjects;
            }
        }
    }
}
