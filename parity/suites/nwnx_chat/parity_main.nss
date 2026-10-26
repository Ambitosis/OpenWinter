// NWNX_Chat: which messages reach the chat script, what it reads, skipping, and hearing distances.
#include "parity_inc"
#include "nwnx_chat"

void Say(object o, string s, int nVolume) { SpeakString(s, nVolume); }

void Report()
{
    object oMod = GetModule();
    PLog("chat.log", GetLocalString(oMod, "CHAT"));
    PLog("heard.log", GetLocalString(oMod, "HEARD"));
    PDone();
}

void main()
{
    object oMod = GetModule();
    PLogF("dist.talk", NWNX_Chat_GetChatHearingDistance());
    PLogF("dist.whisper", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_WHISPER));
    PLogF("dist.shout", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_SHOUT));
    PLogF("dist.dmtalk", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_DM_TALK));
    NWNX_Chat_SetChatHearingDistance(12.5);
    NWNX_Chat_SetChatHearingDistance(1.5, OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_WHISPER);
    PLogF("dist.talk.set", NWNX_Chat_GetChatHearingDistance());
    PLogF("dist.whisper.set", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_WHISPER));
    PLogF("dist.dmwhisper.set", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_DM_WHISPER));
    PLogF("dist.party.set", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_PARTY));
    NWNX_Chat_SetChatHearingDistance(9.0, OBJECT_INVALID, NWNX_CHAT_CHANNEL_DM_WHISPER);
    PLogF("dist.dmwhisper.set9", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_WHISPER));
    NWNX_Chat_SetChatHearingDistance(-1.0);
    PLogF("dist.talk.neg", NWNX_Chat_GetChatHearingDistance());
    NWNX_Chat_SetChatHearingDistance(7.0, OBJECT_INVALID, NWNX_CHAT_CHANNEL_DM_TALK);
    PLogF("dist.dmtalk.set", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_TALK));
    NWNX_Chat_SetChatHearingDistance(20.0);
    NWNX_Chat_SetChatHearingDistance(3.0, OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_WHISPER);
    int c;
    for (c = 1; c <= 30; c++)
    {
        if (c != 1 && c != 2 && c != 3 && c != 4 && c != 6 && c != 14 && c != 17 && c != 18 && c != 19 && c != 30) continue;
        string k = "mx." + IntToString(c);
        PLogF(k + ".get", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, c));
        NWNX_Chat_SetChatHearingDistance(100.0 + IntToFloat(c), OBJECT_INVALID, c);
        PLogF(k + ".after", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, c));
        PLogF(k + ".talk", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, 1));
        PLogF(k + ".whisper", NWNX_Chat_GetChatHearingDistance(OBJECT_INVALID, 3));
        NWNX_Chat_SetChatHearingDistance(20.0);
        NWNX_Chat_SetChatHearingDistance(3.0, OBJECT_INVALID, NWNX_CHAT_CHANNEL_PLAYER_WHISPER);
    }

    object oA = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(), FALSE, "SPEAKER");
    object oB = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(1.0), FALSE, "LISTENER");
    SetEventScript(oB, EVENT_SCRIPT_CREATURE_ON_DIALOGUE, "pw_heard");
    SetAILevel(oB, AI_LEVEL_NORMAL);
    SetAILevel(oA, AI_LEVEL_NORMAL);
    SetListening(oB, TRUE);
    SetListenPattern(oB, "**", 100);
    PLogF("dist.npc", NWNX_Chat_GetChatHearingDistance(oB));
    NWNX_Chat_SetChatHearingDistance(4.0, oB);
    PLogF("dist.npc.set", NWNX_Chat_GetChatHearingDistance(oB));
    PLogF("dist.npc.global", NWNX_Chat_GetChatHearingDistance());

    PLog("outside.message", NWNX_Chat_GetMessage());
    PLogI("outside.channel", NWNX_Chat_GetChannel());
    PLogI("outside.sender.invalid", NWNX_Chat_GetSender() == OBJECT_INVALID);
    PLogI("outside.sender.module", NWNX_Chat_GetSender() == GetModule());
    PLogI("outside.sender.type", GetObjectType(NWNX_Chat_GetSender()));
    PLogI("outside.target.invalid", NWNX_Chat_GetTarget() == OBJECT_INVALID);
    PLogI("outside.target.module", NWNX_Chat_GetTarget() == GetModule());

    NWNX_Chat_RegisterChatScript("pw_chat");
    AssignCommand(oA, Say(oA, "plain talk", TALKVOLUME_TALK));
    DelayCommand(0.5, AssignCommand(oA, Say(oA, "skip me", TALKVOLUME_TALK)));
    DelayCommand(1.0, AssignCommand(oA, Say(oA, "loud shout", TALKVOLUME_SHOUT)));
    DelayCommand(1.5, AssignCommand(oA, Say(oA, "quiet whisper", TALKVOLUME_WHISPER)));
    DelayCommand(2.0, AssignCommand(oA, Say(oA, "silent talk", TALKVOLUME_SILENT_TALK)));
    DelayCommand(2.5, SetLocalInt(oMod, "SEND", NWNX_Chat_SendMessage(NWNX_CHAT_CHANNEL_PLAYER_TALK, "nwnx talk", oA)));
    DelayCommand(3.0, SetLocalInt(oMod, "SEND2", NWNX_Chat_SendMessage(NWNX_CHAT_CHANNEL_SERVER_MSG, "server msg", oA)));
    DelayCommand(3.5, SetLocalInt(oMod, "SEND3", NWNX_Chat_SendMessage(NWNX_CHAT_CHANNEL_PLAYER_TELL, "a tell", oA, oB)));
    DelayCommand(4.0, SetLocalInt(oMod, "SEND4", NWNX_Chat_SendMessage(NWNX_CHAT_CHANNEL_PLAYER_TALK, "no sender", OBJECT_INVALID)));
    DelayCommand(4.5, AssignCommand(oA, ActionSpeakString("action talk")));
    DelayCommand(5.0, AssignCommand(oA, ActionSpeakString("skip me")));
    DelayCommand(6.0, PLogI("send.talk", GetLocalInt(oMod, "SEND")));
    DelayCommand(6.0, PLogI("send.server", GetLocalInt(oMod, "SEND2")));
    DelayCommand(6.0, PLogI("send.tell", GetLocalInt(oMod, "SEND3")));
    DelayCommand(6.0, PLogI("send.nosender", GetLocalInt(oMod, "SEND4")));
    DelayCommand(6.5, Report());
}
