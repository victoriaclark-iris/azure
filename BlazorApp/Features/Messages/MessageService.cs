namespace BlazorApp.Features.Messages
{
    public class MessageService
    {
        private static readonly List<Message> _messages = new();
        private static readonly object _lock = new();

        // CREATE - Send a message to the public chat room
        public Task<Message> SendMessageAsync(Message message)
        {
            lock (_lock)
            {
                _messages.Add(message);
                return Task.FromResult(message);
            }
        }

        // READ - Get all messages in the public chat room
        public Task<List<Message>> GetAllMessagesAsync()
        {
            lock (_lock)
            {
                var allMessages = _messages
                    .OrderBy(m => m.SentDate)
                    .ToList();

                return Task.FromResult(allMessages);
            }
        }

        // DELETE - Delete a message (admin function)
        public Task DeleteMessageAsync(string id)
        {
            lock (_lock)
            {
                var message = _messages.FirstOrDefault(m => m.Id == id);
                if (message != null)
                {
                    _messages.Remove(message);
                    return Task.CompletedTask;
                }
                throw new InvalidOperationException($"Message with id {id} not found");
            }
        }

        // Helper method to get all unique users who have sent messages
        public Task<List<string>> GetActiveUsersAsync()
        {
            lock (_lock)
            {
                var users = _messages
                    .Where(m => !string.IsNullOrEmpty(m.SenderName))
                    .Select(m => m.SenderName)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList();

                return Task.FromResult(users);
            }
        }

        // Get message count
        public Task<int> GetMessageCountAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_messages.Count);
            }
        }

        // Clear all messages (for testing)
        public Task ClearAllMessagesAsync()
        {
            lock (_lock)
            {
                _messages.Clear();
                return Task.CompletedTask;
            }
        }
    }
}