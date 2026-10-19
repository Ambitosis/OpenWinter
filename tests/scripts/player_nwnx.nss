// Player package: native NWNX stack and calls.
void main()
{
    object m = GetModule();
    object c = GetLocalObject(m, "creature");
    SetLocalInt(m, "available", NWNXGetIsAvailable());

    NWNXPushInt(1001);
    NWNXPushObject(c);
    NWNXCall("NWNX_Creature", "AddFeat");

    NWNXPushInt(1001);
    NWNXPushObject(c);
    NWNXCall("NWNX_Creature", "GetKnowsFeat");
    SetLocalInt(m, "knows", NWNXPopInt());

    NWNXPushInt(7);
    NWNXPushInt(4);
    NWNXPushObject(c);
    NWNXCall("NWNX_Creature", "SetSkillRank");

    NWNXPushString("<cabc>red</c> text");
    NWNXCall("NWNX_Util", "StripColors");
    SetLocalString(m, "stripped", NWNXPopString());

    // Unknown function: generic Set/Get store.
    NWNXPushFloat(2.5);
    NWNXPushObject(c);
    NWNXCall("NWNX_Creature", "SetSomethingNew");
    NWNXPushObject(c);
    NWNXCall("NWNX_Creature", "GetSomethingNew");
    SetLocalFloat(m, "generic", NWNXPopFloat());

    // Raw stack round trip: values come back in LIFO order.
    NWNXPushInt(5);
    NWNXPushString("five");
    NWNXPushFloat(5.5);
    NWNXPushVector(Vector(1.0, 2.0, 3.0));
    NWNXPushJson(JsonParse("{\"x\":1}"));
    string s = JsonDump(NWNXPopJson());
    s = s + "|" + FloatToString(NWNXPopVector().y, 0, 1);
    s = s + "|" + FloatToString(NWNXPopFloat(), 0, 1);
    s = s + "|" + NWNXPopString();
    s = s + "|" + IntToString(NWNXPopInt());
    s = s + "|" + IntToString(NWNXPopInt());
    SetLocalString(m, "stack", s);
}
