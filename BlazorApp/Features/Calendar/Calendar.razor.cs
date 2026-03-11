using Microsoft.AspNetCore.Components;

namespace BlazorApp.Features.Calendar;

public partial class Calendar
{
    [Inject] private CalendarService CalendarService { get; set; } = default!;

    private DateTime CurrentDate = DateTime.Today;
    private DateTime? SelectedDate;
    private List<CalendarEvent> MonthlyEvents = new();
    private List<CalendarEvent> SelectedDateEvents = new();
    private bool ShowEventForm = false;
    private bool IsEditMode = false;
    private CalendarEvent NewEvent = new();
    private string? ErrorMessage = null;
    private bool IsLoading = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadMonthlyEvents();
    }

    private async Task LoadMonthlyEvents()
    {
        ClearError();
        IsLoading = true;
        StateHasChanged();

        try
        {
            var newEvents = await CalendarService.GetEventsForMonthAsync(CurrentDate.Year, CurrentDate.Month);
            MonthlyEvents = newEvents;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load calendar events: {ex.Message}";
            Console.WriteLine($"Error loading events: {ex.Message}");
            MonthlyEvents = new List<CalendarEvent>();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task PreviousMonth()
    {
        CurrentDate = CurrentDate.AddMonths(-1);
        await LoadMonthlyEvents();
        SelectedDate = null;
        SelectedDateEvents.Clear();
    }

    private async Task NextMonth()
    {
        CurrentDate = CurrentDate.AddMonths(1);
        await LoadMonthlyEvents();
        SelectedDate = null;
        SelectedDateEvents.Clear();
    }

    private async Task SelectDate(DateTime date)
    {
        SelectedDate = date;
        ClearError();

        try
        {
            SelectedDateEvents = await CalendarService.GetEventsForDateAsync(date);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load events for {date:MM/dd/yyyy}: {ex.Message}";
            Console.WriteLine($"Error loading events for date: {ex.Message}");
            StateHasChanged();
        }
    }

    private void ShowAddEventForm()
    {
        IsEditMode = false;
        NewEvent = new CalendarEvent
        {
            EventDate = SelectedDate ?? DateTime.Today
        };
        ShowEventForm = true;
    }

    private void EditEvent(CalendarEvent calendarEvent)
    {
        IsEditMode = true;
        NewEvent = new CalendarEvent
        {
            Id = calendarEvent.Id,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            EventDate = calendarEvent.EventDate,
            EventType = calendarEvent.EventType,
            PartitionKey = calendarEvent.PartitionKey,
            CreatedDate = calendarEvent.CreatedDate
        };
        ShowEventForm = true;
    }

    private void HideAddEventForm()
    {
        ShowEventForm = false;
        IsEditMode = false;
        NewEvent = new();
    }

    private async Task SaveEvent()
    {
        if (string.IsNullOrWhiteSpace(NewEvent.Title))
        {
            ErrorMessage = "Event title is required";
            StateHasChanged();
            return;
        }

        ClearError();
        IsLoading = true;
        StateHasChanged();

        try
        {
            NewEvent.EventDate = NewEvent.EventDate.Date;

            if (IsEditMode)
            {
                await CalendarService.UpdateEventAsync(NewEvent);
            }
            else
            {
                await CalendarService.CreateEventAsync(NewEvent);
            }

            await LoadMonthlyEvents();
            if (SelectedDate.HasValue)
            {
                await SelectDate(SelectedDate.Value);
            }

            HideAddEventForm();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save event: {ex.Message}";
            Console.WriteLine($"Error saving event: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task DeleteEvent(string eventId)
    {
        ClearError();
        IsLoading = true;
        StateHasChanged();

        try
        {
            await CalendarService.DeleteEventAsync(eventId);
            await LoadMonthlyEvents();
            if (SelectedDate.HasValue)
            {
                await SelectDate(SelectedDate.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete event: {ex.Message}";
            Console.WriteLine($"Error deleting event: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private List<CalendarDay> GetCalendarDays()
    {
        var firstDayOfMonth = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
        var firstDisplayDay = firstDayOfMonth.AddDays(-(int)firstDayOfMonth.DayOfWeek);
        var lastDisplayDay = lastDayOfMonth.AddDays(6 - (int)lastDayOfMonth.DayOfWeek);

        var days = new List<CalendarDay>();
        var currentDay = firstDisplayDay;

        while (currentDay <= lastDisplayDay)
        {
            var eventsForDay = MonthlyEvents.Where(e => e.EventDate.Date == currentDay.Date).ToList();

            days.Add(new CalendarDay
            {
                Date = currentDay,
                IsCurrentMonth = currentDay.Month == CurrentDate.Month,
                IsToday = currentDay.Date == DateTime.Today,
                HasEvents = eventsForDay.Any(),
                EventTypes = eventsForDay.Select(e => e.EventType).Distinct().ToList()
            });
            currentDay = currentDay.AddDays(1);
        }

        return days;
    }

    private string GetEventTypeClass(EventType eventType)
    {
        return eventType.ToString().ToLower();
    }

    private void ClearError()
    {
        ErrorMessage = null;
    }

    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public bool HasEvents { get; set; }
        public List<EventType> EventTypes { get; set; } = new();
    }
}
