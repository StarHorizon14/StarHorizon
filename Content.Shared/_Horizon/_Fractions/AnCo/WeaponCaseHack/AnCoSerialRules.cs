using System.Linq;
using Robust.Shared.Random;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponCaseHack;

/// <summary>
/// Условие, под которое подходит серийный номер дорожки.
/// Текст условия берётся из локализации по ключу <c>anco-hack-rule-{Id}</c>.
/// </summary>
public sealed record AnCoSerialRule(string Id, Func<string, bool> Predicate)
{
    public string LocId => $"anco-hack-rule-{Id}";
}

/// <summary>
/// Набор условий и генератор серийных номеров формата XXXX-0000 (4 буквы - 4 цифры) для взлома ящиков АнКо.
/// </summary>
public static class AnCoSerialRules
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Vowels = "AEIOU";
    private const int MaxGenerateTries = 10000;

    public static readonly IReadOnlyList<AnCoSerialRule> All = new List<AnCoSerialRule>
    {
        new("letter-repeat", s => Lt(s).GroupBy(c => c).Any(g => g.Count() > 1)),
        new("has-a", s => Lt(s).Contains('A')),
        new("has-s", s => Lt(s).Contains('S')),
        new("first-last-letter", s => Lt(s)[0] == Lt(s)[3]),
        new("letters-ascending", s => IsStrictlyAscending(Lt(s))),
        new("vowels-two", s => Lt(s).Count(c => Vowels.Contains(c)) >= 2),
        new("last-letter-vowel", s => Vowels.Contains(Lt(s)[3])),
        new("sum-even", s => Dg(s).Sum() % 2 == 0),
        new("sum-odd", s => Dg(s).Sum() % 2 == 1),
        new("sum-gt-20", s => Dg(s).Sum() > 20),
        new("sum-lt-12", s => Dg(s).Sum() < 12),
        new("digits-distinct", s => Dg(s).Distinct().Count() == 4),
        new("has-zero", s => Dg(s).Contains(0)),
        new("has-seven", s => Dg(s).Contains(7)),
        new("has-nine", s => Dg(s).Contains(9)),
        new("first-gt-last", s => Dg(s)[0] > Dg(s)[3]),
        new("first-last-ten", s => Dg(s)[0] + Dg(s)[3] == 10),
        new("adjacent-digits-equal", s =>
        {
            var d = Dg(s);
            return d[0] == d[1] || d[1] == d[2] || d[2] == d[3];
        }),
        new("all-even", s => Dg(s).All(d => d % 2 == 0)),
        new("all-odd", s => Dg(s).All(d => d % 2 == 1)),
        new("halves-equal", s =>
        {
            var d = Dg(s);
            return d[0] + d[1] == d[2] + d[3];
        }),
    };

    /// <summary>Четыре буквы серийного номера.</summary>
    private static string Lt(string serial) => serial[..4];

    /// <summary>Четыре цифры серийного номера.</summary>
    private static int[] Dg(string serial) => serial[5..9].Select(c => c - '0').ToArray();

    private static bool IsStrictlyAscending(string s)
    {
        for (var i = 0; i < s.Length - 1; i++)
        {
            if (s[i] >= s[i + 1])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Генерирует случайный серийный номер вида ABCD-1234.
    /// </summary>
    public static string GenerateSerial(IRobustRandom random)
    {
        var chars = new char[9];
        for (var i = 0; i < 4; i++)
            chars[i] = Letters[random.Next(Letters.Length)];

        chars[4] = '-';
        for (var i = 5; i < 9; i++)
            chars[i] = (char) ('0' + random.Next(10));

        return new string(chars);
    }

    /// <summary>
    /// Генерирует серийный номер, который удовлетворяет (или не удовлетворяет) условию.
    /// </summary>
    public static string GenerateSerial(IRobustRandom random, AnCoSerialRule rule, bool matches)
    {
        var serial = GenerateSerial(random);
        for (var i = 0; i < MaxGenerateTries; i++)
        {
            if (rule.Predicate(serial) == matches)
                return serial;

            serial = GenerateSerial(random);
        }

        // Недостижимо: все условия имеют вероятность >3%, а их отрицания >6%.
        throw new InvalidOperationException($"Could not generate serial for rule {rule.Id} (matches: {matches})");
    }
}
