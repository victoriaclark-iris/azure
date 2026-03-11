using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorApp.Features.Messages;

public partial class Messages
{
    [Inject] private MessageService MessageService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool isNameSet = false;
    private string userName = string.Empty;

    private List<Message> messages = new();
    private List<string> activeUsers = new();
    private string newMessageContent = string.Empty;
    private bool isLoadingMessages = false;
    private string? errorMessage = null;

    protected override async Task OnInitializedAsync()
    {
        // Start with empty state
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender && messages.Any() && isNameSet)
        {
            await JSRuntime.InvokeVoidAsync("scrollToBottom", "messageContainer");
        }
    }

    private async Task SetUserName()
    {
        if (!string.IsNullOrWhiteSpace(userName))
        {
            userName = userName.Trim();
            isNameSet = true;
            await LoadMessages();
            StateHasChanged();
        }
    }

    private async Task HandleNameKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SetUserName();
        }
    }

    private async Task ResetName()
    {
        isNameSet = false;
        userName = string.Empty;
        messages.Clear();
        activeUsers.Clear();
        StateHasChanged();
    }

    private async Task LoadMessages()
    {
        try
        {
            isLoadingMessages = true;
            errorMessage = null;

            messages = await MessageService.GetAllMessagesAsync();
            activeUsers = await MessageService.GetActiveUsersAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading messages: {ex.Message}";
        }
        finally
        {
            isLoadingMessages = false;
            StateHasChanged();
        }
    }

    private async Task RefreshMessages()
    {
        await LoadMessages();
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(newMessageContent))
            return;

        try
        {
            isLoadingMessages = true;

            var message = new Message
            {
                SenderId = userName,
                SenderName = userName,
                Content = newMessageContent.Trim()
            };

            await MessageService.SendMessageAsync(message);
            messages.Add(message);
            newMessageContent = string.Empty;

            if (!activeUsers.Contains(userName))
            {
                activeUsers.Add(userName);
                activeUsers = activeUsers.OrderBy(u => u).ToList();
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error sending message: {ex.Message}";
        }
        finally
        {
            isLoadingMessages = false;
            StateHasChanged();
        }
    }

    private async Task HandleKeyPress(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessage();
        }
    }
}
