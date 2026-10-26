using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>What a creature is polymorphed into (polymorph.2da), and what it was before.</summary>
public sealed class PolymorphState
{
    public int Row;
    /// <summary>The EFFECT_TYPE_POLYMORPH this form belongs to (its UniqueId).</summary>
    public long EffectId;
    /// <summary>Base Strength, Constitution and Dexterity of the new form (0: the creature's own).</summary>
    public int Str, Con, Dex;
    public int NaturalAC;
    public int OldAppearance, OldRace, OldSize, OldPortraitId, OldSoundSet;
    public string OldPortrait = "";
    /// <summary>
    /// The creature's own equipment (slot bit to item), taken out of its slots for the form. The creature still
    /// possesses these items, but they are in no slot and not in its backpack.
    /// </summary>
    public readonly SortedDictionary<int, Item> Stored = new();
    /// <summary>The form's creature weapons, hide and weapon. They are destroyed when the form ends.</summary>
    public readonly List<Item> Form = new();
    /// <summary>The effect has ended and the creature gets its own form back shortly.</summary>
    public bool Ending;
    /// <summary>The change-back visual has been shown.</summary>
    public bool ChangeBackShown;
    /// <summary>The hidden temporary hit points HPBONUS gave, or null.</summary>
    public Effect? Bonus;

    /// <summary>A stored item the creature still has (not destroyed, not taken by someone else meanwhile).</summary>
    public bool StillHeld(Creature c, Item item) => !item.IsDestroyed && item.Possessor == c.Id && item.Area == null;
}

/// <summary>
/// EffectPolymorph, as measured on the original server (parity suites fx_poly and fx_polyitems):
/// - The creature takes the form's appearance, racial type, portrait, sound set and size; its base Strength, Constitution
///   and Dexterity become the form's (effects still add to them); its size modifier to AC no longer counts; the
///   form's natural AC applies; and it gains the form's bonus hit points as temporary hit points that aren't in
///   the effect list.
/// - Everything it has equipped leaves its slot. It keeps possessing those items (GetItemPossessor), but they are
///   in no slot, not in its backpack, and GetItemPossessedBy doesn't find them; none of their properties count.
///   The engine itself merges nothing: polymorph.2da MergeA/MergeI/MergeW are read by the spell scripts, which
///   copy properties onto the form's items (IPWildShapeCopyItemProperties).
/// - The form's items are created and equipped: CreatureWeapon1-3 in the creature weapon slots (left, right,
///   bite), HideItem in the creature armour slot and EQUIPPED in the right hand; a form with an EQUIPPED weapon
///   gets no creature weapons. Nothing can be equipped or
///   unequipped while polymorphed. No acquire, unacquire or equip events fire, going in or coming out.
/// - A new polymorph replaces the current one at once: the old effect and its items go, the creature's own
///   items stay put away.
/// - When the effect ends (expiry, dispel, RemoveEffect) the creature changes back half a second later; until
///   then an expired or dispelled polymorph is still listed with its second integer set. Death and resting
///   change it back at once. Its own items go back to their slots (the same objects) and the form's are destroyed.
/// - Copies and saved creatures (CopyObject, SQL and campaign objects) are the creature in its own form with its
///   own items equipped, and no polymorph.
/// Deliberately unlike the original: an item that left the creature while it was polymorphed (taken or given by
/// script) is not put back in its slot. The original equips it again while the new holder keeps it too, which
/// duplicates it. The form's items can't leave the creature either.
/// </summary>
public sealed partial class EffectSystem
{
    /// <summary>The polymorph a creature is under (or changing back from), or null.</summary>
    public static PolymorphState? Polymorph(Creature c) => c.PolymorphForm;

    /// <summary>How long the original takes to change a creature back after its polymorph ended.</summary>
    public const double UnpolymorphDelay = 0.5;

    /// <summary>The creature's own equipment: what it wears, or while polymorphed what it wore before (and still has).</summary>
    public static List<KeyValuePair<int, Item>> OwnEquipment(Creature c) =>
        c.PolymorphForm is { } p
            ? p.Stored.Where(kv => p.StillHeld(c, kv.Value)).ToList()
            : c.Equipment.Where(kv => !kv.Value.IsDestroyed).ToList();

    /// <summary>Whether an item is one a polymorph created (a form's creature weapon, hide or weapon) and is still in use.</summary>
    public static bool IsFormItem(World world, Item item) =>
        world.Get(item.Possessor) is Creature { PolymorphForm: { } p } && p.Form.Contains(item);

    void StartPolymorph(EffectConstants k, Effect e, Creature c)
    {
        var table = _world.Resources.TwoDA("polymorph");
        int row = e.Ints[0];
        if (row < 0 || row >= table.RowCount) return;
        // NWNX_ON_POLYMORPH as the form takes over (nwnx_events_creature); a skip keeps the creature as it is.
        var ev = Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_POLYMORPH", c)?.Add("POLYMORPH_TYPE", row);
        if (ev?.Before() == true) { ev.After(); return; }
        StartPolymorphNow(k, e, c, table, row);
        ev?.After();
    }

    void StartPolymorphNow(EffectConstants k, Effect e, Creature c, OpenWinter.Aurora.Formats.TwoDA table, int row)
    {
        CreatureRules.EnsureInit(_world, c);
        var prior = c.PolymorphForm;
        var p = new PolymorphState
        {
            Row = row, EffectId = e.UniqueId,
            Str = table.GetInt(row, "STR", 0), Con = table.GetInt(row, "CON", 0), Dex = table.GetInt(row, "DEX", 0),
            NaturalAC = table.GetInt(row, "NATURALACBONUS", 0),
            OldAppearance = prior?.OldAppearance ?? c.AppearanceType, OldRace = prior?.OldRace ?? c.Race,
            OldSize = prior?.OldSize ?? c.Size, OldPortraitId = prior?.OldPortraitId ?? c.PortraitId,
            OldPortrait = prior?.OldPortrait ?? c.Portrait, OldSoundSet = prior?.OldSoundSet ?? c.SoundSet,
        };
        if (prior != null)
        {
            // A new polymorph replaces the current one: its items go, the creature's own stay put away.
            DestroyForm(c, prior);
            foreach (var kv in prior.Stored) p.Stored[kv.Key] = kv.Value;
        }
        c.PolymorphForm = p;
        if (prior != null)
            RemoveLinks(c, c.Effects.Where(x => x != e && x.Type == e.Type && x.LinkId != e.LinkId).ToList());

        // Everything equipped leaves its slot; the creature still possesses it.
        foreach (var (slot, item) in c.Equipment.ToList())
        {
            c.Equipment.Remove(slot);
            if (item.IsDestroyed) continue;
            if (p.Stored.TryGetValue(slot, out var earlier) && p.StillHeld(c, earlier))
            {
                // Can't happen (nothing is equipped while polymorphed), but never lose an item.
                _world.Inventory.MoveTo(item, c, force: true);
                continue;
            }
            p.Stored[slot] = item;
            item.Possessor = c.Id;
            item.InvX = item.InvY = -1;
            _world.Inventory.EquipmentChanged(c, item, -1);
        }
        // A form that wields a weapon (EQUIPPED) gets no creature weapons (parity suite fx_poly_abilities: the
        // Rakshasa's bite and the frost giant's slam are left out; the hide is still worn).
        string weapon = table.Get(row, "EQUIPPED");
        if (weapon.Length == 0)
        {
            EquipForm(c, p, EquipSlot.CreatureLeft, table.Get(row, "CreatureWeapon1"));
            EquipForm(c, p, EquipSlot.CreatureRight, table.Get(row, "CreatureWeapon2"));
            EquipForm(c, p, EquipSlot.CreatureBite, table.Get(row, "CreatureWeapon3"));
        }
        EquipForm(c, p, EquipSlot.CreatureArmour, table.Get(row, "HideItem"));
        EquipForm(c, p, EquipSlot.RightHand, weapon);

        int appearance = table.GetInt(row, "AppearanceType", -1);
        if (appearance >= 0)
        {
            c.AppearanceType = appearance;
            c.Size = CreatureRules.AppearanceSize(_world, appearance);
        }
        // The form's sound set (polymorph.2da SoundSet; parity suite fx_poly_abilities: chicken 21, wyrmling 331).
        int soundSet = table.GetInt(row, "SoundSet", -1);
        c.SoundSet = soundSet >= 0 ? soundSet : p.OldSoundSet;
        int race = table.GetInt(row, "RacialType", -1);
        if (race >= 0) c.Race = (byte)race;
        int portrait = table.GetInt(row, "PortraitId", -1);
        if (portrait >= 0)
        {
            c.PortraitId = portrait;
            var baseRef = _world.Resources.TwoDA("portraits").Get(portrait, "BaseResRef");
            c.Portrait = baseRef.Length > 0 ? "po_" + baseRef : table.Get(row, "Portrait");
        }
        // Hit points move with the maximum only if the form sets a Constitution: a drow form (its own Constitution,
        // but the elf's -2) loses no hit points going in, though its maximum is one lower; coming back it gains one.
        if (p.Con > 0) MoveHitPoints(c);
        else if (!c.IsDead) c.MaxHitPoints = CreatureRules.MaxHitPoints(_world, c);
        int bonus = table.GetInt(row, "HPBONUS", 0);
        if (bonus > 0)
        {
            // Bonus hit points go with the polymorph (same link) but don't show in the effect list.
            var hp = new Effect
            {
                Type = k.TemporaryHitpoints, SubType = e.SubType, DurationType = e.DurationType, Duration = e.Duration, ExpiresAt = e.ExpiresAt,
                Creator = e.Creator, SpellId = e.SpellId, UniqueId = NextId(), ScriptId = e.ScriptId, LinkId = e.LinkId,
                Target = c.Id, AppliedAt = e.AppliedAt, Hidden = true,
            };
            hp.Ints[0] = bonus;
            c.Effects.Insert(EffectOrder.InsertAt(c.Effects, hp.Type), hp);
            p.Bonus = hp;
        }
        TellArmour(c);
        _world.MarkChanged(c);
    }

    /// <summary>
    /// The line the original prints for a player whose armour changes, which a form's start and end both do:
    /// "Armor/Shield Applies: Skill Armor Check Penalty: 0 Arcane Spell Failure: 0%" in yellow (visual scene
    /// polymorphpc, both ways, with no armour). A non-zero penalty's sign has not been seen.
    /// </summary>
    void TellArmour(Creature c)
    {
        if (!c.IsPC) return;
        int penalty = CreatureRules.ArmourCheckPenalty(_world, c), asf = CreatureRules.ArcaneSpellFailure(_world, c);
        _world.Output?.SendMessage(c, $"<c\u00FF\u00FF\u0001>Armor/Shield Applies: Skill Armor Check Penalty: {(penalty == 0 ? "0" : "-" + penalty)} Arcane Spell Failure: {asf}%</c>");
    }

    /// <summary>Creates one of the form's items from its blueprint and puts it in its slot.</summary>
    void EquipForm(Creature c, PolymorphState p, int slot, string resref)
    {
        if (resref.Length == 0) return;
        var item = _world.Items.FromBlueprint(resref.ToLowerInvariant());
        if (item == null) return;
        _world.Register(item);
        item.Possessor = c.Id;
        c.Equipment[slot] = item;
        p.Form.Add(item);
        _world.Inventory.EquipmentChanged(c, item, EquipSlot.Index(slot));
    }

    /// <summary>Destroys the form's items, wherever they are.</summary>
    void DestroyForm(Creature c, PolymorphState p)
    {
        foreach (var item in p.Form)
        {
            if (item.IsDestroyed) continue;
            foreach (var kv in c.Equipment.Where(kv => kv.Value == item).ToList())
            {
                c.Equipment.Remove(kv.Key);
                _world.Inventory.EquipmentChanged(c, item, -1);
            }
            _world.Destroy(item);
        }
        p.Form.Clear();
    }

    /// <summary>
    /// The polymorph effect <paramref name="e"/> left the creature's effect list: it changes back half a second
    /// later, or at once if the effect already waited that long (see <see cref="DeferPolymorphEnd"/>).
    /// </summary>
    void EndPolymorph(Creature c, Effect e)
    {
        if (c.PolymorphForm is not { } p || p.EffectId != e.UniqueId) return;
        // The original raises NWNX_ON_UNPOLYMORPH once the script that ended the effect is over, and again as the
        // creature changes back (nwnx_events_creature).
        if (Nwnx.Plugins.EventsPlugin.Listening(_world, "NWNX_ON_UNPOLYMORPH"))
            _world.After(0, () => { if (!c.IsDestroyed) UnpolymorphEvent(c); }, persist: false);
        if (e.Ints[5] != 0 || c.IsDead || c.IsDestroyed) { ShowChangeBack(c, p); Unpolymorph(c); return; }
        if (p.Ending) return;
        p.Ending = true;
        ShowChangeBack(c, p);
        // Removed (RemoveEffect), the polymorph leaves the list at once but its bonus hit points stay until the
        // creature changes back (visual scene polymorphpc2: 18/8 0.2 s after RemoveEffect, 5/5 at 0.8 s).
        if (p.Bonus is { } kept && !c.Effects.Contains(kept) && kept.Ints[0] > 0)
        {
            kept.LinkId = "";
            kept.Target = c.Id;
            c.Effects.Insert(EffectOrder.InsertAt(c.Effects, kept.Type), kept);
        }
        _world.After(UnpolymorphDelay, () => { if (c.PolymorphForm == p && !c.IsDestroyed) Unpolymorph(c); });
    }

    /// <summary>
    /// An expired or dispelled polymorph stays listed, with its second integer set, until the creature changes back
    /// at <paramref name="due"/>; then it (and its link) goes.
    /// </summary>
    void DeferPolymorphEnd(GameObject target, List<Effect> link, double due)
    {
        var k = K;
        foreach (var x in link) if (x.Type == k["EFFECT_TYPE_POLYMORPH"]) x.Ints[5] = 1;
        if (target is Creature { PolymorphForm: { } form } c && link.Any(x => x.UniqueId == form.EffectId))
        {
            form.Ending = true;
            ShowChangeBack(c, form);
        }
        _world.After(Math.Max(0, due - _world.Time), () =>
        {
            var still = link.Where(target.Effects.Contains).ToList();
            if (still.Count > 0) RemoveLinks(target, still);
        });
    }

    /// <summary>VFX_IMP_POLYMORPH, which the engine plays itself when a form ends.</summary>
    public const int ChangeBackVfx = 85;

    /// <summary>
    /// The original plays the polymorph visual (VFX_IMP_POLYMORPH, the green columns) on the creature the moment its
    /// form ends (cancelled, removed, run out), half a second before the model changes back; no script asks for it
    /// (visual scenes polymorphpc and polymorphpc2). Shown once per form.
    /// </summary>
    void ShowChangeBack(Creature c, PolymorphState p)
    {
        if (p.ChangeBackShown || c.IsDestroyed) return;
        p.ChangeBackShown = true;
        Notify("vfx", c, new { vfx = ChangeBackVfx, creator = c.Id, miss = false, scale = 1f, translate = Vec(default), rotate = Vec(default), duration = 0f, id = 0L });
    }

    /// <summary>The hidden temporary hit points a polymorph's HPBONUS gave.</summary>
    bool IsPolymorphBonus(Effect e, GameObject target) =>
        target is Creature { PolymorphForm: { } p } && ReferenceEquals(p.Bonus, e);

    /// <summary>The player's Cancel Polymorph: the form ends as if it ran out (see <see cref="DeferPolymorphEnd"/>).</summary>
    public void CancelPolymorph(Creature c, Effect polymorph)
    {
        if (!c.Effects.Contains(polymorph)) return;
        DeferPolymorphEnd(c, c.Effects.Where(x => x.LinkId == polymorph.LinkId).ToList(), _world.Time + UnpolymorphDelay);
    }

    /// <summary>
    /// The form's abilities (polymorph.2da SPELL1-3), by column: -1 where the column is empty. The original offers
    /// them on a polymorphed player's radial (Cast Spell: NE, E, SE); they aren't spells the creature has.
    /// </summary>
    public static int[] FormAbilities(World world, Creature c)
    {
        var abilities = new[] { -1, -1, -1 };
        if (c.PolymorphForm is not { } p) return abilities;
        var table = world.Resources.TwoDA("polymorph");
        for (int i = 0; i < 3; i++) abilities[i] = table.GetInt(p.Row, "SPELL" + (i + 1), -1);
        return abilities;
    }

    static bool IsPolymorphLink(EffectConstants k, IEnumerable<Effect> effects) =>
        effects.Any(x => x.Type == k["EFFECT_TYPE_POLYMORPH"] && !x.Hidden);

    /// <summary>Changes a creature back at once (death, resting) if it is polymorphed.</summary>
    public void EndPolymorphNow(Creature c)
    {
        if (c.PolymorphForm is not { } form) return;
        ShowChangeBack(c, form);
        var k = K;
        var listed = c.Effects.Where(x => x.Type == k["EFFECT_TYPE_POLYMORPH"]).ToList();
        foreach (var x in listed) x.Ints[5] = 1;
        if (listed.Count > 0) RemoveLinks(c, listed);
        if (c.PolymorphForm != null) Unpolymorph(c);
    }

    /// <summary>Changes a creature back at once if its polymorph effect has already gone (resting).</summary>
    public void FinishPolymorphEnd(Creature c)
    {
        if (c.PolymorphForm is { } p && !c.Effects.Any(x => x.UniqueId == p.EffectId)) Unpolymorph(c);
    }

    /// <summary>
    /// Gives the creature its own form back: the form's items are destroyed and its own go back to their slots
    /// (only those it still has).
    /// </summary>
    void UnpolymorphEvent(Creature c)
    {
        var ev = Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_UNPOLYMORPH", c);
        ev?.Before();
        ev?.After();
    }

    void Unpolymorph(Creature c)
    {
        if (c.PolymorphForm is not { } p) return;
        UnpolymorphEvent(c);
        c.PolymorphForm = null;
        if (p.Bonus is { } bonus && c.Effects.Contains(bonus)) RemoveOnly(c, bonus);
        DestroyForm(c, p);
        foreach (var (slot, item) in p.Stored)
        {
            if (!p.StillHeld(c, item)) continue;
            // Nothing can be equipped while polymorphed; should anything be in the slot, it makes room rather than vanish.
            if (c.Equipment.TryGetValue(slot, out var occupant) && occupant != item)
            {
                c.Equipment.Remove(slot);
                _world.Inventory.MoveTo(occupant, c, force: true);
            }
            c.Equipment[slot] = item;
            item.InvX = item.InvY = -1;
            _world.Inventory.EquipmentChanged(c, item, EquipSlot.Index(slot));
        }
        p.Stored.Clear();
        c.AppearanceType = p.OldAppearance;
        c.Race = (byte)p.OldRace;
        c.Size = p.OldSize;
        c.PortraitId = p.OldPortraitId;
        c.Portrait = p.OldPortrait;
        c.SoundSet = p.OldSoundSet;
        MoveHitPoints(c);
        TellArmour(c);
        _world.MarkChanged(c);
    }

    /// <summary>
    /// Going into or out of a form, hit points move with the maximum without a cap (a drow form comes back one
    /// over its maximum) and even for the dead (a dead bear goes from -11 to -14 as its Constitution bonus goes).
    /// </summary>
    void MoveHitPoints(Creature c)
    {
        int max = CreatureRules.MaxHitPoints(_world, c), delta = max - c.MaxHitPoints, before = c.CurrentHitPoints;
        if (delta == 0) return;
        c.MaxHitPoints = max;
        if (c.IsDead || before > 0) c.CurrentHitPoints = before + delta;
        if (!c.IsDead && before > 0 && before + delta <= 0) HitPoints.CheckDeath(_world, c, GameObject.InvalidId, before);
    }

    /// <summary>What is left of the form's bonus hit points (polymorph.2da HPBONUS), which damage takes first.</summary>
    public int PolymorphBonusLeft(Creature c)
    {
        if (c.PolymorphForm is not { Bonus: { } bonus }) return 0;
        return c.Effects.Contains(bonus) ? Math.Max(0, bonus.Ints[0]) : 0;
    }

    /// <summary>
    /// EffectDeath on a polymorphed creature whose form's bonus hit points would carry it above 0 from the -11
    /// the effect leaves: it survives, changed back, at 1 hit point (parity suite fx_poly_death: a drow form with
    /// 12 or more bonus hit points left survives, with 11 it dies; temporary hit points from other effects don't
    /// count). Returns whether it survived.
    /// </summary>
    public bool SurvivesDeath(Creature c, int deathHitPoints)
    {
        if (c.PolymorphForm == null || deathHitPoints + PolymorphBonusLeft(c) <= 0) return false;
        EndPolymorphNow(c);
        if (c.IsDead) return false;
        c.CurrentHitPoints = 1;
        _world.MarkChanged(c);
        return true;
    }

    /// <summary>
    /// Current and stored maximum hit points as they will be once the creature has its own form back (the move
    /// <see cref="MoveHitPoints"/> makes): what a character file or a copy holds. The maximum leaves out the
    /// Constitution that equipment and effects add (<see cref="CreatureRules.StoredMaxHitPoints"/>) (visual scene polymorphpc2: exported
    /// as a bear at 8/8, the character file reads 5/5). Without a form, the hit points as they are.
    /// </summary>
    /// <remarks>With <paramref name="bonus"/>, also the Constitution and feat bonus (without effects) in that form,
    /// which character files leave out of CurrentHitPoints (asking for it initialises the creature's rules).</remarks>
    public static (int Current, int Max, int Bonus) OwnHitPoints(World world, Creature c, bool bonus = false)
    {
        if (c.PolymorphForm is not { } p)
        {
            // The bonus first: it may initialise the rules, which settles current hit points. A creature the rules
            // haven't touched still holds its template's values and is left alone.
            int own = bonus ? CreatureRules.HitPointBonus(world, c, effects: false, items: false) : 0;
            return (c.CurrentHitPoints, c.RulesInitialised ? CreatureRules.StoredMaxHitPoints(world, c, c.MaxHitPoints) : c.MaxHitPoints, own);
        }
        byte race = c.Race;
        int size = c.Size;
        try
        {
            c.PolymorphForm = null;
            c.Race = (byte)p.OldRace;
            c.Size = p.OldSize;
            int max = CreatureRules.MaxHitPoints(world, c);
            return (c.CurrentHitPoints + max - c.MaxHitPoints, CreatureRules.StoredMaxHitPoints(world, c, max),
                CreatureRules.HitPointBonus(world, c, effects: false, items: false));
        }
        finally
        {
            c.PolymorphForm = p;
            c.Race = race;
            c.Size = size;
        }
    }

    /// <summary>A creature that goes away (destroyed, logged out) takes its put-away items and its form's items with it.</summary>
    public void ForgetPolymorph(Creature c)
    {
        if (c.PolymorphForm is not { } p) return;
        c.PolymorphForm = null;
        DestroyForm(c, p);
        foreach (var item in p.Stored.Values.ToList())
            if (p.StillHeld(c, item)) _world.Destroy(item);
        p.Stored.Clear();
    }
}
