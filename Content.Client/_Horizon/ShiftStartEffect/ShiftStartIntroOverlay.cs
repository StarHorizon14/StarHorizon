using System.Numerics;
using Content.Shared._Horizon.ShiftStartEffect;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.ShiftStartEffect;

/// <summary>
/// Полноэкранное интро при появлении: зелёный CRT-экран, название станции, строки брифинга
/// и список экипажа по отделам, после чего экран затухает в игру.
/// </summary>
public sealed class ShiftStartIntroOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    // Таймлайн, секунды.
    private const float FadeIn = 0.8f;
    private const float CompanyAt = 1.0f;
    private const int TimeLineIndex = 1;
    private const int ThreatLineIndex = 2;
    private const float TerminalAt = 0.4f;
    private const float TerminalRowHeight = 16f;
    private const float LogoScale = 4f;
    private const float TitleAt = 2.0f;
    private const float TitleCharTime = 0.09f;
    private const float LinesAt = 3.6f;
    private const float LineStep = 0.6f;
    private const float ManifestAt = 6.2f;
    private const float EntryStep = 0.3f;
    private const float FadeOutAt = 10.4f;
    private const float Total = 11.6f;

    private static readonly Color Text = Color.FromHex("#bfffd0");
    private static readonly Color Dim = Color.FromHex("#4fa06a");

    /// <summary>Интро доиграло, система может убрать оверлей.</summary>
    public bool Finished;

    private readonly ShiftStartIntroEvent _data;
    private readonly TimeSpan _start;
    private readonly ShaderInstance _background;
    private readonly Texture _logo;
    private readonly Font _pixel;
    private readonly Font _mono;
    private readonly Font _monoSmall;

    public ShiftStartIntroOverlay(ShiftStartIntroEvent data)
    {
        IoCManager.InjectDependencies(this);
        _data = data;
        _start = _timing.RealTime;

        _background = _prototype.Index<ShaderPrototype>("ShiftStartBackground").InstanceUnique();
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

        var alpha = Math.Clamp(t / FadeIn, 0f, 1f) * Math.Clamp((Total - t) / (Total - FadeOutAt), 0f, 1f);
        var h = args.ScreenHandle;
        var size = (Vector2) args.ViewportBounds.Size;
        var mid = size.X / 2f;

        // Фон: искажающийся тёмно-зелёный экран с движущимися полосками и помехами.
        _background.SetParameter("Alpha", 0.92f * alpha);
        h.UseShader(_background);
        h.DrawRect(new UIBox2(Vector2.Zero, size), Color.White);
        h.UseShader(null);

        DrawTerminal(h, t, alpha, size);

        var logoSize = (Vector2) _logo.Size * LogoScale;
        var logoTop = size.Y * 0.04f;
        // Заголовок идёт сразу под логотипом.
        var y0 = logoTop + logoSize.Y - 40f;

        if (t > CompanyAt)
        {
            h.DrawTextureRect(_logo,
                UIBox2.FromDimensions(new Vector2(mid - logoSize.X / 2f, logoTop), logoSize),
                Color.White.WithAlpha(alpha * Fade(t, CompanyAt)));
        }

        if (t > TitleAt)
        {
            var chars = Math.Min(_data.Title.Length, (int) ((t - TitleAt) / TitleCharTime) + 1);
            DrawCentered(h, _pixel, _data.Title.ToUpperInvariant()[..chars], mid, y0 + 60f, Color.White.WithAlpha(alpha));
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
            DrawCentered(h, _mono, text, mid, ly + i * 30f, color.WithAlpha(alpha * Fade(t, at)));
        }

        DrawManifest(h, t, alpha, size, ly + _data.Lines.Length * 30f + 24f);
    }

    /// <summary>
    /// Небольшое окно терминала справа снизу: номер терминала, запись и субъект.
    /// </summary>
    private void DrawTerminal(DrawingHandleScreen h, float t, float alpha, Vector2 size)
    {
        if (t < TerminalAt)
            return;

        var a = alpha * Fade(t, TerminalAt);
        string[] lines = { _data.TerminalLine, _data.RecordLine, _data.SubjectLine };
        var right = size.X - 24f;
        var top = size.Y - 24f - lines.Length * TerminalRowHeight;

        for (var i = 0; i < lines.Length; i++)
        {
            var width = h.GetDimensions(_monoSmall, lines[i], 1f).X;
            h.DrawString(_monoSmall, new Vector2(right - width, top + i * TerminalRowHeight), lines[i],
                (i == 0 ? Text : Dim).WithAlpha(a));
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

            h.DrawString(_monoSmall, new Vector2(x, top + 14f), $"{col.Header.ToUpperInvariant()} // {col.Total}", Dim.WithAlpha(alpha));
            h.DrawLine(new Vector2(x, top + 36f), new Vector2(x + colWidth - 20f, top + 36f), Dim.WithAlpha(0.4f * alpha));

            for (var i = 0; i < col.Entries.Length; i++)
            {
                var at = ManifestAt + 0.4f + (i * _data.Columns.Length + c) * EntryStep;
                if (t < at)
                    continue;

                var ey = top + 50f + i * 26f;
                var name = col.Entries[i].Name;
                var job = col.Entries[i].Job;
                var a = alpha * Fade(t, at);
                var nameWidth = h.GetDimensions(_mono, name.ToUpperInvariant(), 1f).X;
                h.DrawString(_mono, new Vector2(x + 14f, ey), name.ToUpperInvariant(), Text.WithAlpha(a));
                h.DrawString(_monoSmall, new Vector2(x + 14f + nameWidth + 8f, ey + 3f), job.ToLowerInvariant(), Dim.WithAlpha(a));
            }

            var hidden = col.Total - col.Entries.Length;
            var moreAt = ManifestAt + 0.4f + (col.Entries.Length * _data.Columns.Length + c) * EntryStep;
            if (hidden > 0 && t >= moreAt)
            {
                h.DrawString(_monoSmall, new Vector2(x + 14f, top + 50f + col.Entries.Length * 26f),
                    Loc.GetString("shift-start-intro-more", ("count", hidden)), Dim.WithAlpha(alpha * Fade(t, moreAt)));
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

    private static float Fade(float t, float at) => Math.Clamp((t - at) / 0.25f, 0f, 1f);

    private static void DrawCentered(DrawingHandleScreen h, Font font, string text, float midX, float y, Color color)
    {
        var width = h.GetDimensions(font, text, 1f).X;
        h.DrawString(font, new Vector2(midX - width / 2f, y), text, color);
    }
}
