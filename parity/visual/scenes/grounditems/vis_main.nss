// Items on the ground: which model each kind of item shows, how it lies, and whether its facing follows
// the item's location or its dropper. Groups of ten items (two rows of five, 2 m apart, made with
// CreateObject at facing 0 unless noted), each shot from straight above and from low down. Column order is
// left to right, back row (north) first. Every item logs base item, model parts, facing and height.
#include "visual_inc"

void Note(object o, string what)
{
    vector v = GetPosition(o);
    WriteTimestampedLogEntry("VISUAL|note|item|" + what + "|valid=" + IntToString(GetIsObjectValid(o)) + "|base=" + IntToString(GetBaseItemType(o))
        + "|parts=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 0)) + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 1))
        + "," + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_WEAPON_MODEL, 2)) + "|simple=" + IntToString(GetItemAppearance(o, ITEM_APPR_TYPE_SIMPLE_MODEL, 0))
        + "|facing=" + FloatToString(GetFacing(o), 0, 1) + "|pos=" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
}

location At(float cx, float cy, int i, float facing)
{
    float x = cx - 2.0 * GetLocalFloat(GetModule(), "gap") + IntToFloat(i % 5) * GetLocalFloat(GetModule(), "gap");
    float y = cy + (i < 5 ? 0.9 : -0.9);
    return Location(GetArea(GetFirstPC()), Vector(x, y, 0.0), facing);
}

void Put(string resref, float cx, float cy, int i, float facing = 0.0)
{
    object o = CreateObject(OBJECT_TYPE_ITEM, resref, At(cx, cy, i, facing));
    Note(o, IntToString(i) + "|" + resref);
}

// A copy with one appearance value changed (made in the player's pack, then copied to the ground).
void PutModified(string resref, int type, int index, int value, float cx, float cy, int i)
{
    object pc = GetFirstPC();
    object src = CreateItemOnObject(resref, pc);
    object mod = CopyItemAndModify(src, type, index, value, TRUE);
    object o = CopyObject(GetIsObjectValid(mod) ? mod : src, At(cx, cy, i, 0.0));
    Note(o, IntToString(i) + "|" + resref + "|modified=" + IntToString(type) + "/" + IntToString(index) + "/" + IntToString(value) + "|modok=" + IntToString(GetIsObjectValid(mod)));
    DestroyObject(src); DestroyObject(mod);
}

void Group(string name, float cx, float cy, float t)
{
    object pc = GetFirstPC();
    DelayCommand(t, VMovePC(pc, cx, cy, 0.0, 90.0));
    DelayCommand(t + 0.6, VCamera(pc, 90.0, 6.0, 1.0));
    DelayCommand(t + 2.5, VShot(name + "_top"));
    DelayCommand(t + 3.5, VCamera(pc, 90.0, 6.0, 70.0));
    DelayCommand(t + 5.5, VShot(name + "_low"));
}

void Drop(float pcFacing, float x, float y)
{
    object pc = GetFirstPC();
    object o = CreateItemOnObject("nw_wswls001", pc);
    SetCutsceneMode(pc, FALSE, FALSE);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, JumpToLocation(Location(GetArea(pc), Vector(x, y, 0.0), pcFacing)));
    AssignCommand(pc, ActionDoCommand(SetFacing(pcFacing)));
    AssignCommand(pc, ActionWait(0.5));
    AssignCommand(pc, ActionPutDownItem(o));
    SetLocalObject(pc, "drop" + IntToString(FloatToInt(pcFacing)), o);
}

void DropNote(float pcFacing)
{
    object pc = GetFirstPC();
    object o = GetLocalObject(pc, "drop" + IntToString(FloatToInt(pcFacing)));
    SetCutsceneMode(pc, TRUE, FALSE);
    WriteTimestampedLogEntry("VISUAL|note|drop|pcFacing=" + FloatToString(GetFacing(pc), 0, 1) + "|onGround=" + IntToString(!GetIsObjectValid(GetItemPossessor(o))));
    Note(o, "drop" + IntToString(FloatToInt(pcFacing)));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc) || GetLocalInt(GetModule(), "started")) return;
    SetLocalInt(GetModule(), "started", 1);
    VBegin(pc); VHidePC(pc);
    SetLocalFloat(GetModule(), "gap", 2.0);
    // a: part-built weapons (baseitems ModelType 2).
    Put("nw_wswls001", 10.0, 8.0, 0); Put("nw_waxbt001", 10.0, 8.0, 1); Put("nw_wbwln001", 10.0, 8.0, 2);
    Put("nw_wbwxl001", 10.0, 8.0, 3); Put("nw_wbwxh001", 10.0, 8.0, 4); Put("nw_wswgs001", 10.0, 8.0, 5);
    Put("nw_wswdg001", 10.0, 8.0, 6); Put("nw_wblms001", 10.0, 8.0, 7); Put("nw_wplhb001", 10.0, 8.0, 8);
    Put("nw_wdbqs001", 10.0, 8.0, 9);
    // b: one-piece weapons (ModelType 0), throwing weapons, wand, shields, torch, arrows.
    Put("nw_wbwsl001", 30.0, 8.0, 0); Put("nw_wthsh001", 30.0, 8.0, 1); Put("nw_wthdt001", 30.0, 8.0, 2);
    Put("nw_wthax001", 30.0, 8.0, 3); Put("nw_wmgwn011", 30.0, 8.0, 4); Put("nw_ashsw001", 30.0, 8.0, 5);
    Put("nw_ashlw001", 30.0, 8.0, 6); Put("nw_ashto001", 30.0, 8.0, 7); Put("nw_it_torch001", 30.0, 8.0, 8);
    Put("nw_wamar001", 30.0, 8.0, 9);
    // c: worn items.
    Put("nw_aarcl001", 10.0, 18.0, 0); Put("nw_mcloth001", 10.0, 18.0, 1); Put("nw_arhe001", 10.0, 18.0, 2);
    Put("x2_it_mcloak001", 10.0, 18.0, 3); Put("nw_it_mboots001", 10.0, 18.0, 4); Put("nw_it_mglove001", 10.0, 18.0, 5);
    Put("nw_it_mbelt001", 10.0, 18.0, 6); Put("nw_it_mneck001", 10.0, 18.0, 7); Put("nw_it_mring001", 10.0, 18.0, 8);
    Put("nw_it_mbracer001", 10.0, 18.0, 9);
    // d: everything else.
    Put("nw_it_mpotion001", 30.0, 18.0, 0); Put("nw_it_sparscr101", 30.0, 18.0, 1); Put("nw_it_gem001", 30.0, 18.0, 2);
    Put("nw_it_gold001", 30.0, 18.0, 3); Put("nw_wambo001", 30.0, 18.0, 4); Put("nw_wambu001", 30.0, 18.0, 5);
    Put("nw_it_mmidmisc01", 30.0, 18.0, 6); Put("nw_it_medkit001", 30.0, 18.0, 7); Put("nw_it_picks001", 30.0, 18.0, 8);
    Put("nw_it_trap001", 30.0, 18.0, 9);
    // e: the same kinds with other parts and colours. Back row: longsword bottom model 3, shield 41, misc 151,
    // helmet 20, armour torso 30. Front row: helmet metal and cloak cloth colour 40, armour leather colour 40,
    // longsword top colour 4, cloak model 1.
    DelayCommand(0.3, PutModified("nw_wswls001", ITEM_APPR_TYPE_WEAPON_MODEL, ITEM_APPR_WEAPON_MODEL_BOTTOM, 3, 10.0, 28.0, 0));
    DelayCommand(0.4, PutModified("nw_ashsw001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 41, 10.0, 28.0, 1));
    DelayCommand(0.5, PutModified("nw_it_mmidmisc01", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 151, 10.0, 28.0, 2));
    DelayCommand(0.6, PutModified("nw_arhe001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 20, 10.0, 28.0, 3));
    DelayCommand(0.7, PutModified("nw_aarcl001", ITEM_APPR_TYPE_ARMOR_MODEL, ITEM_APPR_ARMOR_MODEL_TORSO, 30, 10.0, 28.0, 4));
    DelayCommand(0.8, PutModified("nw_arhe001", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_METAL1, 40, 10.0, 28.0, 5));
    DelayCommand(0.9, PutModified("x2_it_mcloak001", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_CLOTH1, 40, 10.0, 28.0, 6));
    DelayCommand(1.0, PutModified("nw_aarcl001", ITEM_APPR_TYPE_ARMOR_COLOR, ITEM_APPR_ARMOR_COLOR_LEATHER1, 40, 10.0, 28.0, 7));
    DelayCommand(1.1, PutModified("nw_wswls001", ITEM_APPR_TYPE_WEAPON_COLOR, ITEM_APPR_WEAPON_COLOR_TOP, 4, 10.0, 28.0, 8));
    DelayCommand(1.2, PutModified("x2_it_mcloak001", ITEM_APPR_TYPE_SIMPLE_MODEL, 0, 1, 10.0, 28.0, 9));
    // f: facing. Back row: longsword at 0, 45, 90, 180, 270. Front row: crossbow 0 and 90, potion 90, shield 90, amulet 90.
    Put("nw_wswls001", 30.0, 28.0, 0, 0.0); Put("nw_wswls001", 30.0, 28.0, 1, 45.0); Put("nw_wswls001", 30.0, 28.0, 2, 90.0);
    Put("nw_wswls001", 30.0, 28.0, 3, 180.0); Put("nw_wswls001", 30.0, 28.0, 4, 270.0);
    Put("nw_wbwxl001", 30.0, 28.0, 5, 0.0); Put("nw_wbwxl001", 30.0, 28.0, 6, 90.0); Put("nw_it_mpotion001", 30.0, 28.0, 7, 90.0);
    Put("nw_ashsw001", 30.0, 28.0, 8, 90.0); Put("nw_it_mneck001", 30.0, 28.0, 9, 90.0);
    Group("a_weapons", 10.0, 8.0, 2.0);
    Group("b_simple", 30.0, 8.0, 9.0);
    Group("c_worn", 10.0, 18.0, 16.0);
    Group("d_misc", 30.0, 18.0, 23.0);
    Group("e_parts", 10.0, 28.0, 30.0);
    Group("f_facing", 30.0, 28.0, 37.0);
    // g: the player drops a longsword facing north (90), then another facing east (0), 4 m apart.
    DelayCommand(44.0, Drop(90.0, 18.0, 35.0));
    DelayCommand(48.0, Drop(0.0, 22.0, 35.0));
    DelayCommand(52.0, DropNote(90.0)); DelayCommand(52.1, DropNote(0.0));
    DelayCommand(52.5, VMovePC(pc, 20.0, 33.0, 0.0, 90.0));
    DelayCommand(53.0, VCamera(pc, 90.0, 7.0, 1.0));
    DelayCommand(55.0, VShot("g_drop_top"));
    DelayCommand(56.0, VCamera(pc, 90.0, 5.0, 72.0));
    DelayCommand(58.0, VShot("g_drop_low"));
    // i: close-ups 1 m apart. Back row: small, large and tower shield, armour, clothing. Front row: helmet,
    // cloak, gold, potion, throwing axe (all facing 0).
    DelayCommand(58.6, SetLocalFloat(GetModule(), "gap", 1.0));
    DelayCommand(58.7, Put("nw_ashsw001", 20.0, 14.0, 0)); DelayCommand(58.7, Put("nw_ashlw001", 20.0, 14.0, 1));
    DelayCommand(58.7, Put("nw_ashto001", 20.0, 14.0, 2)); DelayCommand(58.7, Put("nw_aarcl001", 20.0, 14.0, 3));
    DelayCommand(58.7, Put("nw_mcloth001", 20.0, 14.0, 4)); DelayCommand(58.7, Put("nw_arhe001", 20.0, 14.0, 5));
    DelayCommand(58.7, Put("x2_it_mcloak001", 20.0, 14.0, 6)); DelayCommand(58.7, Put("nw_it_gold001", 20.0, 14.0, 7));
    DelayCommand(58.7, Put("nw_it_mpotion001", 20.0, 14.0, 8)); DelayCommand(58.7, Put("nw_wthax001", 20.0, 14.0, 9));
    DelayCommand(64.0, VMovePC(pc, 20.0, 14.0, 0.0, 90.0));
    DelayCommand(64.6, VCamera(pc, 90.0, 3.2, 1.0));
    DelayCommand(66.5, VShot("i_close_top"));
    DelayCommand(67.0, VCamera(pc, 90.0, 3.2, 65.0));
    DelayCommand(69.0, VShot("i_close_low"));
    // h: a level view of the facing group, for height above the ground.
    DelayCommand(70.0, VMovePC(pc, 30.0, 28.0, 0.0, 90.0));
    DelayCommand(70.6, VCamera(pc, 90.0, 4.5, 88.0));
    DelayCommand(72.5, VShot("h_level"));
    DelayCommand(74.0, VDone());
}
