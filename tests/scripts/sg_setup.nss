// Save game test: leaves closures and actions pending that a loaded game must run.
// The test sets "sg_target" (a creature) on the module before running this.

void SgMark(object oHolder, string sName, int nValue, location lWhere)
{
    SetLocalInt(oHolder, sName, nValue);
    SetLocalLocation(oHolder, sName + "_loc", lWhere);
}

void main()
{
    object oMod = GetModule();
    object oTarget = GetLocalObject(oMod, "sg_target");
    int n = 7;
    string s = "seven";
    location l = GetLocation(oTarget);
    effect eHaste = EffectHaste();

    // Delayed commands capturing ints, strings, objects, a location and a user function.
    DelayCommand(30.0, SgMark(oMod, "sg_delayed", n * 6, l));
    DelayCommand(30.0, SetLocalString(oMod, "sg_delayed_str", s + "!"));
    // A delayed effect (an engine structure on the captured stack).
    DelayCommand(25.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, eHaste, oTarget, 120.0));
    // An effect with a duration, applied now.
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACIncrease(3), oTarget, 90.0);
    // An action queue: wait, then a command.
    AssignCommand(oTarget, ClearAllActions());
    AssignCommand(oTarget, ActionWait(20.0));
    AssignCommand(oTarget, ActionDoCommand(SetLocalInt(oMod, "sg_action", n + 1)));
    // An engine timer (World.After) holding a C# closure: a delayed DestroyObject.
    DestroyObject(GetLocalObject(oMod, "sg_doomed"), 33.0);
    SetLocalInt(oMod, "sg_setup_ran", 1);
}
