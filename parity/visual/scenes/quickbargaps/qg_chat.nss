// OnPlayerChat: what reached the server from the quickbar's text macros (text, volume), then the character is
// exported so its saved QBList can be read from the sandbox's local vault.
void main()
{
    object pc = GetPCChatSpeaker();
    WriteTimestampedLogEntry("VISUAL|note|chat|volume=" + IntToString(GetPCChatVolume()) + "|text=" + GetPCChatMessage());
    ExportSingleCharacter(pc);
    WriteTimestampedLogEntry("VISUAL|note|exported|" + GetName(pc));
}
