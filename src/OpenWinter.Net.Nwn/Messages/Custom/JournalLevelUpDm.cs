using OpenWinter.Net.Nwn.Wire;
using ObjectType = OpenWinter.Net.Nwn.Messages.NwnExternalTypes.ObjectType;

namespace OpenWinter.Net.Nwn.Messages;

public static partial class JournalMessages
{
    /// <summary>One quest's change: its flags say which fields follow (all of them in a full update).</summary>
    public sealed class JournalFullUpdateEntry : INwnStruct
    {
        [NwnField("flags")]
        public JournalUpdateFlag Flags { get; set; }
        [NwnField("plot_id")]
        public string PlotId { get; set; } = "";
        [NwnField("name")]
        public NwnLocString Name { get; set; } = NwnLocString.FromText("");
        [NwnField("text")]
        public NwnLocString Text { get; set; } = NwnLocString.FromText("");
        [NwnField("calendar_day")]
        public uint CalendarDay { get; set; }
        [NwnField("time_of_day")]
        public uint TimeOfDay { get; set; }
        [NwnField("state")]
        public int State { get; set; }
        [NwnField("priority")]
        public uint Priority { get; set; }
        [NwnField("picture_index")]
        public ushort PictureIndex { get; set; }
        [NwnField("quest_completed")]
        public bool QuestCompleted { get; set; }

        public void Read(NwnReader r) => Read(r, fullUpdate: false);
        public void Write(NwnWriter w) => Write(w, fullUpdate: false);

        bool Has(JournalUpdateFlag flag, bool full) => full || (Flags & flag) != 0;

        internal void Read(NwnReader r, bool fullUpdate)
        {
            Flags = (JournalUpdateFlag)r.ReadWord();
            PlotId = r.ReadString();
            if ((Flags & JournalUpdateFlag.Remove) != 0) return;
            if (Has(JournalUpdateFlag.Szname, fullUpdate)) Name = r.ReadLocString();
            if (Has(JournalUpdateFlag.Sztext, fullUpdate)) Text = r.ReadLocString();
            if (Has(JournalUpdateFlag.Ncalendarday, fullUpdate)) CalendarDay = r.ReadDword();
            if (Has(JournalUpdateFlag.Ntimeofday, fullUpdate)) TimeOfDay = r.ReadDword();
            if (Has(JournalUpdateFlag.Nstate, fullUpdate)) State = r.ReadInt();
            if (Has(JournalUpdateFlag.Npriority, fullUpdate)) Priority = r.ReadDword();
            if (Has(JournalUpdateFlag.Npictureindex, fullUpdate)) PictureIndex = r.ReadWord();
            if (Has(JournalUpdateFlag.Bquestcompleted, fullUpdate)) QuestCompleted = r.ReadBool();
        }

        internal void Write(NwnWriter w, bool fullUpdate)
        {
            w.WriteWord((ushort)Flags);
            w.WriteString(PlotId);
            if ((Flags & JournalUpdateFlag.Remove) != 0) return;
            if (Has(JournalUpdateFlag.Szname, fullUpdate)) w.WriteLocString(Name);
            if (Has(JournalUpdateFlag.Sztext, fullUpdate)) w.WriteLocString(Text);
            if (Has(JournalUpdateFlag.Ncalendarday, fullUpdate)) w.WriteDword(CalendarDay);
            if (Has(JournalUpdateFlag.Ntimeofday, fullUpdate)) w.WriteDword(TimeOfDay);
            if (Has(JournalUpdateFlag.Nstate, fullUpdate)) w.WriteInt(State);
            if (Has(JournalUpdateFlag.Npriority, fullUpdate)) w.WriteDword(Priority);
            if (Has(JournalUpdateFlag.Npictureindex, fullUpdate)) w.WriteWord(PictureIndex);
            if (Has(JournalUpdateFlag.Bquestcompleted, fullUpdate)) w.WriteBool(QuestCompleted);
        }
    }

    /// <summary>
    /// Journal changes. Without changes to follow, an optional trailer clears the journal; the entries a trailer may
    /// carry are read and dropped.
    /// </summary>
    public sealed class JournalFullUpdate : NwnMessage
    {
        public const byte MajorId = 0x1C;
        public const byte MinorId = 0x09;
        public override byte Major => MajorId;
        public override byte Minor => MinorId;
        public override NwnDirection Direction => NwnDirection.ServerToClient;

        [NwnField("changes_to_follow")]
        public bool ChangesToFollow { get; set; }
        [NwnField("full_update")]
        public bool FullUpdate { get; set; }
        [NwnField("entries")]
        public List<JournalFullUpdateEntry> Entries { get; set; } = new();
        [NwnField("clear_old_entries")]
        public bool ClearOldEntries { get; set; }

        public override void Read(NwnReader r)
        {
            Entries = new();
            ChangesToFollow = r.ReadBool();
            if (!ChangesToFollow)
            {
                FullUpdate = false;
                ClearOldEntries = false;
                if (r.AtEnd) return;
                FullUpdate = r.ReadBool();
                ClearOldEntries = FullUpdate;
                var dropped = r.ReadInt();
                for (int i = 0; i < dropped; i++) new JournalFullUpdateEntry().Read(r, fullUpdate: false);
                return;
            }
            FullUpdate = r.ReadBool();
            var count = r.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var entry = new JournalFullUpdateEntry();
                entry.Read(r, FullUpdate);
                Entries.Add(entry);
            }
        }

        public override void Write(NwnWriter w)
        {
            w.WriteBool(ChangesToFollow);
            if (!ChangesToFollow)
            {
                if (!ClearOldEntries) return;
                w.WriteBool(true);
                w.WriteInt(0);
                return;
            }
            w.WriteBool(FullUpdate);
            w.WriteInt(Entries.Count);
            foreach (var entry in Entries) entry.Write(w, FullUpdate);
        }
    }
}

public static partial class LevelupMessages
{
    public const int SpellLevels = 10;

    /// <summary>
    /// The player's level-up choices. Whether an ability point follows the class isn't on the wire; reading tries both
    /// layouts and keeps the one that uses the whole message.
    /// </summary>
    public sealed class LevelUpNull : NwnMessage
    {
        public const byte MajorId = 0x1D;
        public const byte MinorId = 0x00;
        public override byte Major => MajorId;
        public override byte Minor => MinorId;
        public override NwnDirection Direction => NwnDirection.ClientToServer;

        [NwnField("class_leveled_up_in")]
        public byte ClassLeveledUpIn { get; set; }
        [NwnField("has_ability_gain")]
        public bool HasAbilityGain { get; set; }
        [NwnField("ability_gain")]
        public byte AbilityGain { get; set; }
        [NwnField("hit_die")]
        public byte HitDie { get; set; }
        [NwnField("skill_changes")]
        public LevelUpSkillChanges SkillChanges { get; set; } = new();
        [NwnField("skill_points_remaining")]
        public ushort SkillPointsRemaining { get; set; }
        [NwnField("feats")]
        public List<ushort> Feats { get; set; } = new();
        [NwnField("spell_changes")]
        public List<LevelUpSpellLevelChanges> SpellChanges { get; set; } = new();
        [NwnField("familiar_creature_type")]
        public int FamiliarCreatureType { get; set; }
        [NwnField("familiar_name")]
        public string FamiliarName { get; set; } = "";
        [NwnField("animal_companion_creature_type")]
        public int AnimalCompanionCreatureType { get; set; }
        [NwnField("animal_companion_name")]
        public string AnimalCompanionName { get; set; } = "";
        [NwnField("domain1")]
        public byte Domain1 { get; set; }
        [NwnField("domain2")]
        public byte Domain2 { get; set; }
        [NwnField("school")]
        public byte School { get; set; }

        static bool PicksDomains(INwnContext c, int cls) => c.TwoDA("classes", cls, "PickDomains") == "1";
        static bool PicksSchool(INwnContext c, int cls) => c.TwoDA("classes", cls, "PickSchool") == "1";

        public override void Read(NwnReader r)
        {
            var errors = new List<string>();
            foreach (var abilityGain in new[] { false, true })
            {
                var attempt = new NwnWsReader(SliceFrom(r), r.Context);
                try
                {
                    ReadVariant(attempt, abilityGain);
                    if (attempt.AtEnd)
                    {
                        Skip(r, attempt.Position);
                        return;
                    }
                    errors.Add($"ability={abilityGain}: stopped at {attempt.Position} of {attempt.Length}");
                }
                catch (NwnWireException e)
                {
                    errors.Add($"ability={abilityGain}: {e.Message}");
                }
            }
            throw new NwnWireException("level-up choices don't parse: " + string.Join("; ", errors));
        }

        // Re-reading needs the remaining bytes; only the WSNetLayer encoding is self-describing enough to try both.
        static ReadOnlyMemory<byte> SliceFrom(NwnReader r) => r is NwnWsReader ws
            ? ws.Remaining
            : throw new NwnWireException("level-up choices can only be read from the WSNetLayer encoding");

        static void Skip(NwnReader r, int count) => ((NwnWsReader)r).Skip(count);

        void ReadVariant(NwnReader r, bool abilityGain)
        {
            HasAbilityGain = abilityGain;
            ClassLeveledUpIn = r.ReadByte();
            AbilityGain = abilityGain ? r.ReadByte() : (byte)0;
            HitDie = r.ReadByte();
            SkillChanges = r.ReadStruct<LevelUpSkillChanges>();
            SkillPointsRemaining = r.ReadWord();
            int feats = r.ReadByte();
            Feats = new(feats);
            for (int i = 0; i < feats; i++) Feats.Add(r.ReadWord());
            SpellChanges = new(SpellLevels);
            for (int level = 0; level < SpellLevels; level++)
            {
                var changes = new LevelUpSpellLevelChanges { Added = new(), Removed = new() };
                int added = r.ReadByte();
                for (int i = 0; i < added; i++) changes.Added.Add(r.ReadDword());
                int removed = r.ReadByte();
                for (int i = 0; i < removed; i++) changes.Removed.Add(r.ReadDword());
                SpellChanges.Add(changes);
            }
            FamiliarCreatureType = r.ReadInt();
            FamiliarName = r.ReadString();
            AnimalCompanionCreatureType = r.ReadInt();
            AnimalCompanionName = r.ReadString();
            Domain1 = Domain2 = School = 0;
            if (PicksDomains(r.Context, ClassLeveledUpIn))
            {
                Domain1 = r.ReadByte();
                Domain2 = r.ReadByte();
            }
            if (PicksSchool(r.Context, ClassLeveledUpIn)) School = r.ReadByte();
        }

        public override void Write(NwnWriter w)
        {
            if (SpellChanges.Count != SpellLevels) throw new NwnWireException($"level-up needs {SpellLevels} spell levels");
            w.WriteByte(ClassLeveledUpIn);
            if (HasAbilityGain) w.WriteByte(AbilityGain);
            w.WriteByte(HitDie);
            w.WriteStruct(SkillChanges);
            w.WriteWord(SkillPointsRemaining);
            w.WriteByte((byte)Feats.Count);
            foreach (var feat in Feats) w.WriteWord(feat);
            foreach (var level in SpellChanges)
            {
                w.WriteByte((byte)level.Added.Count);
                foreach (var spell in level.Added) w.WriteDword(spell);
                w.WriteByte((byte)level.Removed.Count);
                foreach (var spell in level.Removed) w.WriteDword(spell);
            }
            w.WriteInt(FamiliarCreatureType);
            w.WriteString(FamiliarName);
            w.WriteInt(AnimalCompanionCreatureType);
            w.WriteString(AnimalCompanionName);
            if (PicksDomains(w.Context, ClassLeveledUpIn))
            {
                w.WriteByte(Domain1);
                w.WriteByte(Domain2);
            }
            if (PicksSchool(w.Context, ClassLeveledUpIn)) w.WriteByte(School);
        }
    }
}

public static partial class DungeonMasterMessages
{
    /// <summary>The objects of one type in an area, for the DM; creatures carry names, hit points and states.</summary>
    public sealed class DungeonMasterObjectList : NwnMessage
    {
        public const byte MajorId = 0x1F;
        public const byte MinorId = 0x02;
        public override byte Major => MajorId;
        public override byte Minor => MinorId;
        public override NwnDirection Direction => NwnDirection.ServerToClient;

        [NwnField("area")]
        public uint Area { get; set; }
        [NwnField("object_type")]
        public int ObjectType { get; set; }
        [NwnField("objects")]
        public List<DungeonMasterObjectListEntry> Objects { get; set; } = new();

        bool Creatures => ObjectType == (int)NwnExternalTypes.ObjectType.Creature;

        public override void Read(NwnReader r)
        {
            Area = r.ReadObjectId();
            ObjectType = r.ReadInt();
            var count = r.ReadInt();
            Objects = new(Math.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                var e = new DungeonMasterObjectListEntry { ObjectId = r.ReadObjectId(), FirstName = r.ReadLocString() };
                if (Creatures)
                {
                    e.LastName = r.ReadLocString();
                    e.CurrentHitPoints = r.ReadShort();
                    e.MaxHitPoints = r.ReadShort();
                    e.Invulnerable = r.ReadBool();
                    e.Immortal = r.ReadBool();
                    e.IsPc = r.ReadBool();
                    e.IsDm = r.ReadBool();
                    e.IsControlled = r.ReadBool();
                    e.AiOn = r.ReadBool();
                }
                Objects.Add(e);
            }
        }

        public override void Write(NwnWriter w)
        {
            w.WriteObjectId(Area);
            w.WriteInt(ObjectType);
            w.WriteInt(Objects.Count);
            foreach (var e in Objects)
            {
                w.WriteObjectId(e.ObjectId);
                w.WriteLocString(e.FirstName);
                if (!Creatures) continue;
                w.WriteLocString(e.LastName);
                w.WriteShort(e.CurrentHitPoints);
                w.WriteShort(e.MaxHitPoints);
                w.WriteBool(e.Invulnerable);
                w.WriteBool(e.Immortal);
                w.WriteBool(e.IsPc);
                w.WriteBool(e.IsDm);
                w.WriteBool(e.IsControlled);
                w.WriteBool(e.AiOn);
            }
        }
    }
}
