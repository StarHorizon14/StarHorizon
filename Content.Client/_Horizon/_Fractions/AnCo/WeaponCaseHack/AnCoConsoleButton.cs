using Robust.Client.UserInterface.Controls;

namespace Content.Client._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Кнопка в стиле консоли: у <see cref="Button.StyleBoxOverride"/> один бокс на все состояния,
/// поэтому бокс и цвет текста меняются при смене <see cref="BaseButton.DrawMode"/>.
/// </summary>
public sealed class AnCoConsoleButton : Button
{
    public AnCoConsoleButton()
    {
        Label.FontOverride = AnCoConsoleTheme.Mono(16);
        ApplyMode();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        ApplyMode();
    }

    private void ApplyMode()
    {
        // ContainerButton вызывает DrawModeChanged() из базового конструктора, когда Label у Button ещё не создан.
        // Свой конструктор потом вызывает ApplyMode() явно, так что состояние всё равно выставится.
        if (Label == null!)
            return;

        var (background, border, text) = DrawMode switch
        {
            DrawModeEnum.Hover => (AnCoConsoleTheme.Border, AnCoConsoleTheme.Accent, AnCoConsoleTheme.Highlight),
            DrawModeEnum.Pressed => (AnCoConsoleTheme.Accent, AnCoConsoleTheme.Accent, AnCoConsoleTheme.Background),
            DrawModeEnum.Disabled => (AnCoConsoleTheme.Panel, AnCoConsoleTheme.Muted, AnCoConsoleTheme.Disabled),
            _ => (AnCoConsoleTheme.Panel, AnCoConsoleTheme.Border, AnCoConsoleTheme.Accent),
        };

        StyleBoxOverride = AnCoConsoleTheme.Box(background, border, 4);
        Label.FontColorOverride = text;
    }
}
