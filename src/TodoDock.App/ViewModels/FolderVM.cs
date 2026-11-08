using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TodoDock.Core;

namespace TodoDock.ViewModels;

public sealed partial class FolderVM : ObservableObject
{
    private readonly MainViewModel _main;
    /// <summary>null — виртуальная папка «Без папки».</summary>
    public Folder? Folder { get; }
    public long? Id => Folder?.Id;
    public bool IsReal => Folder != null;

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isEditing;
    public ObservableCollection<TaskVM> Tasks { get; } = new();
    public string Color => Folder?.Color ?? "#9AA3B2";
    public string Icon => Folder?.Icon ?? "📥";

    public FolderVM(Folder? folder, MainViewModel main)
    {
        Folder = folder; _main = main; _name = folder?.Name ?? "Без папки";
        Tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Count));
    }

    public int Count => Tasks.Count;

    /// <summary>Показывать задачи папки в окне на рабочем столе.</summary>
    public bool ShowOnDesktop => Folder?.ShowOnDesktop == true;
    public string DesktopMenuHeader => ShowOnDesktop ? "✓ На рабочем столе (убрать)" : "Показывать на рабочем столе";

    public void CommitEdit()
    {
        IsEditing = false;
        var n = Name.Trim();
        if (Folder == null || n.Length == 0 || n == Folder.Name) { Name = Folder?.Name ?? Name; return; }
        Folder.Name = n; _main.RenameFolder(Folder.Id, n);
    }
}
