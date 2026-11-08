using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TodoDock.Core;

namespace TodoDock.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly TodoRepository _repo;

    public ObservableCollection<FolderVM> Folders { get; } = new();
    /// <summary>Вкладка «Сегодня»: открытые задачи со сроком на сегодня или просроченные.</summary>
    public ObservableCollection<TaskVM> TodayTasks { get; } = new();
    /// <summary>Папки с галочкой «показывать на рабочем столе» — для окна на рабочем столе.</summary>
    public ObservableCollection<FolderVM> WidgetFolders { get; } = new();
    [ObservableProperty] private string _quickText = "";
    [ObservableProperty] private string _newFolderName = "";

    /// <summary>Данные задач изменились (нужно пересчитать уведомления).</summary>
    public event Action? Changed;

    public MainViewModel(TodoRepository repo) { _repo = repo; Reload(); }

    public void Reload()
    {
        var expanded = Folders.ToDictionary(f => f.Id ?? -1, f => f.IsExpanded);
        Folders.Clear();
        TodayTasks.Clear();
        WidgetFolders.Clear();
        var tasks = _repo.GetTasks();
        var all = new List<FolderVM> { new(null, this) };
        all.AddRange(_repo.GetFolders().Select(f => new FolderVM(f, this)));
        foreach (var f in all)
        {
            if (expanded.TryGetValue(f.Id ?? -1, out var e)) f.IsExpanded = e;
            foreach (var t in tasks.Where(t => t.FolderId == f.Id)) f.Tasks.Add(new TaskVM(t, this) { Folder = f });
            // «Без папки» показываем только когда в ней что-то есть
            if (f.IsReal || f.Count > 0) Folders.Add(f);
        }
        foreach (var f in Folders.Where(f => f.ShowOnDesktop)) WidgetFolders.Add(f);
        foreach (var t in Folders.SelectMany(f => f.Tasks)
                     .Where(t => t.Item.DueAt is { } d && d.Date <= DateTime.Today)
                     .OrderBy(t => t.Item.DueAt))
            TodayTasks.Add(t);
        Changed?.Invoke();
    }

    [RelayCommand]
    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(QuickText)) return;
        var p = QuickAddParser.Parse(QuickText, DateTime.Now);
        QuickText = "";
        if (string.IsNullOrWhiteSpace(p.Title)) return;
        _repo.AddParsed(p, DateTime.Now);
        Reload();
    }

    [RelayCommand]
    private void AddFolder()
    {
        if (string.IsNullOrWhiteSpace(NewFolderName)) return;
        _repo.GetOrCreateFolder(NewFolderName.Trim(), DateTime.Now);
        NewFolderName = "";
        Reload();
    }

    [RelayCommand] private void DeleteTask(TaskVM t) { _repo.DeleteTask(t.Item.Id); Reload(); }
    [RelayCommand] private void RenameFolderStart(FolderVM f) => f.IsEditing = f.IsReal;

    /// <summary>Удаляет папку; подтверждение показывает окно панели (иначе MessageBox прячется под Topmost-панелью).</summary>
    public void DeleteFolder(FolderVM f, bool deleteTasks)
    {
        if (f.Folder == null) return;
        _repo.DeleteFolder(f.Folder.Id, deleteTasks);
        Reload();
    }

    public void SetFolderOnDesktop(FolderVM f, bool show)
    {
        if (f.Folder == null) return;
        _repo.SetFolderShowOnDesktop(f.Folder.Id, show);
        Reload();
    }

    public string? GetSetting(string key) => _repo.GetSetting(key);
    public void SetSetting(string key, string? value) => _repo.SetSetting(key, value);

    public void RenameFolder(long id, string name) => _repo.RenameFolder(id, name);
    public void SaveTask(TaskItem t) => _repo.UpdateTask(t);
    public List<Folder> GetFolders() => _repo.GetFolders();
    public TaskItem CloneTask(TaskItem t) => t.Clone();
    public void UpdateTask(TaskItem t) { _repo.UpdateTask(t); Reload(); }

    public void CompleteTask(TaskVM t)
    {
        _repo.Complete(t.Item.Id, DateTime.Now);
        // даём увидеть зачёркивание, затем обновляем список
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) => { timer.Stop(); Reload(); };
        timer.Start();
    }

    /// <summary>Drag &amp; drop: переносит задачу в папку, перед <paramref name="before"/> (или в конец).</summary>
    public void MoveTask(TaskVM task, FolderVM target, TaskVM? before)
    {
        var ids = target.Tasks.Where(t => t != task).Select(t => t.Item.Id).ToList();
        var idx = before == null || before == task ? ids.Count : Math.Max(0, ids.IndexOf(before.Item.Id));
        ids.Insert(idx, task.Item.Id);
        _repo.PlaceTasks(target.Id, ids);
        Reload();
    }
}
