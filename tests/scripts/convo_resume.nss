// Pauses the conversation, then resumes it (and marks the PC speaker) from delayed commands, like act_pretalk_1 in HotU.
void main()
{
    ActionPauseConversation();
    DelayCommand(0.5, SetLocalInt(GetPCSpeaker(), "delayed_speaker", 1));
    DelayCommand(0.5, ActionResumeConversation());
}
