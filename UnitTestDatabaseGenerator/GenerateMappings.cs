using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class GenerateMappings
    {
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
        public string RootDirectory = @"c:\temp"; //TODO: need to implement a user interface to be able to change this
        public bool GenerateViewMappings { get; set; }
        public bool GenerateIntegrityConstraintMappings { get; set; }
        public bool GenerateStoredProcedureMappings { get; set; }

        private readonly List<string> _deletedFiles = new List<string>();
        private readonly List<string> _addedFiles = new List<string>();
        private readonly List<string> _folderList = new List<string>();

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
            _folderList.Add(DatabaseName + "\\Constraints");
            _folderList.Add(DatabaseName + "\\StoredProcedures");
            _folderList.Add(DatabaseName + "\\Tables");
            _folderList.Add(DatabaseName + "\\Views");
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
            if (Directory.Exists(sDir))
            {
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        if (f.EndsWith(".cs"))
                        {
                            _deletedFiles.Add(f);
                        }
                    }
                    DirSearch(d);
                }
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
            //TODO: refactor this
            var foundIndex = _deletedFiles.IndexOf(Path.Combine(RootDirectory, DatabaseName, tableSpView, name + ".cs"));
            if (foundIndex > -1)
            {
                _deletedFiles.RemoveAt(foundIndex);
            }

            if (name != "")
            {
                // added file
                _addedFiles.Add(DatabaseName + "\\" + tableSpView + "\\" + name + ".cs");
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

        private void AddNewNodes(XmlDocument doc, XmlNamespaceManager nsmgr, XmlNodeList itemGroupNodes)
        {
            var containsFolderItemGroup = false;
            var containsCompileItemGroup = false;

            foreach (XmlNode itemGroupNode in itemGroupNodes)
            {
                var childNodes = itemGroupNode.ChildNodes;

                foreach (XmlNode childNode in childNodes)
                {
                    if (childNode.Name == "Folder")
                    {
                        containsFolderItemGroup = true;

                        var includeAttribute = childNode.Attributes["Include"];

                        if (includeAttribute != null)
                        {
                            foreach (var item in _folderList)
                            {
                                itemGroupNode.AppendChild(CreateChildNode(doc, "Folder", item));
                            }
                            break;
                        }
                    }
                    else if (childNode.Name == "Compile")
                    {
                        containsCompileItemGroup = true;

                        var includeAttribute = childNode.Attributes["Include"];

                        foreach (var item in _addedFiles)
                        {
                            itemGroupNode.AppendChild(CreateChildNode(doc, "Compile", item));
                        }
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // need to handle situation where the Compile or folder itemgroups do not exist
            if (!containsFolderItemGroup)
            {
                var projectNode = doc.SelectNodes("//a:Project", nsmgr);
                var itemGroupNode = doc.CreateNode(XmlNodeType.Element, "ItemGroup", "http://schemas.microsoft.com/developer/msbuild/2003");
                projectNode[0].AppendChild(itemGroupNode);

                foreach (var item in _folderList)
                {
                    itemGroupNode.AppendChild(CreateChildNode(doc, "Folder", item));
                }
            }

            if (!containsCompileItemGroup)
            {
                var projectNode = doc.SelectNodes("//a:Project", nsmgr);
                var itemGroupNode = doc.CreateNode(XmlNodeType.Element, "ItemGroup", "http://schemas.microsoft.com/developer/msbuild/2003");
                projectNode[0].AppendChild(itemGroupNode);

                foreach (var item in _addedFiles)
                {
                    itemGroupNode.AppendChild(CreateChildNode(doc, "Compile", item));
                }
            }
        }

        private void DeleteEmptyNodes(XmlNodeList itemGroupNodes)
        {
            // remove any empty ItemGroup nodes
            foreach (XmlNode itemGroupNode in itemGroupNodes)
            {
                if (itemGroupNode.ChildNodes.Count == 0)
                {
                    itemGroupNode.ParentNode.RemoveChild(itemGroupNode);
                }
            }
        }

        private void DeleteNodeContainingChildName(XmlNodeList itemGroupNodes, string childGroupName)
        {
            var toBeRemoved = new List<XmlNode>();

            foreach (XmlNode itemGroupNode in itemGroupNodes)
            {
                var childNodes = itemGroupNode.ChildNodes;

                foreach (XmlNode childNode in childNodes)
                {
                    if (childNode.Name == childGroupName)
                    {
                        var includeAttribute = childNode.Attributes["Include"];

                        if (includeAttribute != null && includeAttribute.Value != null && includeAttribute.Value.Contains(DatabaseName + "\\"))
                        {
                            toBeRemoved.Add(childNode);
                        }
                    }
                }

                foreach (var item in toBeRemoved)
                {
                    itemGroupNode.RemoveChild(item);
                }

                toBeRemoved.Clear();
            }
        }

        private XmlNode CreateChildNode(XmlDocument doc, string elementName, string attributeValue)
        {
            var folderNode = doc.CreateNode(XmlNodeType.Element, elementName, "http://schemas.microsoft.com/developer/msbuild/2003");
            var xKey = doc.CreateAttribute("Include");
            xKey.Value = attributeValue;
            folderNode.Attributes.Append(xKey);

            return folderNode;
        }
    }
}
