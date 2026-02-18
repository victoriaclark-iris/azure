using Microsoft.Azure.Cosmos;
using BlazorApp.Models;
using System.Net;

namespace BlazorApp.Services
{
    public class CosmosDbService
    {
        private readonly Container _container;

        public CosmosDbService(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        // CREATE - Add a new item
        public async Task<TodoItem> CreateItemAsync(TodoItem item)
        {
            try
            {
                var response = await _container.CreateItemAsync(item, new PartitionKey(item.PartitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"Item with id {item.Id} already exists");
            }
        }

        // READ - Get a single item by Id and Partition Key
        public async Task<TodoItem?> GetItemAsync(string id, string partitionKey)
        {
            try
            {
                var response = await _container.ReadItemAsync<TodoItem>(id, new PartitionKey(partitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        // READ - Get all items (with optional filter)
        public async Task<List<TodoItem>> GetItemsAsync(string? queryString = null)
        {
            var query = string.IsNullOrEmpty(queryString) 
                ? "SELECT * FROM c" 
                : queryString;
            
            var queryDefinition = new QueryDefinition(query);
            var iterator = _container.GetItemQueryIterator<TodoItem>(queryDefinition);
            
            var results = new List<TodoItem>();
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }
            
            return results;
        }

        // UPDATE - Update an existing item
        public async Task<TodoItem> UpdateItemAsync(TodoItem item)
        {
            try
            {
                var response = await _container.UpsertItemAsync(item, new PartitionKey(item.PartitionKey));
                return response.Resource;
            }
            catch (CosmosException ex)
            {
                throw new InvalidOperationException($"Failed to update item: {ex.Message}");
            }
        }

        // DELETE - Delete an item by Id and Partition Key
        public async Task DeleteItemAsync(string id, string partitionKey)
        {
            try
            {
                await _container.DeleteItemAsync<TodoItem>(id, new PartitionKey(partitionKey));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Item with id {id} not found");
            }
        }
    }
}