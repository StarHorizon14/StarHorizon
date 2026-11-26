namespace Content.Shared._Horizon.Durability;

/// <summary>
/// Общее оформление прочности для осмотра и статуса предмета в руке.
/// Используется износом оружия и бронепластинами, чтобы они выглядели одинаково.
/// </summary>
public static class DurabilityFormat
{
    private const int Segments = 10;

    /// <summary>
    /// Доля оставшейся прочности в диапазоне [0, 1].
    /// </summary>
    public static float Fraction(float current, float max)
    {
        return max > 0f ? Math.Clamp(current / max, 0f, 1f) : 0f;
    }

    public static int Percent(float fraction)
    {
        return (int) MathF.Ceiling(Math.Clamp(fraction, 0f, 1f) * 100f);
    }

    /// <summary>
    /// Цвет разметки: зелёный — целое, жёлтый — потрёпанное, красный — на грани поломки.
    /// </summary>
    public static string Color(float fraction)
    {
        if (fraction >= 0.6f)
            return "lime";

        return fraction >= 0.25f ? "yellow" : "red";
    }

    /// <summary>
    /// Множитель цены изношенной вещи: целая стоит полную цену,
    /// дальше цена линейно падает до <paramref name="brokenFraction"/> у полностью изношенной.
    /// </summary>
    public static double PriceFactor(float fraction, float brokenFraction)
    {
        var floor = Math.Clamp(brokenFraction, 0f, 1f);
        return floor + (1f - floor) * Math.Clamp(fraction, 0f, 1f);
    }

    /// <summary>
    /// Снижает цену в <see cref="Content.Shared.Cargo.PriceCalculationEvent"/> с учётом износа.
    /// Событие вызывается до сложения со статической ценой, поэтому скидку вычитаем заранее.
    /// </summary>
    public static void ApplyWearToPrice(ref Content.Shared.Cargo.PriceCalculationEvent args, double staticPrice, float fraction, float brokenFraction)
    {
        if (staticPrice <= 0)
            return;

        args.Price -= staticPrice * (1 - PriceFactor(fraction, brokenFraction));
    }

    /// <summary>
    /// Текстовая шкала из <see cref="Segments"/> делений.
    /// </summary>
    public static string Bar(float fraction)
    {
        var lit = (int) MathF.Ceiling(Math.Clamp(fraction, 0f, 1f) * Segments);
        return new string('█', lit) + new string('░', Segments - lit);
    }
}
