using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TodoDock.Core;

public sealed class TodoRepository : IDisposable
{
    private readonly SqliteConnection _db;

    public TodoRepository(string connectionString)
    {
        _db = new SqliteConnection(connectionString);
        _db.Open();
        Exec("PRAGMA foreign_keys=ON;");
        Exec(@"
CREATE TABLE IF NOT EXISTS folders(
  id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, color TEXT NOT NULL DEFAULT '#4F8CFF',
  icon TEXT NOT NULL DEFAULT '📁', sort_order INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS tasks(
  id INTEGER PRIMARY KEY AUTOINCREMENT, folder_id INTEGER NULL REFERENCES folders(id) ON DELETE SET NULL,
  title TEXT NOT NULL, note TEXT NOT NULL DEFAULT '', priority INTEGER NOT NULL DEFAULT 1,
  status TEXT NOT NULL DEFAULT 'open', due_at TEXT NULL, repeat_rule TEXT NULL,
  sort_order INTEGER NOT NULL DEFAULT 0, created_at TEXT NOT NULL, completed_at TEXT NULL);");
    }

    public static TodoRepository OpenFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new TodoRepository($"Data Source={path}");
    }

    public static TodoRepository InMemory() => new("Data Source=:memory:");

    // ---------- Папки ----------
    public List<Folder> GetFolders()
    {
        var list = new List<Folder>();
        using var c = Cmd("SELECT id,name,color,icon,sort_order FROM folders ORDER BY sort_order,id");
        using var r = c.ExecuteReader();
        while (r.Read())
            list.Add(new Folder { Id = r.GetInt64(0), Name = r.GetString(1), Color = r.GetString(2), Icon = r.GetString(3), SortOrder = r.GetInt32(4) });
        return list;
    }

    public Folder AddFolder(string name, DateTime now)
    {
        using var c = Cmd("INSERT INTO folders(name,sort_order,created_at) VALUES($n,(SELECT IFNULL(MAX(sort_order),0)+1 FROM folders),$c); SELECT last_insert_rowid();");
        c.Parameters.AddWithValue("$n", name.Trim());
        c.Parameters.AddWithValue("$c", Iso(now));
        var id = (long)c.ExecuteScalar()!;
        return GetFolders().First(f => f.Id == id);
    }

    public Folder GetOrCreateFolder(string name, DateTime now) =>
        GetFolders().FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.CurrentCultureIgnoreCase)) ?? AddFolder(name, now);

    public void RenameFolder(long id, string name)
    {
        using var c = Cmd("UPDATE folders SET name=$n WHERE id=$i");
        c.Parameters.AddWithValue("$n", name.Trim()); c.Parameters.AddWithValue("$i", id); c.ExecuteNonQuery();
    }

    /// <summary>Удаляет папку; задачи переносятся в «Без папки» либо удаляются.</summary>
    public void DeleteFolder(long id, bool deleteTasks)
    {
        if (deleteTasks) { using var d = Cmd("DELETE FROM tasks WHERE folder_id=$i"); d.Parameters.AddWithValue("$i", id); d.ExecuteNonQuery(); }
        using var c = Cmd("DELETE FROM folders WHERE id=$i"); c.Parameters.AddWithValue("$i", id); c.ExecuteNonQuery();
    }

    // ---------- Задачи ----------
    public List<TaskItem> GetTasks(bool includeDone = false)
    {
        var list = new List<TaskItem>();
        using var c = Cmd("SELECT id,folder_id,title,note,priority,status,due_at,repeat_rule,sort_order,created_at,completed_at FROM tasks " +
                          (includeDone ? "" : "WHERE status='open' ") + "ORDER BY sort_order,id");
        using var r = c.ExecuteReader();
        while (r.Read())
            list.Add(new TaskItem
            {
                Id = r.GetInt64(0), FolderId = r.IsDBNull(1) ? null : r.GetInt64(1), Title = r.GetString(2), Note = r.GetString(3),
                Priority = (Priority)r.GetInt32(4), IsDone = r.GetString(5) == "done",
                DueAt = r.IsDBNull(6) ? null : Parse(r.GetString(6)), RepeatRule = r.IsDBNull(7) ? null : r.GetString(7),
                SortOrder = r.GetInt32(8), CreatedAt = Parse(r.GetString(9)), CompletedAt = r.IsDBNull(10) ? null : Parse(r.GetString(10))
            });
        return list;
    }

    public TaskItem AddTask(string title, long? folderId, Priority priority, DateTime? due, RepeatRule? repeat, DateTime now)
    {
        using var c = Cmd(@"INSERT INTO tasks(folder_id,title,priority,due_at,repeat_rule,sort_order,created_at)
VALUES($f,$t,$p,$d,$r,(SELECT IFNULL(MAX(sort_order),0)+1 FROM tasks),$c); SELECT last_insert_rowid();");
        c.Parameters.AddWithValue("$f", (object?)folderId ?? DBNull.Value);
        c.Parameters.AddWithValue("$t", title);
        c.Parameters.AddWithValue("$p", (int)priority);
        c.Parameters.AddWithValue("$d", due is { } dv ? Iso(dv) : DBNull.Value);
        c.Parameters.AddWithValue("$r", repeat?.ToString() is { } rs ? rs : DBNull.Value);
        c.Parameters.AddWithValue("$c", Iso(now));
        var id = (long)c.ExecuteScalar()!;
        return GetTasks(true).First(t => t.Id == id);
    }

    public TaskItem AddParsed(ParsedTask p, DateTime now)
    {
        long? folderId = p.FolderName is { Length: > 0 } fn ? GetOrCreateFolder(fn, now).Id : null;
        return AddTask(p.Title, folderId, p.Priority, p.DueAt, p.Repeat, now);
    }

    public void UpdateTask(TaskItem t)
    {
        using var c = Cmd("UPDATE tasks SET title=$t,note=$n,priority=$p,due_at=$d,repeat_rule=$r,folder_id=$f WHERE id=$i");
        c.Parameters.AddWithValue("$t", t.Title); c.Parameters.AddWithValue("$n", t.Note);
        c.Parameters.AddWithValue("$p", (int)t.Priority);
        c.Parameters.AddWithValue("$d", t.DueAt is { } d ? Iso(d) : DBNull.Value);
        c.Parameters.AddWithValue("$r", (object?)t.RepeatRule ?? DBNull.Value);
        c.Parameters.AddWithValue("$f", (object?)t.FolderId ?? DBNull.Value);
        c.Parameters.AddWithValue("$i", t.Id); c.ExecuteNonQuery();
    }

    public void DeleteTask(long id)
    {
        using var c = Cmd("DELETE FROM tasks WHERE id=$i"); c.Parameters.AddWithValue("$i", id); c.ExecuteNonQuery();
    }

    /// <summary>Завершает задачу. Для повторяющейся создаёт следующее вхождение и возвращает его.</summary>
    public TaskItem? Complete(long id, DateTime now)
    {
        var t = GetTasks(true).First(x => x.Id == id);
        using (var c = Cmd("UPDATE tasks SET status='done',completed_at=$c WHERE id=$i"))
        { c.Parameters.AddWithValue("$c", Iso(now)); c.Parameters.AddWithValue("$i", id); c.ExecuteNonQuery(); }

        var rule = RepeatRule.Parse(t.RepeatRule);
        if (rule == null) return null;
        var next = rule.Next(t.DueAt ?? now);
        while (next <= now) next = rule.Next(next); // пропущенные вхождения не копим
        var created = AddTask(t.Title, t.FolderId, t.Priority, next, rule, now);
        created.Note = t.Note; UpdateTask(created);
        return created;
    }

    public void Reopen(long id)
    {
        using var c = Cmd("UPDATE tasks SET status='open',completed_at=NULL WHERE id=$i"); c.Parameters.AddWithValue("$i", id); c.ExecuteNonQuery();
    }

    /// <summary>Задаёт порядок и папку для списка задач (drag &amp; drop).</summary>
    public void PlaceTasks(long? folderId, IReadOnlyList<long> orderedIds)
    {
        using var tx = _db.BeginTransaction();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            using var c = Cmd("UPDATE tasks SET folder_id=$f,sort_order=$o WHERE id=$i");
            c.Transaction = tx;
            c.Parameters.AddWithValue("$f", (object?)folderId ?? DBNull.Value);
            c.Parameters.AddWithValue("$o", i); c.Parameters.AddWithValue("$i", orderedIds[i]); c.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void ReorderFolders(IReadOnlyList<long> orderedIds)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        { using var c = Cmd("UPDATE folders SET sort_order=$o WHERE id=$i"); c.Parameters.AddWithValue("$o", i); c.Parameters.AddWithValue("$i", orderedIds[i]); c.ExecuteNonQuery(); }
    }

    // ---------- helpers ----------
    private static string Iso(DateTime d) => d.ToString("o", CultureInfo.InvariantCulture);
    private static DateTime Parse(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private SqliteCommand Cmd(string sql) { var c = _db.CreateCommand(); c.CommandText = sql; return c; }
    private void Exec(string sql) { using var c = Cmd(sql); c.ExecuteNonQuery(); }
    public void Dispose() => _db.Dispose();
}
