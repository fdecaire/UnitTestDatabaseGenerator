﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnitTestHelperLibrary;

namespace UnitTestDatabaseGenerator
{
    public class MasterProcessor
    {
        private static MasterProcessor _instance;
        public CancellationTokenSource cts;
        public static MasterProcessor Instance => _instance ?? (_instance = new MasterProcessor());
        public int PercentComplete {
            get
            {
                int totalComplete = 0;
                foreach (var mapping in _mappingList)
                {
                    totalComplete += mapping.TotalCompleted; //TODO: probably need to lock mapping first
                }

                var total = (int)((double)totalComplete / (double) TotalObjects * 100);

                return total;
            }
        }

        public int TotalObjects { get; set; }
        private readonly List<GenerateMappings> _mappingList = new List<GenerateMappings>();

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

                    foreach (var database in databaseList)
                    {
                        var mapping = new GenerateMappings
                        {
                            DatabaseName = database,
                            ConnectionString = connectionString,
                            GenerateIntegrityConstraintMappings = constraints,
                            GenerateStoredProcedureMappings = storedProcedures,
                            GenerateViewMappings = views,
                            GenerateFunctionMappings = functions,
                            RootDirectory = destinationDirectory
                        };

                        TotalObjects += mapping.Count;
                        _mappingList.Add(mapping);
                    }

                    Parallel.ForEach(_mappingList, po, (mapping) =>
                    {
                        mapping.CreateMappings(action);
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
