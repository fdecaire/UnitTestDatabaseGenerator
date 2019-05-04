using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class GenerateMappings
    {
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
        public string RootDirectory { get; set; }
        public bool GenerateViewMappings { get; set; }
        public bool GenerateIntegrityConstraintMappings { get; set; }
        public bool GenerateStoredProcedureMappings { get; set; }
        public bool GenerateFunctionMappings { get; set; }
        public int TotalCompleted { get; private set; }

        private readonly List<string> _deletedFiles = new List<string>();
        private CancellationToken _token;
        public void CreateMappings(object action)
        {
            _token = (CancellationToken)action;

            TotalCompleted = 0;

            Setup();

            if (_token.IsCancellationRequested)
            {
                return;
            }

            CreateTableGeneratorMappings();

            if (GenerateStoredProcedureMappings)
            {
                CreateStoredProcedureMappings();
            }

            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (GenerateViewMappings)
            {
                CreateViewMappings();
            }

            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (GenerateIntegrityConstraintMappings)
            {
                CreateConstraintMappings();
            }

            if (_token.IsCancellationRequested)
            {
                return;
            }

            if (GenerateFunctionMappings)
            {
                CreateFunctionMappings();
            }

            DeleteUnusedFiles();
        }

        public int Count
        {
            get
            {
                var total = CountTables();
                total += CountStoredProcedures();
                total += CountViews();
                total += CountConstraints();
                total += CountFunctions();

                return total;
            }
        }

        private int CountFunctions()
        {
            return 0;
        }

        private int CountConstraints()
        {
            return 0;
        }

        private int CountViews()
        {
            return 0;
        }

        private int CountStoredProcedures()
        {
            return 0;
        }

        private int CountTables()
        {
            var query = "SELECT COUNT(*) AS Total FROM " + DatabaseName + ".INFORMATION_SCHEMA.tables";
            using (var db = new ADODatabaseContext(ConnectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    return reader["Total"].ToInt();
                }
            }

            return 0;
        }

        private void CreateFunctionMappings()
        {
            Directory.CreateDirectory(Path.Combine(RootDirectory, DatabaseName, "Functions"));
            var query = $"SELECT ROUTINE_NAME,routine_schema FROM {DatabaseName}.information_schema.routines WHERE routine_type = 'FUNCTION'";
             
            using (var db = new ADODatabaseContext(ConnectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    if (_token.IsCancellationRequested)
                    {
                        return;
                    }

                    // generate any new stored procedure mappings
                    CreateFunction(reader["ROUTINE_NAME"].ToString(), reader["ROUTINE_SCHEMA"].ToString());
                }
            }
        }

        public void CreateFunction(string functionName, string schemaName)
        {
            using (var file = new StreamWriter(Path.Combine(RootDirectory, DatabaseName, "Functions", functionName + $"_{schemaName}.cs")))
            {
                var functionMappings = new FunctionMappings(ConnectionString, DatabaseName, functionName, schemaName);

                file.Write(functionMappings.EmitCode());
            }

            TotalCompleted++;
        }

        private void Setup()
        {
            // scan for all the files that currently exist and insert them into DeleteFiles
            DirSearch(Path.Combine(RootDirectory, DatabaseName));
        }

        private void DeleteUnusedFiles()
        {
            foreach (var item in _deletedFiles)
            {
                File.Delete(item);
            }

            _deletedFiles.Clear();
        }

        private void DirSearch(string sDir)
        {
            if (!Directory.Exists(sDir)) return;

            foreach (var d in Directory.GetDirectories(sDir))
            {
                foreach (var f in Directory.GetFiles(d))
                {
                    if (f.EndsWith(".cs"))
                    {
                        _deletedFiles.Add(f);
                    }
                }
                DirSearch(d);
            }
        }

        private void CreateTableGeneratorMappings()
        {
            Directory.CreateDirectory(Path.Combine(RootDirectory, DatabaseName, "TableGeneratorCode"));
            var tableDefinitionString = new StringBuilder();
            var tableGeneratorMappings = new TableGeneratorMappings(ConnectionString, DatabaseName, _token);

            var query = "SELECT * FROM " + DatabaseName + ".INFORMATION_SCHEMA.tables ORDER BY TABLE_SCHEMA,TABLE_NAME";
            using (var db = new ADODatabaseContext(ConnectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    if (_token.IsCancellationRequested)
                    {
                        return;
                    }

                    tableDefinitionString.Append("\t\t\tnew TableDefinition {");
                    tableDefinitionString.Append(tableGeneratorMappings.EmitTableGenerateCode(reader["TABLE_NAME"].ToString(), reader["TABLE_SCHEMA"].ToString()));
                    tableDefinitionString.AppendLine("},");

                    TotalCompleted++;
                }
            }


            var result = tableGeneratorMappings.EmitCode(tableDefinitionString.ToString());

            using (var file = new StreamWriter(Path.Combine(RootDirectory, DatabaseName, "TableGeneratorCode", DatabaseName + "TableGeneratorCode.cs")))
            {
                file.Write(result);
            }

            UpdateProjectFileList("TableGeneratorCode", DatabaseName + "TableGeneratorCode");
        }

        private void UpdateProjectFileList(string tableSpView, string name)
        {
            // delete any existing table mappings first (in case a table was deleted)
            var foundIndex = _deletedFiles.IndexOf(Path.Combine(RootDirectory, DatabaseName, tableSpView, name + ".cs"));
            if (foundIndex > -1)
            {
                _deletedFiles.RemoveAt(foundIndex);
            }
        }

        public void CreateStoredProcedureMappings()
        {
            Directory.CreateDirectory(Path.Combine(RootDirectory, DatabaseName, "StoredProcedures"));

            var noStoredProceduresCreated = true;
            var query = $"SELECT ROUTINE_NAME, ROUTINE_SCHEMA FROM {DatabaseName}.information_schema.routines WHERE routine_type = 'PROCEDURE'";
            using (var db = new ADODatabaseContext(ConnectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    if (_token.IsCancellationRequested)
                    {
                        return;
                    }

                    // generate any new stored procedure mappings
                    CreateStoredProcedure(reader["ROUTINE_NAME"].ToString(),reader["ROUTINE_SCHEMA"].ToString());
                    noStoredProceduresCreated = false;
                }
            }

            if (noStoredProceduresCreated)
            {
                UpdateProjectFileList("StoredProcedures", "");
            }
        }

        public void CreateStoredProcedure(string storedProcedureName, string schemaName)
        {
            using (var file = new StreamWriter(Path.Combine(RootDirectory, DatabaseName, "StoredProcedures", storedProcedureName + $"_{schemaName}.cs")))
            {
                var storedProcedureMappings = new StoredProcedureMappings(ConnectionString, DatabaseName, storedProcedureName, schemaName);

                file.Write(storedProcedureMappings.EmitCode());
            }

            UpdateProjectFileList("StoredProcedures", storedProcedureName);
            TotalCompleted++;
        }

        public void CreateViewMappings()
        {
            Directory.CreateDirectory(Path.Combine(RootDirectory, DatabaseName, "Views"));

            var noViewsCreated = true;
            var query = "SELECT TABLE_NAME FROM " + DatabaseName + ".information_schema.views";
            using (var db = new ADODatabaseContext(ConnectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    if (_token.IsCancellationRequested)
                    {
                        return;
                    }

                    // generate any new view mappings
                    CreateView(reader["TABLE_NAME"].ToString());
                    noViewsCreated = false;
                }
            }

            if (noViewsCreated)
            {
                UpdateProjectFileList("Views", "");
            }
        }

        public void CreateView(string viewName)
        {
            using (var file = new StreamWriter(Path.Combine(RootDirectory, DatabaseName, "Views", viewName + ".cs")))
            {
                var viewMappings = new ViewMappings(ConnectionString, DatabaseName, viewName);

                file.Write(viewMappings.EmitCode());
            }

            UpdateProjectFileList("Views", viewName);
            TotalCompleted++;
        }

        public void CreateConstraintMappings()
        {
            Directory.CreateDirectory(Path.Combine(RootDirectory, DatabaseName, "Constraints"));

            var constraintMappings = new ConstraintMappings(ConnectionString, DatabaseName);
            var result = constraintMappings.EmitCode();

            using (var file = new StreamWriter(Path.Combine(RootDirectory, DatabaseName, "Constraints", DatabaseName + "Constraints.cs")))
            {
                file.Write(result);
            }

            UpdateProjectFileList("Constraints", DatabaseName + "Constraints");
        }
    }
}
