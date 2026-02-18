using Newtonsoft.Json;

namespace BlazorApp.Models
{
    public class TodoItem
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; } = "default";

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("isCompleted")]
        public bool IsCompleted { get; set; }

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}