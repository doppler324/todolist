using TodoDock.Core;
using Xunit;

public class ParserTests
{
    // Среда 2026-11-11 12:00
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0);

    [Fact]
    public void Parses_all_tokens()
    {
        var p = QuickAddParser.Parse("Купить хлеб #Дом !высокий @завтра 18:00 *ежедневно", Now);
        Assert.Equal("Купить хлеб", p.Title);
        Assert.Equal("Дом", p.FolderName);
        Assert.Equal(Priority.High, p.Priority);
        Assert.Equal(new DateTime(2026, 11, 12, 18, 0, 0), p.DueAt);
        Assert.Equal("daily", p.Repeat!.ToString());
    }

    [Fact] public void Plain_title() { var p = QuickAddParser.Parse("Просто задача", Now); Assert.Equal("Просто задача", p.Title); Assert.Null(p.DueAt); Assert.Null(p.FolderName); }
    [Fact] public void Date_only_defaults_to_nine() => Assert.Equal(new DateTime(2026, 11, 13, 9, 0, 0), QuickAddParser.Parse("x @13.11", Now).DueAt);
    [Fact] public void Past_time_today_rolls_to_tomorrow() => Assert.Equal(new DateTime(2026, 11, 12, 9, 0, 0), QuickAddParser.Parse("x @09:00", Now).DueAt);
    [Fact] public void Future_time_today() => Assert.Equal(new DateTime(2026, 11, 11, 18, 30, 0), QuickAddParser.Parse("x @18:30", Now).DueAt);
    [Fact] public void Past_date_rolls_to_next_year() => Assert.Equal(new DateTime(2027, 1, 5, 9, 0, 0), QuickAddParser.Parse("x @05.01", Now).DueAt);
    [Fact] public void Invalid_date_stays_in_title() => Assert.Equal("x @32.01", QuickAddParser.Parse("x @32.01", Now).Title);
    [Fact] public void Weekday_list_repeat() => Assert.Equal("days:1,3,5", QuickAddParser.Parse("x *пн,ср,пт", Now).Repeat!.ToString());
}

public class RepeatTests
{
    [Fact] public void Daily() => Assert.Equal(new DateTime(2026, 11, 12, 10, 0, 0), RepeatRule.Daily.Next(new DateTime(2026, 11, 11, 10, 0, 0)));
    [Fact] public void Monthly_clamps() => Assert.Equal(new DateTime(2026, 2, 28), RepeatRule.Monthly.Next(new DateTime(2026, 1, 31)));
    [Fact] public void Weekdays_skip_weekend() => Assert.Equal(new DateTime(2026, 11, 16), RepeatRule.Weekdays.Next(new DateTime(2026, 11, 13))); // пт → пн
    [Fact] public void Roundtrip() => Assert.Equal("days:1,3,5", RepeatRule.Parse("days:1,3,5")!.ToString());
    [Fact] public void Garbage_is_null() => Assert.Null(RepeatRule.Parse("blah"));
}

public class RepositoryTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0);

    [Fact]
    public void AddParsed_creates_folder_once()
    {
        using var r = TodoRepository.InMemory();
        r.AddParsed(QuickAddParser.Parse("a #Работа", Now), Now);
        r.AddParsed(QuickAddParser.Parse("b #работа", Now), Now);
        Assert.Single(r.GetFolders());
        Assert.Equal(2, r.GetTasks().Count);
    }

    [Fact]
    public void Completing_repeating_task_creates_next()
    {
        using var r = TodoRepository.InMemory();
        var t = r.AddParsed(QuickAddParser.Parse("зарядка @завтра 07:00 *ежедневно", Now), Now);
        var next = r.Complete(t.Id, Now)!;
        Assert.Equal(new DateTime(2026, 11, 13, 7, 0, 0), next.DueAt);
        Assert.Single(r.GetTasks());
    }

    [Fact]
    public void Completing_plain_task_creates_nothing()
    {
        using var r = TodoRepository.InMemory();
        var t = r.AddTask("x", null, Priority.Normal, null, null, Now);
        Assert.Null(r.Complete(t.Id, Now));
        Assert.Empty(r.GetTasks());
    }

    [Fact]
    public void Delete_folder_moves_or_deletes_tasks()
    {
        using var r = TodoRepository.InMemory();
        var f = r.AddFolder("F", Now);
        r.AddTask("a", f.Id, Priority.Normal, null, null, Now);
        r.DeleteFolder(f.Id, deleteTasks: false);
        Assert.Null(r.GetTasks().Single().FolderId);
        var g = r.AddFolder("G", Now);
        r.AddTask("b", g.Id, Priority.Normal, null, null, Now);
        r.DeleteFolder(g.Id, deleteTasks: true);
        Assert.Single(r.GetTasks());
    }

    [Fact]
    public void PlaceTasks_reorders_and_moves()
    {
        using var r = TodoRepository.InMemory();
        var f = r.AddFolder("F", Now);
        var a = r.AddTask("a", null, Priority.Normal, null, null, Now);
        var b = r.AddTask("b", null, Priority.Normal, null, null, Now);
        r.PlaceTasks(f.Id, new[] { b.Id, a.Id });
        var ts = r.GetTasks();
        Assert.Equal(new[] { "b", "a" }, ts.Select(t => t.Title));
        Assert.All(ts, t => Assert.Equal(f.Id, t.FolderId));
    }
}

public class NotificationTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0);

    [Fact]
    public void Tick_fires_due_task_once_and_it_stays_active()
    {
        using var r = TodoRepository.InMemory();
        r.AddTask("x", null, Priority.Normal, Now.AddMinutes(-5), null, Now);
        r.AddTask("future", null, Priority.Normal, Now.AddHours(1), null, Now);
        Assert.Single(r.Tick(Now));
        Assert.Empty(r.Tick(Now.AddMinutes(1)));                 // повторно не создаётся
        Assert.Single(r.GetActiveNotifications());               // и висит, пока не закрыто
    }

    [Fact]
    public void Missed_notifications_fire_after_restart()
    {
        using var r = TodoRepository.InMemory();
        r.AddTask("x", null, Priority.Normal, Now, null, Now);
        Assert.Single(r.Tick(Now.AddDays(2)));
    }

    [Fact]
    public void Snooze_hides_then_wakes()
    {
        using var r = TodoRepository.InMemory();
        r.AddTask("x", null, Priority.Normal, Now, null, Now);
        var n = r.Tick(Now).Single();
        r.Snooze(n.Id, Now.AddMinutes(5));
        Assert.Empty(r.GetActiveNotifications());
        Assert.Equal(Now.AddMinutes(5), r.NextWake());
        Assert.Empty(r.Tick(Now.AddMinutes(4)));
        Assert.Single(r.Tick(Now.AddMinutes(5)));
        Assert.Single(r.GetActiveNotifications());
    }

    [Fact]
    public void Dismiss_and_complete_clear_notifications()
    {
        using var r = TodoRepository.InMemory();
        var a = r.AddTask("a", null, Priority.Normal, Now, null, Now);
        var b = r.AddTask("b", null, Priority.Normal, Now, null, Now);
        var ns = r.Tick(Now);
        r.Dismiss(ns.First(n => n.TaskId == a.Id).Id);
        Assert.Single(r.GetActiveNotifications());
        r.Complete(b.Id, Now);
        Assert.Empty(r.GetActiveNotifications());
        Assert.Empty(r.Tick(Now.AddMinutes(1)));                 // закрытое не возвращается
    }

    [Fact]
    public void NextWake_is_earliest_due()
    {
        using var r = TodoRepository.InMemory();
        Assert.Null(r.NextWake());
        r.AddTask("a", null, Priority.Normal, Now.AddHours(3), null, Now);
        r.AddTask("b", null, Priority.Normal, Now.AddHours(1), null, Now);
        Assert.Equal(Now.AddHours(1), r.NextWake());
    }

    [Fact]
    public void Repeating_task_gets_new_notification_for_next_occurrence()
    {
        using var r = TodoRepository.InMemory();
        var t = r.AddTask("зарядка", null, Priority.Normal, Now, RepeatRule.Daily, Now);
        r.Tick(Now);
        r.Complete(t.Id, Now);
        Assert.Empty(r.GetActiveNotifications());
        Assert.Single(r.Tick(Now.AddDays(1)));
    }
}
