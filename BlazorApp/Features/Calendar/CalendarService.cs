using Microsoft.Azure.Cosmos;
using System.Net;

namespace BlazorApp.Features.Calendar
{
    public class CalendarService
    {
        private readonly Container _container;

        public CalendarService(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        // CREATE - Add a new calendar event
        public async Task<CalendarEvent> CreateEventAsync(CalendarEvent calendarEvent)
        {
            try
            {
                var response = await _container.CreateItemAsync(calendarEvent, new PartitionKey(calendarEvent.PartitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException($"Event with id {calendarEvent.Id} already exists");
            }
        }

        // READ - Get a single event by Id
        public async Task<CalendarEvent?> GetEventAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<CalendarEvent>(id, new PartitionKey("calendar-events"));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        // READ - Get all events for a specific month
        public async Task<List<CalendarEvent>> GetEventsForMonthAsync(int year, int month)
        {
            var monthPrefix = $"{year:D4}-{month:D2}";
            var query = "SELECT * FROM c WHERE c.partitionKey = 'calendar-events' AND STARTSWITH(c.eventDate, @monthPrefix) ORDER BY c.eventDate";

            var queryDefinition = new QueryDefinition(query)
                .WithParameter("@monthPrefix", monthPrefix);

            var results = new List<CalendarEvent>();
            using var iterator = _container.GetItemQueryIterator<CalendarEvent>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        // READ - Get all events for a specific date
        public async Task<List<CalendarEvent>> GetEventsForDateAsync(DateTime date)
        {
            var dateKey = date.ToString("yyyy-MM-dd");
            var query = "SELECT * FROM c WHERE c.partitionKey = 'calendar-events' AND SUBSTRING(c.eventDate, 0, 10) = @dateKey ORDER BY c.eventDate";

            var queryDefinition = new QueryDefinition(query)
                .WithParameter("@dateKey", dateKey);

            var results = new List<CalendarEvent>();
            using var iterator = _container.GetItemQueryIterator<CalendarEvent>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.ToList());
            }

            return results;
        }

        // UPDATE - Update an existing event
        public async Task<CalendarEvent> UpdateEventAsync(CalendarEvent calendarEvent)
        {
            try
            {
                var response = await _container.ReplaceItemAsync(calendarEvent, calendarEvent.Id, new PartitionKey(calendarEvent.PartitionKey));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Event with id {calendarEvent.Id} not found");
            }
        }

        // DELETE - Remove an event
        public async Task DeleteEventAsync(string id)
        {
            try
            {
                await _container.DeleteItemAsync<CalendarEvent>(id, new PartitionKey("calendar-events"));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Event with id {id} not found");
            }
        }
    }
}