using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Shared;

namespace ChangeFeedConsole
{
    class Program
    {
        private static readonly string _endpointUrl = "<your-endpoint-url>";
        private static readonly string _primaryKey = "<your-primary-key>";
        private static readonly string _databaseId = "StoreDatabase";
        private static readonly string _containerId = "CartContainer";
        private static readonly string _destinationContainerId = "CartContainerByState";
        private static CosmosClient _client = new CosmosClient(_endpointUrl, _primaryKey);

        static async Task Main(string[] args)
        {

            Database database = _client.GetDatabase(_databaseId);
            Container container = database.GetContainer(_containerId);
            Container destinationContainer = database.GetContainer(_destinationContainerId);

            ContainerProperties leaseContainerProperties = new ContainerProperties("CartContainerByState", "/id");
            Container leaseContainer = await  database.CreateContainerIfNotExistsAsync(leaseContainerProperties, throughput: 400);

            var builder = container.GetChangeFeedProcessorBuilder("migrationProcessor",
               (IReadOnlyCollection<CartAction> input, CancellationToken cancellationToken) =>
               {
                  Console.WriteLine(input.Count + " Changes Received");
                  //todo: Add processor code here
                  var tasks = new List<Task>();

                foreach (var doc in input)
                {
                    tasks.Add(destinationContainer.CreateItemAsync(doc, new PartitionKey(doc.BuyerState)));
                }

                return Task.WhenAll(tasks);
               });

            var processor = builder
                            .WithInstanceName("changeFeedConsole")
                            .WithLeaseContainer(leaseContainer)
                            .Build();

            await processor.StartAsync();
            Console.WriteLine("Started Change Feed Processor");
            Console.WriteLine("Press any key to stop the processor...");

            Console.ReadKey();

            Console.WriteLine("Stopping Change Feed Processor");
            await processor.StopAsync();
        }
    }
}