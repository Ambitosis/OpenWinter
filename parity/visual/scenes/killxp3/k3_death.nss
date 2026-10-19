// killxp3: the victim's OnDeath logs the player's experience (is the kill's experience given before or after it?).
void main()
{
    WriteTimestampedLogEntry("VISUAL|note|kx3|ondeath|xp=" + IntToString(GetXP(GetFirstPC())));
}
