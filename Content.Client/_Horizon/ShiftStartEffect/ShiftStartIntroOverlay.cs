using System.Numerics;
using Content.Shared._Horizon.ShiftStartEffect;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.ShiftStartEffect;

/// <summary>
/// Полноэкранное интро при появлении: зелёный CRT-экран, название станции, строки брифинга
/// и список экипажа по отделам, после чего экран затухает в игру.
/// </summary>
public sealed class ShiftStartIntroOverlay : Overlay
{
    [Dependency] private readonly IAudioManager _audio = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    // Таймлайн, секунды.
    private const float FadeIn = 0.8f;
    private const float CompanyAt = 0.6f;
    // Логотип проявляется медленно, остальной текст печатается.
    private const float LogoFade = 2.4f;
    private const int TimeLineIndex = 1;
    private const int ThreatLineIndex = 2;
    private const float TerminalAt = TitleAt;
    private const float TerminalStep = 0.9f;
    private const float TerminalRowHeight = 16f;
    private const int RecordLineIndex = 0;
    private const float LogoScale = 4f;
    private const float TitleAt = 3.2f;
    private const float TitleCharsPerSecond = 11f;
    private const float TypeCharsPerSecond = 45f;
    private const float LinesAt = 5.2f;
    private const float LineStep = 1.0f;
    private const float ManifestAt = 9.0f;
    private const float EntryStep = 0.2f;
    private const float FadeOutAt = 13.8f;
    private const float Total = 15.0f;
    // Минимальный интервал между щелчками, не чаще ~22 раз в секунду.
    private const float TypeSoundInterval = 0.045f;

    private static readonly Color Text = Color.FromHex("#bfffd0");
    private static readonly Color Dim = Color.FromHex("#4fa06a");
    private static readonly Color RecordRed = Color.FromHex("#ff2a2a");

    /// <summary>Интро доиграло, система может убрать оверлей.</summary>
    public bool Finished;

    private readonly ShiftStartIntroEvent _data;
    private readonly TimeSpan _start;
    private readonly TypingSoundSynth _typingSound;
    private int _typedChars;
    private int _typedPrev;
    private TimeSpan _nextSound;
    private readonly Texture _logo;
    private readonly Font _pixel;
    private readonly Font _mono;
    private readonly Font _monoSmall;

    public ShiftStartIntroOverlay(ShiftStartIntroEvent data)
    {
        IoCManager.InjectDependencies(this);
        _data = data;
        _start = _timing.RealTime;
        _typingSound = new TypingSoundSynth(_audio, _timing);

        _logo = _cache.GetResource<TextureResource>("/Textures/_Horizon/Interface/ShiftStart/nanotrasen.png").Texture;
        var pixel = _cache.GetResource<FontResource>("/Fonts/_Horizon/Pixelizer.ttf");
        var mono = _cache.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf");
        _pixel = new VectorFont(pixel, 56);
        _mono = new VectorFont(mono, 16);
        _monoSmall = new VectorFont(mono, 12);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var t = (float) (_timing.RealTime - _start).TotalSeconds;
        if (t >= Total)
        {
            Finished = true;
            return;
        }

        _typedChars = 0;
        var alpha = Math.Clamp(t / FadeIn, 0f, 1f) * Math.Clamp((Total - t) / (Total - FadeOutAt), 0f, 1f);
        var h = args.ScreenHandle;
        // Область рисования оверлея может быть выше окна, тогда нижние элементы уходят за экран,
        // поэтому высоту ограничиваем реальным размером окна.
        var size = (Vector2) args.ViewportBounds.Size;
        if (_clyde.MainWindow != null)
            size.Y = MathF.Min(size.Y, _clyde.MainWindow.Size.Y);
        var mid = size.X / 2f;

        h.DrawRect(new UIBox2(Vector2.Zero, size), Color.Black.WithAlpha(0.88f * alpha));

        // Строки развёртки.
        for (var y = 0f; y < size.Y; y += 3f)
            h.DrawLine(new Vector2(0, y), new Vector2(size.X, y), Color.Black.WithAlpha(0.25f * alpha));

        // Яркая полоса, бегущая по экрану.
        var bar = t * 180f % (size.Y + 120f) - 60f;
        h.DrawRect(new UIBox2(0, bar, size.X, bar + 40f), Text.WithAlpha(0.03f * alpha));

        DrawTerminal(h, t, alpha, size);

        var logoSize = (Vector2) _logo.Size * LogoScale;
        var logoTop = size.Y * 0.04f;
        // Заголовок идёт сразу под логотипом.
        var y0 = logoTop + logoSize.Y - 40f;

        if (t > CompanyAt)
        {
            h.DrawTextureRect(_logo,
                UIBox2.FromDimensions(new Vector2(mid - logoSize.X / 2f, logoTop), logoSize),
                Color.White.WithAlpha(alpha * Math.Clamp((t - CompanyAt) / LogoFade, 0f, 1f)));
        }

        if (t > TitleAt)
        {
            var title = _data.Title.ToUpperInvariant();
            DrawCentered(h, _pixel, title, Typed(title, t, TitleAt, TitleCharsPerSecond), mid, y0 + 60f, Color.White.WithAlpha(alpha));
        }

        var ly = y0 + 160f;
        for (var i = 0; i < _data.Lines.Length; i++)
        {
            var at = LinesAt + i * LineStep;
            if (t < at)
                continue;

            // Третья строка — уровень угрозы, красится в цвет уровня.
            var color = i == ThreatLineIndex ? _data.ThreatColor : i == _data.Lines.Length - 1 ? Dim : Text;
            var text = i == TimeLineIndex ? $"{_data.Lines[i]} // {FormatTime(t)}" : _data.Lines[i];
            DrawCentered(h, _mono, text, Typed(text, t, at), mid, ly + i * 30f, color.WithAlpha(alpha));
        }

        DrawManifest(h, t, alpha, size, ly + _data.Lines.Length * 30f + 24f);

        PlayTypeSound(t);
    }

    /// <summary>
    /// Часть строки, напечатанная к моменту t. Учитывает общее число напечатанных символов для звука.
    /// </summary>
    private string Typed(string text, float t, float at, float charsPerSecond = TypeCharsPerSecond)
    {
        if (t < at)
            return string.Empty;

        var n = Math.Min(text.Length, (int) ((t - at) * charsPerSecond) + 1);
        _typedChars += n;
        return text[..n];
    }

    /// <summary>
    /// Щелчок печати на каждый новый символ, поэтому звук совпадает с текстом на экране.
    /// </summary>
    private void PlayTypeSound(float t)
    {
        _typingSound.Update();

        var now = _timing.RealTime;
        var fresh = _typedChars - _typedPrev;
        _typedPrev = _typedChars;

        if (fresh <= 0 || t >= FadeOutAt || now < _nextSound)
            return;

        _typingSound.Tick();
        _nextSound = now + TimeSpan.FromSeconds(TypeSoundInterval);
    }

    /// <summary>Освобождает звуковые ресурсы, вызывается при удалении оверлея.</summary>
    public void Cleanup()
    {
        _typingSound.Dispose();
    }

    /// <summary>
    /// Небольшое окно терминала справа снизу: номер терминала, запись и субъект.
    /// </summary>
    private void DrawTerminal(DrawingHandleScreen h, float t, float alpha, Vector2 size)
    {
        if (t < TerminalAt)
            return;

        string[] lines = { _data.RecordLine, _data.TerminalLine, _data.SubjectLine };
        var right = size.X - 24f;
        var top = size.Y - 24f - lines.Length * TerminalRowHeight;

        for (var i = 0; i < lines.Length; i++)
        {
            var width = h.GetDimensions(_monoSmall, lines[i], 1f).X;
            var y = top + i * TerminalRowHeight;
            var startAt = TerminalAt + i * TerminalStep;
            var isRecord = i == RecordLineIndex;
            var color = isRecord ? RecordRed : i == 1 ? Text : Dim;
            h.DrawString(_monoSmall, new Vector2(right - width, y), Typed(lines[i], t, startAt), color.WithAlpha(alpha));

            // Красный кругляш записи слева от надписи, мигает как индикатор REC.
            if (isRecord && t >= startAt)
            {
                var on = (int) (t * 1.6f) % 2 == 0;
                h.DrawCircle(new Vector2(right - width - 14f, y + TerminalRowHeight / 2f), 5f,
                    RecordRed.WithAlpha(alpha * (on ? 1f : 0.25f)));
            }
        }
    }

    private void DrawManifest(DrawingHandleScreen h, float t, float alpha, Vector2 size, float top)
    {
        if (t < ManifestAt || _data.Columns.Length == 0)
            return;

        var margin = size.X * 0.06f;
        h.DrawLine(new Vector2(margin, top), new Vector2(size.X - margin, top), Dim.WithAlpha(0.6f * alpha));

        var colWidth = (size.X - margin * 2f) / _data.Columns.Length;

        for (var c = 0; c < _data.Columns.Length; c++)
        {
            var col = _data.Columns[c];
            var x = margin + c * colWidth;
            var headerAt = ManifestAt + c * 0.2f;
            if (t < headerAt)
                continue;

            var header = $"{col.Header.ToUpperInvariant()} // {col.Total}";
            h.DrawString(_monoSmall, new Vector2(x, top + 14f), Typed(header, t, headerAt), Dim.WithAlpha(alpha));
            h.DrawLine(new Vector2(x, top + 36f), new Vector2(x + colWidth - 20f, top + 36f), Dim.WithAlpha(0.4f * alpha));

            for (var i = 0; i < col.Entries.Length; i++)
            {
                var at = ManifestAt + 0.4f + (i * _data.Columns.Length + c) * EntryStep;
                if (t < at)
                    continue;

                var ey = top + 50f + i * 26f;
                var name = col.Entries[i].Name;
                var job = col.Entries[i].Job;
                var upperName = name.ToUpperInvariant();
                var nameWidth = h.GetDimensions(_mono, upperName, 1f).X;
                h.DrawString(_mono, new Vector2(x + 14f, ey), Typed(upperName, t, at), Text.WithAlpha(alpha));
                // Должность печатается после имени.
                var jobAt = at + upperName.Length / TypeCharsPerSecond;
                h.DrawString(_monoSmall, new Vector2(x + 14f + nameWidth + 8f, ey + 3f),
                    Typed(job.ToLowerInvariant(), t, jobAt), Dim.WithAlpha(alpha));
            }

            var hidden = col.Total - col.Entries.Length;
            var moreAt = ManifestAt + 0.4f + (col.Entries.Length * _data.Columns.Length + c) * EntryStep;
            if (hidden > 0 && t >= moreAt)
            {
                var more = Loc.GetString("shift-start-intro-more", ("count", hidden));
                h.DrawString(_monoSmall, new Vector2(x + 14f, top + 50f + col.Entries.Length * 26f),
                    Typed(more, t, moreAt), Dim.WithAlpha(alpha));
            }
        }
    }

    /// <summary>Время раунда на момент появления плюс прошедшее с начала интро — идёт вживую.</summary>
    private string FormatTime(float t)
    {
        var time = _data.RoundTime + TimeSpan.FromSeconds(t);
        return Loc.GetString("shift-start-intro-time",
            ("days", time.Days),
            ("hours", time.Hours.ToString("D2")),
            ("minutes", time.Minutes.ToString("D2")),
            ("seconds", time.Seconds.ToString("D2")));
    }

    /// <summary>Центрируем по полной строке, чтобы печатаемый текст не ездил по экрану.</summary>
    private static void DrawCentered(DrawingHandleScreen h, Font font, string full, string visible, float midX, float y, Color color)
    {
        var width = h.GetDimensions(font, full, 1f).X;
        h.DrawString(font, new Vector2(midX - width / 2f, y), visible, color);
    }
}
