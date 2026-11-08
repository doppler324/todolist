using System.Globalization;
using System.Text.RegularExpressions;

namespace TodoDock.Core;

public sealed record ParsedTask(string Title, string? FolderName, DateTime? DueAt, RepeatRule? Repeat);

/// <summary>
/// Разбор быстрого ввода: «Купить хлеб #Дом !высокий @завтра 18:00 *ежедневно».
/// #папка · @дата [время] · *повтор (ежедневно, будни, пн,ср,пт или числа месяца: 5,15,25). Дата без времени = 09:00.
/// </summary>
public static class QuickAddParser
{
    private static readonly Regex Time = new(@"^(\d{1,2}):(\d{2})$");
    private static readonly Regex Date = new(@"^(\d{1,2})\.(\d{1,2})(?:\.(\d{4}))?$");

    public static ParsedTask Parse(string input, DateTime now)
    {
        var title = new List<string>();
        string? folder = null;
        DateTime? date = null;
        TimeSpan? time = null;
        RepeatRule? repeat = null;
        var prevWasDate = false;

        foreach (var tok in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var isDate = false;
            if (tok.Length > 1 && tok[0] == '#') folder = tok[1..];
            else if (tok.Length > 1 && tok[0] == '*' && ParseRepeat(tok[1..]) is { } r) repeat = r;
            else if (tok.Length > 1 && tok[0] == '@')
            {
                isDate = true;
                var v = tok[1..].ToLowerInvariant();
                if (Time.IsMatch(v)) time = ParseTime(v);
                else if (ParseDate(v, now) is { } d) date = d;
                else { isDate = false; title.Add(tok); }
            }
            else if (prevWasDate && Time.IsMatch(tok)) { time = ParseTime(tok); isDate = true; }
            else title.Add(tok);
            prevWasDate = isDate;
        }

        DateTime? due = null;
        if (date != null || time != null)
        {
            var day = date ?? now.Date;
            var t = time ?? TimeSpan.FromHours(9);
            due = day + t;
            if (date == null && due <= now) due = due.Value.AddDays(1); // только время, уже прошло → завтра
        }

        return new ParsedTask(string.Join(' ', title), folder, due, repeat);
    }

    private static TimeSpan? ParseTime(string s)
    {
        var m = Time.Match(s);
        int h = int.Parse(m.Groups[1].Value), min = int.Parse(m.Groups[2].Value);
        return h < 24 && min < 60 ? new TimeSpan(h, min, 0) : null;
    }

    private static DateTime? ParseDate(string v, DateTime now)
    {
        switch (v)
        {
            case "сегодня": case "today": return now.Date;
            case "завтра": case "tomorrow": return now.Date.AddDays(1);
            case "послезавтра": return now.Date.AddDays(2);
        }
        var m = Date.Match(v);
        if (!m.Success) return null;
        int day = int.Parse(m.Groups[1].Value), mon = int.Parse(m.Groups[2].Value);
        var hasYear = m.Groups[3].Success;
        var year = hasYear ? int.Parse(m.Groups[3].Value) : now.Year;
        if (mon is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, mon)) return null;
        var d = new DateTime(year, mon, day);
        if (!hasYear && d < now.Date) d = d.AddYears(1);
        return d;
    }

    private static RepeatRule? ParseRepeat(string v)
    {
        switch (v.ToLowerInvariant())
        {
            case "ежедневно": case "daily": return RepeatRule.Daily;
            case "еженедельно": case "weekly": return RepeatRule.Weekly;
            case "ежемесячно": case "monthly": return RepeatRule.Monthly;
            case "будни": case "weekdays": return RepeatRule.Weekdays;
        }
        var nums = v.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (nums.Length > 0 && nums.All(n => int.TryParse(n, out var d) && d is >= 1 and <= 31))
            return RepeatRule.OnMonthDays(nums.Select(int.Parse));
        var map = new Dictionary<string, DayOfWeek>
        {
            ["пн"] = DayOfWeek.Monday, ["вт"] = DayOfWeek.Tuesday, ["ср"] = DayOfWeek.Wednesday,
            ["чт"] = DayOfWeek.Thursday, ["пт"] = DayOfWeek.Friday, ["сб"] = DayOfWeek.Saturday, ["вс"] = DayOfWeek.Sunday
        };
        var parts = v.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts.All(map.ContainsKey) ? RepeatRule.OnDays(parts.Select(p => map[p]).ToArray()) : null;
    }
}
