using System;
using System.Collections.Generic;
using System.Windows.Forms;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
	public partial class frmMain : Form
	{
		public frmMain()
		{
			InitializeComponent();
		}

		private void frmMain_Load(object sender, EventArgs e)
		{
			// lookup all the data severs on this network and allow the user to select one


			//TODO: set the selected item to the last saved item
            txtDestinationDirectory.Text = @"c:\temp\testoutput";
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
                btnGenerate.Text = "Generate";
                lblResult.Visible = true;
                doneTimer.Enabled = false;
            }
        }
    }
}
