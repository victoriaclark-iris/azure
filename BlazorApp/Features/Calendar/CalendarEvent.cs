using Newtonsoft.Json;

namespace BlazorApp.Features.Calendar
{
    public class CalendarEvent
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; } = "calendar-events";

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("eventDate")]
        public DateTime EventDate { get; set; } = DateTime.Today;

        [JsonProperty("eventType")]
        public EventType EventType { get; set; } = EventType.General;

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [JsonProperty("color")]
        public string Color { get; set; } = "#ff0040";
    }

    public enum EventType
    {
        General,
        Important,
        Social,
        Study,
        Work,
        Personal
    }
}