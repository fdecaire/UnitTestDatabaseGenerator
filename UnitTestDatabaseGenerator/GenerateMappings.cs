using System.Collections.Generic;
using System.IO;
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

        private readonly List<string> _deletedFiles = new List<string>();

        public void CreateMappings()
        {
            Setup();

            CreateTableGeneratorMappings();

            if (GenerateStoredProcedureMappings)
            {
                CreateStoredProcedureMappings();
            }

            if (GenerateViewMappings)
            {
                CreateViewMappings();
            }

            if (GenerateIntegrityConstraintMappings)
            {
                CreateConstraintMappings();
            }

            DeleteUnusedFiles();
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

            var tableGeneratorMappings = new TableGeneratorMappings(ConnectionString, DatabaseName);
            var result = tableGeneratorMappings.EmitCode();

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
            var query = "SELECT ROUTINE_NAME FROM " + DatabaseName + ".information_schema.routines WHERE routine_type = 'PROCEDURE'";
            using (var db = new ADODatabaseContext(ConnectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    // generate any new stored procedure mappings
                    CreateStoredProcedure(reader["ROUTINE_NAME"].ToString());
                    noStoredProceduresCreated = false;
                }
            }

            if (noStoredProceduresCreated)
            {
                UpdateProjectFileList("StoredProcedures", "");
            }
        }

        public void CreateStoredProcedure(string storedProcedureName)
        {
            using (var file = new StreamWriter(Path.Combine(RootDirectory, DatabaseName, "StoredProcedures", storedProcedureName + ".cs")))
            {
                var storedProcedureMappings = new StoredProcedureMappings(ConnectionString, DatabaseName, storedProcedureName);

                file.Write(storedProcedureMappings.EmitCode());
            }

            UpdateProjectFileList("StoredProcedures", storedProcedureName);
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
