using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class TableGeneratorMappings
    {
        private readonly string _databaseName;
        private readonly string _connectionString;
        private readonly CancellationToken _token;

        public TableGeneratorMappings(string connectionString, string databaseName, object action)
        {
            _databaseName = databaseName;
            _connectionString = connectionString;
            _token = (CancellationToken)action;
        }

        private List<string> ReadPrimaryKeyList(string tableName)
        {
            var keyList = new List<string>();

            var query = "SELECT * FROM [" + _databaseName + "].INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE table_name='" + tableName + "' AND CONSTRAINT_NAME LIKE 'PK_%' ORDER BY ORDINAL_POSITION";

            using (var db = new ADODatabaseContext(_connectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    keyList.Add(reader["COLUMN_NAME"].ToString());
                }
            }

            return keyList;
        }

        public string EmitCode(string tableDefinitionString)
        {
            var @out = new StringBuilder();

            @out.AppendLine("using System;");
            @out.AppendLine("using UnitTestHelperLibrary;");
            @out.AppendLine("using System.Collections.Generic;");
            @out.AppendLine("");
            @out.AppendLine("namespace DataLayer." + _databaseName + ".TableGenerator");
            @out.AppendLine("{");
            @out.AppendLine("\t// DO NOT MODIFY! This code is auto-generated.");
            @out.AppendLine("\tpublic partial class " + _databaseName + "Tables");
            @out.AppendLine("\t{");
            @out.AppendLine("\t\tpublic static string DatabaseName {");
            @out.AppendLine("\t\t\tget ");
            @out.AppendLine("\t\t\t{");
            @out.AppendLine("\t\t\t\treturn \"" + _databaseName + "\";");
            @out.AppendLine("\t\t\t}");
            @out.AppendLine("\t\t}");
            @out.AppendLine("");

            @out.AppendLine("\t\tpublic static List<TableDefinition> TableList = new List<TableDefinition> {");

            @out.AppendLine(tableDefinitionString);

            @out.AppendLine("\t\t};");
            @out.AppendLine("\t}");
            @out.AppendLine("}");

            return @out.ToString();
        }

        public string EmitTableGenerateCode(string tableName, string schema)
        {
            var @out = new StringBuilder();
            var firstTime = true;

            @out.Append("Name=\"" + tableName + "\", ");

            @out.Append($"CreateScript=\"CREATE TABLE [{schema}].[{tableName}](");
            var query = $"SELECT * FROM [{_databaseName}].INFORMATION_SCHEMA.COLUMNS WHERE table_name='{tableName}' ORDER BY ORDINAL_POSITION";
            using (var db = new ADODatabaseContext(_connectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    if (_token.IsCancellationRequested)
                    {
                        return "";
                    }

                    if (firstTime)
                    {
                        firstTime = false;
                    }
                    else
                    {
                        @out.Append(",");
                    }

                    @out.Append($"[{reader["COLUMN_NAME"]}]");
                    @out.Append($"[{reader["DATA_TYPE"]}]");

                    switch (reader["DATA_TYPE"].ToString().ToLower())
                    {
                        case "char":
                        case "nchar":
                        case "varchar":
                        case "nvarchar":
                        case "varbinary":
                            if (reader["CHARACTER_MAXIMUM_LENGTH"].ToString() == "-1")
                            {
                                @out.Append("(MAX)");
                            }
                            else
                            {
                                @out.Append($"({reader["CHARACTER_MAXIMUM_LENGTH"]})");
                            }
                            break;
                        case "numeric":
                        case "money":
                            @out.Append($"({reader["NUMERIC_PRECISION"]}");

                            if (reader["NUMERIC_SCALE"].ToString() != "")
                            {
                                @out.Append($",{reader["NUMERIC_SCALE"]}");
                            }

                            @out.Append(")");
                            break;
                    }

                    // output identity field information
                    @out.Append(GetIdentitySeedAndIncrementValues(tableName, reader["COLUMN_NAME"].ToString()));

                    if (reader["IS_NULLABLE"].ToString() != "YES")
                    {
                        @out.Append(" NOT NULL");
                    }
                }
            }

            // get the primary key
            firstTime = true;
            query = $"SELECT * FROM [{_databaseName}].INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE table_name='{tableName}' AND CONSTRAINT_NAME LIKE 'PK_%' ORDER BY ORDINAL_POSITION";
            using (var db = new ADODatabaseContext(_connectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    if (_token.IsCancellationRequested)
                    {
                        return "";
                    }

                    if (firstTime)
                    {
                        @out.Append($", CONSTRAINT [{reader["CONSTRAINT_NAME"]}] PRIMARY KEY CLUSTERED (");
                        firstTime = false;
                    }
                    else
                    {
                        @out.Append(",");
                    }

                    @out.Append($"[{reader["COLUMN_NAME"]}]");

                    @out.Append(" ASC");

                }
            }

            if (!firstTime)
            {
                @out.Append(")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
                @out.Append(") ON [PRIMARY]\"");
            }
            else
            {
                @out.Append(")\"");
            }

            return @out.ToString();
        }

        private string GetIdentitySeedAndIncrementValues(string tableName, string fieldName)
        {
            using (var db = new ADODatabaseContext(_connectionString))
            {
                var queryString = "SELECT * " +
                                 "FROM [" + _databaseName + "].sys.identity_columns AS a INNER JOIN [" + _databaseName + "].sys.objects AS b ON a.object_id=b.object_id " +
                                 "WHERE LOWER(b.name)='" + tableName.ToLower().Trim() + "' AND LOWER(a.name)='" +
                                 fieldName.ToLower().Trim() + "' AND type='U'";

                using (var columnReader = db.ReadQuery(queryString))
                {
                    while (columnReader.Read())
                    {
                        return $" IDENTITY({columnReader["SEED_VALUE"]},{columnReader["INCREMENT_VALUE"]})";
                    }
                }
            }

            return "";
        }
    }
}
