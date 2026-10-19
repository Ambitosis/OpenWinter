#include "parity_inc"

void Sample(object master, string label)
{
    PLogI(label, GetIsObjectValid(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, master)));
    if (label == "familiar.0.1")
    {
        vector d = GetPosition(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, master)) - GetPosition(master);
        PLogF("familiar.dx", d.x, 0.05);
        PLogF("familiar.dy", d.y, 0.05);
    }
}

void Summon(object master)
{
    AssignCommand(master, SummonFamiliar());
    Sample(master, "familiar.immediate");
    DelayCommand(0.1, Sample(master, "familiar.0.1"));
    DelayCommand(0.1, PLogF("familiar.spawn_distance", GetDistanceBetween(master, GetAssociate(ASSOCIATE_TYPE_FAMILIAR, master)), 0.05));
    DelayCommand(0.5, Sample(master, "familiar.0.5"));
    DelayCommand(1.0, Sample(master, "familiar.1.0"));
    DelayCommand(1.5, Sample(master, "familiar.1.5"));
    DelayCommand(2.0, Sample(master, "familiar.2.0"));
    DelayCommand(2.5, Sample(master, "familiar.2.5"));
    DelayCommand(3.0, Sample(master, "familiar.3.0"));
}

void main()
{
    object master = CreateObject(OBJECT_TYPE_CREATURE, "nw_elfmage001", PCentre());
    SetAILevel(master, AI_LEVEL_NORMAL);
    DelayCommand(1.0, Summon(master));
    DelayCommand(5.0, PDone());
}
