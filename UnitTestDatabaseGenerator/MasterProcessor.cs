using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestDatabaseGenerator
{
    public class MasterProcessor
    {
        private static MasterProcessor _instance;
        public CancellationTokenSource cts;
        public static MasterProcessor Instance => _instance ?? (_instance = new MasterProcessor());
        public static int PercentComplete { get; set; }
        public bool Stopped { get; private set; }
        public void Start(List<string> databaseList, string connectionString, bool constraints, bool storedProcedures, bool views, bool functions, string destinationDirectory)
        {
            Stopped = false;
            cts = new CancellationTokenSource();

            ThreadPool.QueueUserWorkItem(new WaitCallback(action =>
            {
                var token = (CancellationToken)action;

                if (!token.IsCancellationRequested)
                {
                    var po = new ParallelOptions
                    {
                        CancellationToken = cts.Token,
                        MaxDegreeOfParallelism = System.Environment.ProcessorCount
                    };

                    Parallel.ForEach(databaseList, po, (database) =>
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
                        mappings.CreateMappings(action);
                    });
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
