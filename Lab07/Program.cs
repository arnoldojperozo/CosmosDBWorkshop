using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using System.Collections.Generic;
using System.Linq;

public class Program
{
    private static readonly string _endpointUri = "";
    private static readonly string _primaryKey = "";

    private static readonly string _databaseId = "ImportDatabase";
    private static readonly string _containerId = "FoodCollection";

    public static async Task Main(string[] args)
    {
        using (CosmosClient client = new CosmosClient(_endpointUri, _primaryKey))
        {
            Database database = client.GetDatabase(_databaseId);
            Container container = database.GetContainer(_containerId);

            //await BulkUpload(container);
            await BulkDelete(container);
        }
    }

    private static async Task BulkUpload(Container container)
    {
        List<Food> foods = new Bogus.Faker<Food>()
        .RuleFor(p => p.Id, f => (-1 - f.IndexGlobal).ToString())
        .RuleFor(p => p.Description, f => f.Commerce.ProductName())
        .RuleFor(p => p.ManufacturerName, f => f.Company.CompanyName())
        .RuleFor(p => p.FoodGroup, f => "Energy Bars")
        .Generate(1000);

        int pointer = 0;

        while (pointer < foods.Count)
        {
            StoredProcedureExecuteResponse<int> result = await container.Scripts.ExecuteStoredProcedureAsync<int>("bulkUpload", new PartitionKey("Energy Bars"), new dynamic[] {foods.Skip(pointer)});
            pointer += result.Resource;
            await Console.Out.WriteLineAsync($"{pointer} Total Items\t{result.Resource} Items Uploaded in this Iteration");
        }
    }

    private static async Task BulkDelete(Container container)
    {
        bool resume = true;
        do
        {
            string query = "SELECT * FROM foods f WHERE f.foodGroup = 'Energy Bars'";
            StoredProcedureExecuteResponse<DeleteStatus> result = await container.Scripts.ExecuteStoredProcedureAsync<DeleteStatus>("bulkDelete", new PartitionKey("Energy Bars"),  new dynamic[] {query});

            await Console.Out.WriteLineAsync($"Batch Delete Completed.\tDeleted: {result.Resource.Deleted}\tContinue: {result.Resource.Continuation}");
            resume = result.Resource.Continuation;
        }
        while (resume);
    }
}
