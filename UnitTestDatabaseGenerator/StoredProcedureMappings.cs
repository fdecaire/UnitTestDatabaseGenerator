using System.Text;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class StoredProcedureMappings
    {
        private readonly string _databaseName;
        private string _storedProcedureName;
        private readonly string _connectionString;
        private string _code;

        public StoredProcedureMappings(string connectionString, string databaseName, string storedProcedureName)
        {
            _databaseName = databaseName;
            _storedProcedureName = storedProcedureName;
            _connectionString = connectionString;

            NormalizeStoredProcedureName();

            _code = LookupStoredProcedureCode();
        }

        private void NormalizeStoredProcedureName()
        {
            _storedProcedureName = _storedProcedureName.Replace("~", "_tilde_");
            _storedProcedureName = _storedProcedureName.Replace("-", "_dash_");
            _storedProcedureName = _storedProcedureName.Replace(".", "_dot_");
        }

        private string LookupStoredProcedureCode()
        {
            var result = "";

            using (var db = new ADODatabaseContext(_connectionString.Replace("master", _databaseName)))
            {
                var reader = db.ReadQuery("SELECT OBJECT_DEFINITION(OBJECT_ID('" + _storedProcedureName + "')) AS code");
                while (reader.Read())
                {
                    result = reader["code"].ToString();
                }
            }

            return result;
        }

        public string EmitCode()
        {
            var @out = new StringBuilder();

            _code = _code.Replace("ALTER PROCEDURE", "CREATE PROCEDURE");

            _code = @"USE [" + _databaseName + @"]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO 
" + _code;

            @out.AppendLine("using UnitTestHelperLibrary;");
            @out.AppendLine("");
            @out.AppendLine("namespace DataLayer." + _databaseName + ".StoredProcedures");
            @out.AppendLine("{");
            @out.AppendLine("\t// DO NOT MODIFY! This code is auto-generated.");
            @out.AppendLine("\tpublic partial class " + _storedProcedureName + " : StoredProc");
            @out.AppendLine("\t{");
            @out.AppendLine("\t\tprivate static StoredProc _instance;");
            @out.AppendLine("\t\tpublic static StoredProc Instance");
            @out.AppendLine("\t\t{");
            @out.AppendLine("\t\t\tget { return _instance ?? (_instance = new " + _storedProcedureName + "()); }");
            @out.AppendLine("\t\t}");
            @out.AppendLine("\t\toverride public string Name");
            @out.AppendLine("\t\t{");
            @out.AppendLine("\t\t\tget { return \"" + _storedProcedureName + "\"; }");
            @out.AppendLine("\t\t}");
            @out.AppendLine("\t\toverride public string Database { get { return \"" + _databaseName + "\"; } }");
            @out.AppendLine("\t\toverride public string Code");
            @out.AppendLine("\t\t{");
            @out.AppendLine("\t\t\tget");
            @out.AppendLine("\t\t\t{");
            @out.AppendLine("\t\t\treturn @\"" + _code.Replace("\"", "\"\"") + "\";");
            @out.AppendLine("\t\t\t}");
            @out.AppendLine("\t\t}");
            @out.AppendLine("\t}");
            @out.AppendLine("}");


            return @out.ToString();
        }
    }
}
