using System;
using System.Data;
using System.Data.Sql;
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

		private string GetConnectionString()
		{
			string result = "";

			if (cbUseSQLAuthentication.Checked)
			{
				result = $"server={txtServerName.Text};Trusted_Connection=yes;database=master;User ID={txtUserId.Text};Password={txtPassword.Text}";
			}
			else
			{
				result = $"server={txtServerName.Text};Trusted_Connection=yes;database=master;Integrated Security=true;";
			}

			return result;
		}

		// fill the check list box of databases from the server selected
		private void PopulateDatabaseList()
		{
			// clear the list first
			lstDatabases.Items.Clear();

			var query = "SELECT name, database_id, create_date FROM sys.databases WHERE name NOT IN ('master','tempdb','model','msdb') ORDER BY name";

			using (var db = new ADODatabaseContext(GetConnectionString()))
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
            foreach (int index in lstDatabases.CheckedIndices)
			{
				var nhibernateMappings = new GenerateMappings
					{
						DatabaseName = lstDatabases.Items[index].ToString(),
						ConnectionString = GetConnectionString(),
						GenerateIntegrityConstraintMappings = cbStoreProcMappings.Checked,
						GenerateStoredProcedureMappings = cbStoreProcMappings.Checked,
						GenerateViewMappings = cbViewMappings.Checked,
                        RootDirectory = txtDestinationDirectory.Text
                };
				nhibernateMappings.CreateMappings();
			}

			// indicate that the operation has completed
			btnGenerate.Enabled = false;
			lblResult.Visible = true;
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
            //TODO: use find directory dialog box
        }
    }
}
