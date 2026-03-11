using Microsoft.AspNetCore.Components;

namespace BlazorApp.Features.Todo;

public partial class TodoList
{
    [Inject] private TodoService TodoService { get; set; } = default!;

    private List<TodoItem> todos = new();
    private TodoItem newTodo = new();
    private bool isLoading = false;
    private string? errorMessage = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadTodos();
    }

    private async Task LoadTodos()
    {
        try
        {
            isLoading = true;
            errorMessage = null;
            StateHasChanged();

            todos = await TodoService.GetItemsAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading todos: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task AddTodo()
    {
        if (string.IsNullOrWhiteSpace(newTodo.Title))
            return;

        try
        {
            isLoading = true;
            errorMessage = null;
            StateHasChanged();

            await TodoService.CreateItemAsync(newTodo);

            newTodo = new TodoItem();

            await LoadTodos();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error adding todo: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task ToggleComplete(TodoItem todo)
    {
        try
        {
            isLoading = true;
            errorMessage = null;

            todo.IsCompleted = !todo.IsCompleted;
            await TodoService.UpdateItemAsync(todo);

            StateHasChanged();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error updating todo: {ex.Message}";
            todo.IsCompleted = !todo.IsCompleted;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteTodo(TodoItem todo)
    {
        try
        {
            isLoading = true;
            errorMessage = null;

            await TodoService.DeleteItemAsync(todo.Id, todo.PartitionKey);
            todos.Remove(todo);

            StateHasChanged();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error deleting todo: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}
