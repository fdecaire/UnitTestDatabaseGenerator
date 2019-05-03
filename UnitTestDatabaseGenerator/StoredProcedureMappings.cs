using System.Text;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class StoredProcedureMappings
    {
        private readonly string _databaseName;
        private readonly string _schemaName;
        private string _storedProcedureName;
        private readonly string _connectionString;
        private string _code;

        public StoredProcedureMappings(string connectionString, string databaseName, string storedProcedureName, string schemaName)
        {
            _databaseName = databaseName;
            _storedProcedureName = storedProcedureName;
            _schemaName = schemaName;
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
                var reader = db.ReadQuery($@"
                    SELECT
                        pr.name,
                        m.definition,
                        pr.type_desc,
                        pr.create_date,
                        pr.modify_date,
                        schema_name(pr.schema_id)
                    FROM
                        sys.procedures pr
                        INNER JOIN sys.sql_modules m ON pr.object_id = m.object_id
                    WHERE
                        is_ms_shipped = 0 AND
                        schema_name(pr.schema_id) = '{_schemaName}' AND
                        pr.name = '{_storedProcedureName}'
                        ");
                while (reader.Read())
                {
                    result = reader["definition"].ToString();
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
            @out.AppendLine("\tpublic class " + _storedProcedureName + " : StoredProc");
            @out.AppendLine("\t{");
            @out.AppendLine("\t\tprivate static StoredProc _instance;");
            @out.AppendLine("\t\tpublic static StoredProc Instance => _instance ?? (_instance = new " + _storedProcedureName + "());");
            @out.AppendLine("\t\tpublic override string Name => \"" + _storedProcedureName + "\";");
            @out.AppendLine("\t\tpublic override string Database => \"" + _databaseName + "\";");
            @out.AppendLine("\t\tpublic override string Code =>");
            @out.AppendLine("\t\t\t@\"" + _code.Replace("\"", "\"\"") + "\";");
            @out.AppendLine("\t}");
            @out.AppendLine("}");


            return @out.ToString();
        }
    }
}
