using Content.Server.Chat.Systems;
using Content.Shared._Goobstation.SlotMachine;

namespace Content.Server._Goobstation.SlotMachine;

public sealed class PrizeSystem : SharedPrizeSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Speak(EntityUid uid, string message)
    {
        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, hideChat: false, hideLog: true, checkRadioPrefix: false);
    }
}
