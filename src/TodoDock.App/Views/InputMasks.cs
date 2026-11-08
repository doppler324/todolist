using System.Windows.Controls;
using System.Windows.Input;

namespace TodoDock.Views;

/// <summary>
/// Маски ввода: время «ЧЧ:ММ» и дата «дд.мм.гггг» — разделители ставятся сами, пока набираете только цифры.
/// При удалении разделитель заново не дописывается.
/// </summary>
internal static class InputMasks
{
    public static void AttachTime(TextBox tb) => Attach(tb, TodoDock.Core.InputFormat.FormatTime);
    public static void AttachDate(TextBox tb) => Attach(tb, TodoDock.Core.InputFormat.FormatDate);

    private static void Attach(TextBox tb, Func<string, bool, bool, string> format)
    {
        var prevLen = tb.Text.Length;
        var busy = false;
        tb.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        tb.TextChanged += (_, _) =>
        {
            if (busy) return;
            busy = true;
            var grew = tb.Text.Length > prevLen;
            var digits = new string(tb.Text.Where(char.IsDigit).ToArray());
            var text = format(digits, grew, false);
            if (text != tb.Text) { tb.Text = text; tb.CaretIndex = text.Length; }
            prevLen = tb.Text.Length;
            busy = false;
        };
        // при уходе из поля приводим к полному виду (9:5 → 09:05)
        tb.LostFocus += (_, _) => { busy = true; tb.Text = format(new string(tb.Text.Where(char.IsDigit).ToArray()), false, true); prevLen = tb.Text.Length; busy = false; };
    }

}
