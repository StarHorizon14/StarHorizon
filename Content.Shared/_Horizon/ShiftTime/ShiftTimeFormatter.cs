namespace Content.Shared._Horizon.ShiftTime;

/// <summary>
/// Форматирование длительности смены с поддержкой дней.
/// Стандартный формат TimeSpan "hh\:mm\:ss" после 24 часов обнуляется, а смена теперь идёт до нескольких суток.
/// </summary>
public static class ShiftTimeFormatter
{
    /// <summary>
    /// Полных суток в длительности.
    /// </summary>
    public static int Days(TimeSpan duration)
    {
        return Math.Max(0, (int) Math.Floor(duration.TotalDays));
    }

    /// <summary>
    /// "ЧЧ:ММ:СС" для коротких смен и "N дней ЧЧ:ММ:СС" для смен длиннее суток.
    /// </summary>
    public static string Format(TimeSpan duration)
    {
        var days = Days(duration);
        var time = duration.ToString("hh\\:mm\\:ss");

        return days > 0
            ? Loc.GetString("shift-time-with-days", ("days", days), ("time", time))
            : time;
    }
}
