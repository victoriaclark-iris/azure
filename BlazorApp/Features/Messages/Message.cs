using Newtonsoft.Json;

namespace BlazorApp.Features.Messages
{
    public class Message
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("senderId")]
        public string SenderId { get; set; } = string.Empty;

        [JsonProperty("senderName")]
        public string SenderName { get; set; } = string.Empty;

        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        [JsonProperty("sentDate")]
        public DateTime SentDate { get; set; } = DateTime.UtcNow;
    }
}