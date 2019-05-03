using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace UnitTestDatabaseGenerator
{
    public class MasterProcessor
    {
        private static MasterProcessor _instance;
        public CancellationTokenSource cts = new CancellationTokenSource();
        public static MasterProcessor Instance => _instance ?? (_instance = new MasterProcessor());
        public static int PercentComplete { get; set; }
        public bool Stopped { get; private set; }
        public void Start(List<string> databaseList, string connectionString, bool constraints, bool storedProcedures, bool views, bool functions, string destinationDirectory)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(action =>
            {
                var token = (CancellationToken)action;

                if (!token.IsCancellationRequested)
                {
                    //TODO: change this to a parallel.foreach()
                    foreach (var database in databaseList)
                    {
                        var mappings = new GenerateMappings
                        {
                            DatabaseName = database,
                            ConnectionString = connectionString,
                            GenerateIntegrityConstraintMappings = constraints,
                            GenerateStoredProcedureMappings = storedProcedures,
                            GenerateViewMappings = views,
                            GenerateFunctionMappings = functions,
                            RootDirectory = destinationDirectory
                        };
                        mappings.CreateMappings(action); //TODO: pass cancellation token
                    }
                }
                Stopped = true;
            }), cts.Token);
        }
        public void Stop()
        {
            cts.Cancel();
        }
    }
}
