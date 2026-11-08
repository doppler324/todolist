namespace TodoDock.Core;

public enum Priority { Low = 0, Normal = 1, High = 2 }

public sealed class Folder
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#4F8CFF";
    public string Icon { get; set; } = "📁";
    public int SortOrder { get; set; }
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
}
