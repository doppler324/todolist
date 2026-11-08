namespace TodoDock.Core;

public enum Priority { Low = 0, Normal = 1, High = 2 }

public sealed class Folder
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4F8CFF";
    public string Icon { get; set; } = "📁";
    public int SortOrder { get; set; }
    /// <summary>Задачи этой папки показываются в окне на рабочем столе.</summary>
    public bool ShowOnDesktop { get; set; }
}

public sealed class TaskItem
{
    public long Id { get; set; }
    public long? FolderId { get; set; }
    public string Title { get; set; } = "";
    public string Note { get; set; } = "";
    public Priority Priority { get; set; } = Priority.Normal;
    public bool IsDone { get; set; }
    public DateTime? DueAt { get; set; }
    public string? RepeatRule { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Дублировать уведомление по этой задаче в Telegram (если Telegram настроен).</summary>
    public bool SendTelegram { get; set; } = true;

    /// <summary>Дополнительные разовые напоминания (в дополнение к сроку и повтору).</summary>
    public List<DateTime> Reminders { get; set; } = new();

    public TaskItem Clone()
    {
        var c = (TaskItem)MemberwiseClone();
        c.Reminders = new List<DateTime>(Reminders);
        return c;
    }
}

public enum NotificationStatus { Active, Snoozed, Dismissed }

public sealed class AppNotification
{
    public long Id { get; set; }
    public long? TaskId { get; set; }
    public DateTime FireAt { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public NotificationStatus Status { get; set; }
    public DateTime? SnoozeUntil { get; set; }
    /// <summary>Не хранится: true только для уведомления, созданного прямо сейчас по задаче с флагом Telegram.</summary>
    public bool SendTelegram { get; set; }
}
