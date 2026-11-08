using CommunityToolkit.Mvvm.ComponentModel;
using TodoDock.Core;

namespace TodoDock.ViewModels;

public sealed partial class TaskVM : ObservableObject
{
    private readonly MainViewModel _main;
    public TaskItem Item { get; }
    public FolderVM Folder { get; set; } = null!;

    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title;

    public TaskVM(TaskItem item, MainViewModel main)
    {
        Item = item; _main = main; _title = item.Title; _isDone = item.IsDone;
    }

    partial void OnIsDoneChanged(bool value) { if (value && !Item.IsDone) _main.CompleteTask(this); }

    public bool IsOverdue => Item.DueAt is { } d && d < DateTime.Now && !IsDone;

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (Item.DueAt is { } d)
            {
                var day = d.Date == DateTime.Today ? "сегодня" : d.Date == DateTime.Today.AddDays(1) ? "завтра" : d.ToString("dd.MM");
                parts.Add($"🔔 {day} {d:HH:mm}");
            }
            if (RepeatRule.Parse(Item.RepeatRule) is { } r) parts.Add("🔁 " + r.Describe());
            if (Item.Reminders.Count > 0) parts.Add($"⏰ +{Item.Reminders.Count}");
            return string.Join("  ", parts);
        }
    }

    public void CommitEdit()
    {
        IsEditing = false;
        var t = Title.Trim();
        if (t.Length == 0 || t == Item.Title) { Title = Item.Title; return; }
        Item.Title = t; _main.SaveTask(Item);
    }
}
