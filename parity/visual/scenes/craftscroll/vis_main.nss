#include "visual_inc"
// The player's own spell cast at blank items in her backpack goes through the stock spell hook
// (x2_inc_spellhook -> x2_pc_craft): Scribe Scroll succeeds, Brew Potion (a feat she lacks) fails.
void Note(string s)
{
    object pc = GetFirstPC();
    string p = "";
    object i = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(i)) { p += GetResRef(i) + ":" + IntToString(GetBaseItemType(i)) + ":" + IntToString(GetGoldPieceValue(i)) + " "; i = GetNextItemInInventory(pc); }
    WriteTimestampedLogEntry("VISUAL|note|" + s + "|gold=" + IntToString(GetGold(pc)) + "|xp=" + IntToString(GetXP(pc))
        + "|scribe=" + IntToString(GetHasFeat(FEAT_SCRIBE_SCROLL, pc)) + "|brew=" + IntToString(GetHasFeat(FEAT_BREW_POTION, pc)) + "|pack=" + p);
}
void Cast(string tag, int spell)
{
    object pc = GetFirstPC();
    AssignCommand(pc, ActionCastSpellAtObject(spell, GetItemPossessedBy(pc, tag), METAMAGIC_ANY, TRUE));
}
void Give(string r) { CreateItemOnObject(r, GetFirstPC()); }
void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetPlotFlag(pc, TRUE);
    WriteTimestampedLogEntry("VISUAL|note|character-fixture|" + GetName(pc) + "|pc=" + IntToString(GetIsPC(pc)));
    object i = GetFirstItemInInventory(pc);
    while (GetIsObjectValid(i)) { DestroyObject(i); i = GetNextItemInInventory(pc); }
    SetXP(pc, 1000);
    GiveGoldToCreature(pc, 1000);
    DelayCommand(1.0, VCamera(pc, 90.0, 10.0, 60.0));
    DelayCommand(1.0, Give("x2_it_cfm_bscrl"));
    DelayCommand(1.0, Give("x2_it_cfm_pbottl"));
    DelayCommand(3.0, Note("before"));
    DelayCommand(3.5, Cast("x2_it_cfm_bscrl", SPELL_MAGIC_MISSILE));
    DelayCommand(5.0, VShot("a_scribing"));
    DelayCommand(9.0, Note("scribed")); DelayCommand(9.0, VShot("b_scribed"));
    DelayCommand(10.0, Cast("x2_it_cfm_pbottl", SPELL_MAGE_ARMOR));
    DelayCommand(11.5, VShot("c_brewing"));
    DelayCommand(15.0, Note("brew")); DelayCommand(15.0, VShot("d_brew"));
    DelayCommand(17.0, VDone());
}
