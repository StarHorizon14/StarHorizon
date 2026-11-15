using Robust.Client.Audio;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.ShiftStartEffect;

/// <summary>
/// Звук печати, синтезируемый на лету, аудиофайл не нужен.
/// Щелчок — 12 мс белого шума с быстро затухающей огибающей (1 − t)², пропущенного через полосовой
/// фильтр 1800–2700 Гц. Каждый раз высота (0.85–1.25) и тембр немного разные, поэтому печать звучит «живо».
/// </summary>
public sealed class TypingSoundSynth : IDisposable
{
    private const int SampleRate = 44100;
    private const int VariantCount = 8;
    private const float ClickSeconds = 0.012f;
    private const float MinFilterHz = 1800f;
    private const float MaxFilterHz = 2700f;
    private const float MinPitch = 0.85f;
    private const float MaxPitch = 1.25f;
    private const float Gain = 0.5f;
    // Источник звука освобождаем с запасом: щелчок короткий, но на низкой высоте тянется дольше.
    private static readonly TimeSpan SourceLifetime = TimeSpan.FromMilliseconds(150);

    private readonly IAudioManager _audio;
    private readonly IGameTiming _timing;
    private readonly Random _random = new();
    private readonly AudioStream[] _variants = new AudioStream[VariantCount];
    private readonly List<(IAudioSource Source, TimeSpan Expires)> _playing = new();

    public TypingSoundSynth(IAudioManager audio, IGameTiming timing)
    {
        _audio = audio;
        _timing = timing;

        for (var i = 0; i < VariantCount; i++)
        {
            // Центры фильтров равномерно по диапазону, чтобы тембры заметно отличались.
            var hz = MinFilterHz + (MaxFilterHz - MinFilterHz) * i / (VariantCount - 1);
            _variants[i] = _audio.LoadAudioRaw(GenerateClick(hz), 1, SampleRate, "shift_start_typing");
        }
    }

    /// <summary>Один щелчок печати.</summary>
    public void Tick()
    {
        var stream = _variants[_random.Next(VariantCount)];
        var source = _audio.CreateAudioSource(stream);
        if (source == null)
            return;

        source.Global = true;
        source.Pitch = MinPitch + (float) _random.NextDouble() * (MaxPitch - MinPitch);
        source.Gain = Gain;
        source.StartPlaying();
        _playing.Add((source, _timing.RealTime + SourceLifetime));
    }

    /// <summary>Освобождает источники, которые уже доиграли.</summary>
    public void Update()
    {
        var now = _timing.RealTime;
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            if (now < _playing[i].Expires)
                continue;

            _playing[i].Source.Dispose();
            _playing.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        foreach (var (source, _) in _playing)
        {
            source.Dispose();
        }

        _playing.Clear();
    }

    private short[] GenerateClick(float centerHz)
    {
        var length = (int) (SampleRate * ClickSeconds);
        var noise = new float[length];
        for (var i = 0; i < length; i++)
        {
            var envelope = 1f - i / (float) length;
            noise[i] = ((float) _random.NextDouble() * 2f - 1f) * envelope * envelope;
        }

        // Полосовой фильтр (biquad, Q = 1), как BiquadFilterNode в браузере.
        var w0 = 2f * MathF.PI * centerHz / SampleRate;
        var alpha = MathF.Sin(w0) / 2f;
        var a0 = 1f + alpha;
        var b0 = alpha / a0;
        var b2 = -alpha / a0;
        var a1 = -2f * MathF.Cos(w0) / a0;
        var a2 = (1f - alpha) / a0;

        var filtered = new float[length];
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f, peak = 0.0001f;
        for (var i = 0; i < length; i++)
        {
            var x0 = noise[i];
            var y0 = b0 * x0 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1;
            x1 = x0;
            y2 = y1;
            y1 = y0;
            filtered[i] = y0;
            peak = MathF.Max(peak, MathF.Abs(y0));
        }

        // Нормализуем: громкость задаёт Gain источника.
        var samples = new short[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = (short) (filtered[i] / peak * 0.9f * short.MaxValue);
        }

        return samples;
    }
}
