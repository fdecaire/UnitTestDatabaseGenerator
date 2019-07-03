using System;
using System.Text;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class FunctionMappings
    {
        private readonly string _databaseName;
        private readonly string _schemaName;
        private string _functionName;
        private readonly string _connectionString;
        private string _code;

        public FunctionMappings(string connectionString, string databaseName, string functionName, string schemaName)
        {
            _databaseName = databaseName;
            _functionName = functionName;
            _schemaName = schemaName;
            _connectionString = connectionString;

            NormalizeFunctionName();

            _code = LookupFunctionCode();
        }

        private string LookupFunctionCode()
        {
            var result = "";

            using (var db = new ADODatabaseContext(_connectionString.Replace("master", _databaseName)))
            {
                var reader = db.ReadQuery($@"
                    select
                        *
                    from 
                        sys.sql_modules
                    where 
                        object_name(object_id) = '{_functionName}'
                        ");
                while (reader.Read())
                {
                    result = reader["definition"].ToString();
                }
            }

            return result;
        }

        private void NormalizeFunctionName()
        {
            _functionName = _functionName.Replace("~", "_tilde_");
            _functionName = _functionName.Replace("-", "_dash_");
            _functionName = _functionName.Replace(".", "_dot_");
        }

        public string EmitCode()
        {
            var @out = new StringBuilder();

            @out.AppendLine("using UnitTestHelperLibrary;");
            @out.AppendLine("");
            @out.AppendLine("namespace DataLayer." + _databaseName.FixSpecialCharacters() + ".Functions");
            @out.AppendLine("{");
            @out.AppendLine("\t// DO NOT MODIFY! This code is auto-generated.");
            @out.AppendLine("\tpublic class " + _functionName + " : StoredProc");
            @out.AppendLine("\t{");
            @out.AppendLine("\t\tprivate static StoredProc _instance;");
            @out.AppendLine($"\t\tpublic static StoredProc Instance => _instance ?? (_instance = new {_functionName}());");
            @out.AppendLine($"\t\tpublic override string Name => \"{_functionName}\";");
            @out.AppendLine($"\t\tpublic override string Database => \"{_databaseName}\";");
            @out.AppendLine("\t\tpublic override string Code => @\"" + _code.Replace("\"", "\"\"") + "\";");
            @out.AppendLine("\t}");
            @out.AppendLine("}");

            return @out.ToString();
        }

    }
}
