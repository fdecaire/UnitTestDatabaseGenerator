using System.Text;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class ConstraintMappings
    {
        private string _connectionString;
        private string _databaseName;

        public ConstraintMappings(string connectionString, string databaseName)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;

            _connectionString = _connectionString.Replace("master", databaseName);
        }

        public string ConstraintMappingQueryString
        {
            get
            {
                var query = @"
                    SELECT PKTABLE_QUALIFIER = CONVERT(SYSNAME,DB_NAME()),
			                     PKTABLE_OWNER = CONVERT(SYSNAME,SCHEMA_NAME(O1.SCHEMA_ID)),
			                     PKTABLE_NAME = CONVERT(SYSNAME,O1.NAME),
			                     PKCOLUMN_NAME = CONVERT(SYSNAME,C1.NAME),
			                     FKTABLE_QUALIFIER = CONVERT(SYSNAME,DB_NAME()),
			                     FKTABLE_OWNER = CONVERT(SYSNAME,SCHEMA_NAME(O2.SCHEMA_ID)),
			                     FKTABLE_NAME = CONVERT(SYSNAME,O2.NAME),
			                     FKCOLUMN_NAME = CONVERT(SYSNAME,C2.NAME),
			                     -- Force the column to be non-nullable (see SQL BU 325751)
			                     --KEY_SEQ             = isnull(convert(smallint,k.constraint_column_id), sysconv(smallint,0)),
			                     UPDATE_RULE = CONVERT(SMALLINT,CASE OBJECTPROPERTY(F.OBJECT_ID,'CnstIsUpdateCascade') 
																				                    WHEN 1 THEN 0
																				                    ELSE 1
																			                    END),
			                     DELETE_RULE = CONVERT(SMALLINT,CASE OBJECTPROPERTY(F.OBJECT_ID,'CnstIsDeleteCascade') 
																				                    WHEN 1 THEN 0
																				                    ELSE 1
																			                    END),
			                     FK_NAME = CONVERT(SYSNAME,OBJECT_NAME(F.OBJECT_ID)),
			                     PK_NAME = CONVERT(SYSNAME,I.NAME),
			                     DEFERRABILITY = CONVERT(SMALLINT,7)   -- SQL_NOT_DEFERRABLE
                    FROM   SYS.ALL_OBJECTS O1,
			                     SYS.ALL_OBJECTS O2,
			                     SYS.ALL_COLUMNS C1,
			                     SYS.ALL_COLUMNS C2,
			                     SYS.FOREIGN_KEYS F
			                     INNER JOIN SYS.FOREIGN_KEY_COLUMNS K
				                     ON (K.CONSTRAINT_OBJECT_ID = F.OBJECT_ID)
			                     INNER JOIN SYS.INDEXES I
				                     ON (F.REFERENCED_OBJECT_ID = I.OBJECT_ID
						                     AND F.KEY_INDEX_ID = I.INDEX_ID)
                    WHERE  O1.OBJECT_ID = F.REFERENCED_OBJECT_ID
			                     AND O2.OBJECT_ID = F.PARENT_OBJECT_ID
			                     AND C1.OBJECT_ID = F.REFERENCED_OBJECT_ID
			                     AND C2.OBJECT_ID = F.PARENT_OBJECT_ID
			                     AND C1.COLUMN_ID = K.REFERENCED_COLUMN_ID
			                     AND C2.COLUMN_ID = K.PARENT_COLUMN_ID";
                return query;
            }
        }

        public string EmitCode(string constraintMappingString)
        {
            var result = new StringBuilder();

            result.AppendLine("using System.Collections.Generic;");
            result.AppendLine("using UnitTestHelperLibrary;");
            result.AppendLine("");
            result.AppendLine("namespace ApplicationUnderTest." + _databaseName + ".Constraints");
            result.AppendLine("{");
            result.AppendLine("\tpublic class " + _databaseName + "Constraints");
            result.AppendLine("\t{");
            result.AppendLine("\t\tpublic static List<ConstraintDefinition> ConstraintList = new List<ConstraintDefinition> {");

            result.AppendLine(constraintMappingString);

            result.AppendLine("\t\t};");
            result.AppendLine("\t}");
            result.AppendLine("}");
            return result.ToString();
        }
    }
}
