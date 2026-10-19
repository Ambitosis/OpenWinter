namespace OpenWinter.Content;

/// <summary>BioWare sound-set slots and original QuickChat keyboard paths.</summary>
public static class QuickChat
{
    public sealed record Entry(char Key, string Label, int Slot)
    {
        public uint StringRef => Slot switch { <= 10 => (uint)(4711 + Slot - (Slot >= 4 ? 2 : 0)), >= 22 and <= 25 => (uint)(4731 + Slot - 22), >= 26 and <= 31 => (uint)(4741 + Slot - 26), >= 34 and <= 40 => (uint)(4751 + Slot - 34), >= 41 => (uint)(4761 + Slot - 41), _ => uint.MaxValue };
    }
    public sealed record Group(char Key, string Label, Entry[] Entries)
    {
        public uint StringRef => Key switch { 'W' => 4710, 'E' => 4730, 'D' => 4740, 'S' => 4750, _ => 4760 };
    }
    public static readonly Group[] Groups =
    [
        new('W', "Combat", [new('E',"Attack",0),new('R',"Battle cry",1),new('D',"Heal me",4),new('W',"Help",5),new('A',"Enemies sighted",6),new('S',"Flee",7),new('T',"Taunt",8),new('F',"Guard me",9),new('X',"Hold",10)]),
        new('E', "Exploration", [new('E',"Follow me",22),new('W',"Look here",23),new('D',"Group party",24),new('S',"Move over",25)]),
        new('D', "Tasks", [new('W',"Pick lock",26),new('E',"Search",27),new('S',"Go stealthy",28),new('C',"Can do",29),new('X',"Cannot do",30),new('A',"Task complete",31)]),
        new('S', "Social", [new('S',"Hello",34),new('D',"Yes",35),new('W',"No",36),new('E',"Stop",37),new('C',"Rest",38),new('X',"Bored",39),new('A',"Goodbye",40)]),
        new('X', "Feelings", [new('X',"Thank you",41),new('W',"Laugh",42),new('C',"Cuss",43),new('D',"Cheer",44),new('S',"Something to say",45),new('A',"Good idea",46),new('Z',"Bad idea",47),new('E',"Threaten",48)])
    ];
    public static bool IsSelectable(int slot) => slot is 2 or 3 || Groups.Any(g => g.Entries.Any(e => e.Slot == slot));
    public static int AssociateCommand(int slot) => slot switch { 0 => -3, 4 => -4, 9 => -7, 10 => -2, 22 => -5, _ => 0 };
}
