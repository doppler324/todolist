namespace TodoDock.Core;

/// <summary>
/// Правило повтора. Строковые формы: daily, weekdays, weekly, monthly, days:1,3,5 (DayOfWeek, 0 = вс).
/// </summary>
public sealed class RepeatRule
{
    private readonly string _kind;
    private readonly HashSet<DayOfWeek> _days = new();

    private RepeatRule(string kind) => _kind = kind;

    public static RepeatRule Daily => new("daily");
    public static RepeatRule Weekly => new("weekly");
    public static RepeatRule Monthly => new("monthly");
    public static RepeatRule Weekdays => OnDays(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);

    public static RepeatRule OnDays(params DayOfWeek[] days)
    {
        var r = new RepeatRule("days");
        foreach (var d in days) r._days.Add(d);
        return r;
    }

    public static RepeatRule? Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().ToLowerInvariant();
        switch (s)
        {
            case "daily": return Daily;
            case "weekly": return Weekly;
            case "monthly": return Monthly;
            case "weekdays": return Weekdays;
        }
        if (s.StartsWith("days:"))
        {
            var days = s[5..].Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var n) && n is >= 0 and <= 6 ? (DayOfWeek?)n : null)
                .Where(x => x != null).Select(x => x!.Value).ToArray();
            return days.Length > 0 ? OnDays(days) : null;
        }
        return null;
    }

    public override string ToString() => _kind == "days"
        ? "days:" + string.Join(",", _days.OrderBy(d => ((int)d + 6) % 7).Select(d => (int)d))
        : _kind;

    /// <summary>Следующее вхождение строго после <paramref name="from"/> (время суток сохраняется).</summary>
    public DateTime Next(DateTime from)
    {
        switch (_kind)
        {
            case "daily": return from.AddDays(1);
            case "weekly": return from.AddDays(7);
            case "monthly": return from.AddMonths(1);
            default:
                for (var i = 1; i <= 7; i++)
                {
                    var c = from.AddDays(i);
                    if (_days.Contains(c.DayOfWeek)) return c;
                }
                return from.AddDays(1);
        }
    }

    public string Describe()
    {
        if (_kind == "daily") return "каждый день";
        if (_kind == "weekly") return "каждую неделю";
        if (_kind == "monthly") return "каждый месяц";
        if (ToString() == Weekdays.ToString()) return "пн–пт";
        var names = new[] { "вс", "пн", "вт", "ср", "чт", "пт", "сб" };
        return string.Join(",", _days.OrderBy(d => ((int)d + 6) % 7).Select(d => names[(int)d]));
    }
}
