namespace TodoDock.Core;

/// <summary>Форматирование цифр в «ЧЧ:ММ» и «дд.мм.гггг» по мере ввода.</summary>
public static class InputFormat
{
    public static string FormatTime(string digits, bool grew, bool final)
    {
        digits = digits.Length > 4 ? digits[..4] : digits;
        if (digits.Length == 0) return "";
        string hour, min;
        if (digits[0] >= '3') { hour = digits[..1]; min = digits[1..]; }                 // 9 → 9:..
        else if (digits.Length == 1) { hour = digits; min = ""; }
        else if (int.Parse(digits[..2]) > 23) { hour = digits[..1]; min = digits[1..]; }  // 25 → 2:5
        else { hour = digits[..2]; min = digits[2..]; }
        if (min.Length > 2) min = min[..2];
        if (min.Length > 0 && min[0] > '5') min = "0" + min[0];                           // 16:7 → 16:07

        var hourComplete = hour.Length == 2 || (hour.Length == 1 && digits[0] >= '3');
        if (final)
        {
            if (min.Length == 0) min = "00";
            if (min.Length == 1) min += "0";
            return hour.PadLeft(2, '0') + ":" + min;
        }
        return hour + (min.Length > 0 || (grew && hourComplete) ? ":" : "") + min;
    }

    public static string FormatDate(string digits, bool grew, bool final)
    {
        digits = digits.Length > 8 ? digits[..8] : digits;
        if (digits.Length == 0) return "";
        var rest = digits;
        string Take(char maxFirst)
        {
            if (rest.Length == 0) return "";
            string part;
            if (rest[0] > maxFirst) { part = "0" + rest[0]; rest = rest[1..]; }     // 5 → 05
            else if (rest.Length > 1) { part = rest[..2]; rest = rest[2..]; }
            else { part = rest; rest = ""; }
            return part;
        }
        var day = Take('3');
        var month = Take('1');
        var year = rest.Length > 4 ? rest[..4] : rest;

        var sb = new System.Text.StringBuilder(day);
        if (month.Length > 0 || (grew && day.Length == 2)) sb.Append('.');
        sb.Append(month);
        if (year.Length > 0 || (grew && month.Length == 2)) sb.Append('.');
        sb.Append(year);
        return sb.ToString();
    }
}
