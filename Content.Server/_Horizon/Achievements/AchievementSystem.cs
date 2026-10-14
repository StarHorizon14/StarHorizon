using System.Linq;
using System.Text.Json;
using Content.Shared.Examine;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Horizon.Achievements;

/// <summary>
/// Admin-granted achievements, stored per player account and shown when examining any character they play.
/// Saved to a JSON file in the server's data directory so they persist between rounds without a database migration.
/// </summary>
public sealed class AchievementSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public const int MaxNameLength = 100;

    private static readonly ResPath SavePath = new("/achievements.json");

    private Dictionary<Guid, List<string>> _achievements = new();
    private ISawmill _sawmill = default!;

    /// <summary>
    /// Don't overwrite a file we couldn't read, or every stored achievement would be lost.
    /// </summary>
    private bool _loadFailed;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("achievements");
        Load();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<AchievementHolderComponent, ExaminedEvent>(OnExamined);
    }

    public IReadOnlyList<string> GetAchievements(NetUserId user)
    {
        return _achievements.TryGetValue(user.UserId, out var list) ? list : Array.Empty<string>();
    }

    /// <returns>False if the player already has an achievement with this name.</returns>
    public bool AddAchievement(NetUserId user, string name)
    {
        if (!_achievements.TryGetValue(user.UserId, out var list))
            _achievements[user.UserId] = list = new List<string>();

        if (list.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            return false;

        list.Add(name);
        Save();
        EnsureHolder(user);
        return true;
    }

    /// <returns>False if the player has no achievement with this name.</returns>
    public bool RemoveAchievement(NetUserId user, string name)
    {
        if (!_achievements.TryGetValue(user.UserId, out var list)
            || list.RemoveAll(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)) == 0)
            return false;

        if (list.Count == 0)
            _achievements.Remove(user.UserId);

        Save();
        return true;
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var holder = EnsureComp<AchievementHolderComponent>(args.Entity);
        holder.UserId = args.Player.UserId;
    }

    /// <summary>
    /// Make sure the player's current body shows a freshly granted achievement right away.
    /// </summary>
    private void EnsureHolder(NetUserId user)
    {
        if (_players.TryGetSessionById(user, out var session) && session.AttachedEntity is { } entity)
            EnsureComp<AchievementHolderComponent>(entity).UserId = user;
    }

    private void OnExamined(Entity<AchievementHolderComponent> ent, ref ExaminedEvent args)
    {
        var list = GetAchievements(ent.Comp.UserId);
        if (list.Count == 0)
            return;

        var names = string.Join(", ", list.Select(FormattedMessage.EscapeText));
        args.PushMarkup(Loc.GetString("achievement-examine", ("achievements", names)), -1);
    }

    private void Load()
    {
        try
        {
            if (!_res.UserData.Exists(SavePath))
                return;

            using var reader = _res.UserData.OpenText(SavePath);
            _achievements = JsonSerializer.Deserialize<Dictionary<Guid, List<string>>>(reader.ReadToEnd()) ?? new();
            _sawmill.Info($"Loaded achievements for {_achievements.Count} players");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to load achievements, saving is disabled until the file is fixed: {e}");
            _achievements = new();
            _loadFailed = true;
        }
    }

    private void Save()
    {
        if (_loadFailed)
            return;

        try
        {
            using var writer = _res.UserData.OpenWriteText(SavePath);
            writer.Write(JsonSerializer.Serialize(_achievements));
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to save achievements: {e}");
        }
    }
}
