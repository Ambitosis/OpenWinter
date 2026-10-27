using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ClientSideMessageCombatMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Combat, as the client knows it from OpenWinter's own server: attacks become "attack" notifies (the swing, the
/// target's reaction, hit or miss and the damage), damage and healing "hp-change", the combat messages and combat
/// feedback chat lines; hit points, damage levels, reactions and effect icons feed the "hud", "creature-labels" and
/// "reactions" notifies; dead and dying creatures are flagged; the death panel and battle music are passed on. The
/// client's attack and respawn requests go back as the input nwmain sends.
/// </summary>
public sealed class NwnCombat(INwnBridgeData data, NwnWorldMirror world, Action<byte[]> emit)
{
    /// <summary>How the server rates a creature for the player (its AI state's reaction): neutral, friendly, hostile.</summary>
    public const int Neutral = 0, Friendly = 1, Hostile = 2;

    /// <summary>A player character dies at -10 hit points; from 0 down to there she is dying.</summary>
    const int DeadAt = -10;

    /// <summary>The damagelevels.2da row of a dead creature.</summary>
    const int DamageLevelDead = 5;

    /// <summary>NWN's equipment slot bit for the chest.</summary>
    const int ChestSlot = 0x2;

    /// <summary>How long a hit waits for its damage message before later damage is no longer taken for it.</summary>
    const double HitWait = 2;

    const uint InvalidObject = GameObjUpdateMessages.InvalidObjectId;

    sealed class Creature
    {
        public int? Hp, MaxHp, TempHp;
        public int DamageLevel = -1;
        public int Reaction;
        public bool IsPc;
        public string Portrait = "";
        public int Level;
        public uint Master = InvalidObject;
        public int Associate;
        public ObjectFlags Condition;
    }

    readonly Dictionary<uint, Creature> _creatures = [];
    readonly List<(ushort Icon, bool Flashing)> _icons = [];
    /// <summary>Hits waiting for their damage message, by attacker and target: when each swing is shown.</summary>
    readonly Dictionary<(uint, uint), Queue<double>> _pendingHits = [];
    /// <summary>When each attacker's last swing ends, so the next one follows it.</summary>
    readonly Dictionary<uint, double> _swingEnds = [];
    readonly List<(double At, byte[] Message)> _later = [];
    readonly List<byte[]> _afterList = [];
    readonly Dictionary<uint, int> _reactionsSent = [];
    readonly Dictionary<uint, (string, int)> _labelsSent = [];
    readonly List<uint> _gone = [];
    readonly Dictionary<(uint, ushort), Stack<long>> _effects = [];
    long _nextEffect;
    uint _labelArea = uint.MaxValue, _reactionArea = uint.MaxValue, _area;
    uint _target;
    int _gold, _ac;
    uint? _xp;
    bool _canLevelUp, _hudDirty, _inList;
    string _lastHud = "";
    double _clock, _nextHud, _hudHeld;
    int _roll = Environment.TickCount & int.MaxValue;

    // -- client to server ---------------------------------------------------

    /// <summary>The NWN message for a client request, or null when there is none to send (or it isn't a combat one).</summary>
    public NwnMessage? FromClient(string kind, string json)
    {
        if (kind is not ("attack" or "respawn" or "hud-target")) return null;
        // Requests without arguments (the death panel's respawn) carry no JSON at all.
        var d = (string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json)) as JsonObject ?? [];
        uint Id(string key) => d[key] is JsonValue v && v.TryGetValue<uint>(out var id) ? id : 0;
        switch (kind)
        {
            case "attack" when Id("target") is var target and not 0:
                return new InputMessages.InputAttack { Target = target };
            case "respawn":
                return new DeathMessages.DeathRespawn();
            case "hud-target":
                // The HUD's target frame: answered from what the bridge knows, at the next update.
                _target = Id("id");
                (_hudDirty, _nextHud) = (true, 0);
                break;
        }
        return null;
    }

    /// <summary>
    /// A left click on a creature: nwmain attacks one that is hostile to the player (and alive) instead of
    /// talking to or using it. Null for anything else.
    /// </summary>
    public NwnMessage? Click(uint target) =>
        _creatures.TryGetValue(target, out var c) && c.Reaction == Hostile && c.Condition == 0 && target != world.Controlled
            ? new InputMessages.InputAttack { Target = target }
            : null;

    /// <summary>The server's reaction to a creature for the player: <see cref="Neutral"/>, <see cref="Friendly"/> or <see cref="Hostile"/>.</summary>
    public int Reaction(uint id) => _creatures.TryGetValue(id, out var c) ? c.Reaction : Neutral;

    // -- server to client ---------------------------------------------------

    /// <summary>Combat messages, the death panel, battle music and fire-and-forget visual effects; false for anything else.</summary>
    public bool ToClient(NwnMessage message)
    {
        switch (message)
        {
            case ClientSideMessageComplexDamage d:
                Damage(d);
                Log(NwnCombatLog.Line(d, data, Name));
                return true;
            case ClientSideMessageComplexAttack or ClientSideMessageSpecialAttack or ClientSideMessageTouchAttack or ClientSideMessageComplexDeath
                or ClientSideMessageInitiative or ClientSideMessageSavingThrow or ClientSideMessageSpellResistance
                or ClientSideMessageCounterspell or ClientSideMessageDispelMagic:
                Log(NwnCombatLog.Line(message, data, Name));
                return true;
            case PopupGuiPanelMessages.PopUpGUIPanelPopUpPlayerDeathPanel p:
                // Shown whenever the server asks, dead or not: some modules show it to the living.
                Notify("gui-death", world.Controlled, new JsonObject
                {
                    ["respawn"] = p.RespawnButton, ["waitForHelp"] = p.WaitForHelpButton,
                    ["help"] = p.StringOverride.Length > 0 ? p.StringOverride : p.StringReference != 0 ? data.Text(p.StringReference, 0) : "",
                });
                return true;
            case PopupGuiPanelMessages.PopUpGUIPanelDestroyPlayerDeathPanel:
                Notify("gui-death-close", world.Controlled, []);
                return true;
            case AmbientMessages.AmbientBattleMusicPlay music:
                Notify("battle-music", world.Controlled, new JsonObject { ["on"] = music.Play });
                return true;
            case GameObjUpdateMessages.GameObjUpdateVisEffect v:
                VisualEffect(v);
                return true;
        }
        return false;
    }

    /// <summary>Combat feedback (defences, immunities, healing, experience) as chat lines; false for other feedback.</summary>
    public bool Feedback(ClientSideMessageFeedbackMessages.ClientSideMessageFeedback feedback)
    {
        if (NwnCombatLog.Feedback(feedback.Payload, data, Name) is not { } line) return false;
        if (feedback.Payload.Value is ClientSideMessageFeedbackMessages.FeedbackDamageHealed healed)
            Notify("hp-change", healed.Target, new JsonObject { ["target"] = healed.Target, ["amount"] = healed.Amount, ["type"] = 0, ["combat"] = false });
        Log(line);
        return true;
    }

    /// <summary>
    /// The object list: hit points, damage levels, reactions, deaths, attacks and effects of creatures, and the
    /// player's icons, gold, armour class and experience. Conditions are given to the world mirror before it
    /// describes the list (<paramref name="mirror"/>), so a creature that arrives dead spawns dead; the attacks follow.
    /// </summary>
    public void ObjectList(GameObjUpdateMessages.GameObjUpdateObjList list, Action<GameObjUpdateMessages.GameObjUpdateObjList> mirror)
    {
        if (_area != world.AreaId)
        {
            // A new area: only the player's own creature comes along.
            _area = world.AreaId;
            foreach (var id in _creatures.Keys.Where(id => id != world.Controlled).ToList()) _creatures.Remove(id);
            _pendingHits.Clear();
            _swingEnds.Clear();
            _effects.Clear();
        }
        _inList = true;
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case GameObjUpdateObjListUpdateCreature u:
                    Update(u);
                    break;
                case GameObjUpdateObjListPlayerInfo p when p.ObjectId == world.Controlled || world.Controlled == 0:
                    PlayerInfo(p);
                    break;
                case GameObjUpdateMessages.ObjListGuiEntry { Value: GuiCharacterSheet { Experience: { } xp } }:
                    (_xp, _hudDirty) = (xp, true);
                    break;
                case GameObjUpdateDeleteMessages.ObjListDeleteCreature d:
                    Forget(d.ObjectId);
                    break;
            }
        }
        _inList = false;
        mirror(list);
        foreach (var m in _afterList) emit(m);
        _afterList.Clear();
    }

    void Forget(uint id)
    {
        // A party member or the target the server removes (a henchman destroyed, a summons dismissed) leaves the HUD.
        if (_creatures.Remove(id, out var gone)) _hudDirty |= gone.Master == world.Controlled || id == _target;
        _swingEnds.Remove(id);
        foreach (var key in _pendingHits.Keys.Where(k => k.Item1 == id || k.Item2 == id).ToList()) _pendingHits.Remove(key);
    }

    void Update(GameObjUpdateObjListUpdateCreature u)
    {
        var c = Of(u.ObjectId);
        bool watched = u.ObjectId == world.Controlled || c.Master == world.Controlled || u.ObjectId == _target || _partyPlayers.Any(p => p.Id == u.ObjectId);
        if (u.PcPartyStatus is { } status) c.IsPc = status.IsPc;
        if (u.HpCurrent is { } hp)
        {
            (c.Hp, c.MaxHp, c.TempHp) = (hp, u.HpMax, u.HpTemp);
            _hudDirty |= watched;
        }
        if (u.DamageLevel is { } level)
        {
            c.DamageLevel = level;
            _hudDirty |= watched;
        }
        if (u.AiReaction is { } reaction && reaction != c.Reaction)
        {
            c.Reaction = reaction;
            _hudDirty |= watched;
        }
        if (u.PortraitId is { } portrait)
            c.Portrait = portrait >= 65534 ? (u.CustomPortrait ?? "").ToLowerInvariant()
                : data.TwoDA("portraits", portrait, "BaseResRef") is { Length: > 0 } baseRef && baseRef != "****" ? "po_" + baseRef.ToLowerInvariant() : "";
        if (u.PrimaryAttributes is { } attributes) c.Level = attributes.Classes.Sum(k => k.Level);
        if (u.MasterAndAssoc is { } master) (c.Master, c.Associate) = (master.MasterId, master.AssociateType);
        if (u.AnimTypeData is { } anim)
            foreach (var attack in anim.Attacks) Swing(u.ObjectId, attack);
        Condition(u.ObjectId);
        if (u.Effects is { } effects) DurationEffects(u.ObjectId, effects);
    }

    /// <summary>
    /// Dead or dying. A player character (whose hit points the server sends) is dying from 0 down to -10 and dead
    /// below; any other creature is dead at the "dead" damage level. Lying down alone tells nothing: the dead
    /// animations a script plays on the living send the same animation codes (bridgecombat's bystanders).
    /// </summary>
    void Condition(uint id)
    {
        var c = Of(id);
        bool player = c.IsPc || id == world.Controlled;
        var condition = player && c.Hp is { } hp
            ? hp <= DeadAt ? ObjectFlags.Dead : hp <= 0 ? ObjectFlags.Dying : ObjectFlags.None
            : c.DamageLevel == DamageLevelDead ? ObjectFlags.Dead : ObjectFlags.None;
        if (condition == c.Condition) return;
        c.Condition = condition;
        world.SetFlags(id, condition, ObjectFlags.Dead | ObjectFlags.Dying);
        _hudDirty = true;
    }

    void PlayerInfo(GameObjUpdateObjListPlayerInfo p)
    {
        if (p.AcGoldWeightLevelup is { } stats)
        {
            (_gold, _ac, _canLevelUp) = ((int)stats.Gold, stats.Ac, stats.CanLevelUp);
            _hudDirty = true;
        }
        if (p.EffectIcons is { } icons)
        {
            foreach (var removed in icons.Remove)
            {
                int at = _icons.FindIndex(i => i.Icon == removed);
                if (at >= 0) _icons.RemoveAt(at);
            }
            foreach (var added in icons.Add)
            {
                _icons.Add((added.Icon, added.Flashing));
                // A flashing icon is one about to run out (removed and added again flashing).
                _afterList.Add(Notice("effect-icon-flash", world.Controlled, new JsonObject { ["icon"] = (int)added.Icon, ["flashing"] = added.Flashing }));
            }
            _hudDirty = true;
        }
    }

    /// <summary>
    /// An attack in the attacker's update: the swing, the target's reaction, hit or miss and its damage. Each swing
    /// starts when the attacker's previous one ends (anim_len), so several in one update, or updates arriving
    /// together after a stall, play in order without overlapping.
    /// </summary>
    void Swing(uint attacker, GameObjUpdateAnimationMessages.AttackRecord a)
    {
        double start = Math.Max(_clock, _swingEnds.GetValueOrDefault(attacker));
        _swingEnds[attacker] = start + a.AnimLen / 1000.0;
        bool hit = NwnCombatLog.IsHit(a.Result);
        bool critical = a.Result is NwnCombatLog.ResultCritical or NwnCombatLog.ResultDevastating;
        bool parried = a.Result == NwnCombatLog.ResultParried;
        bool offHand = a.WeaponAttackType == 2;
        if (hit)
        {
            if (!_pendingHits.TryGetValue((attacker, a.TargetId), out var waiting)) _pendingHits[(attacker, a.TargetId)] = waiting = new();
            waiting.Enqueue(start);
            // The player's hit points drop in the HUD when the blow is shown, not before.
            if (a.TargetId == world.Controlled) _hudHeld = Math.Max(_hudHeld, start);
        }
        var appearance = world.AppearanceOf(attacker);
        int Hand(int slot) => appearance?.Equipment.FirstOrDefault(e => e.Slot == slot)?.BaseItem ?? -1;
        int right = Hand(NwnSwings.SlotRight), left = Hand(NwnSwings.SlotLeft);
        float distance = world.TryPose(attacker, out var from, out _, out _, out _) && world.TryPose(a.TargetId, out var to, out _, out _, out _)
            ? Vector2.Distance(new(from.X, from.Y), new(to.X, to.Y)) : 1f;
        _roll = (int)((_roll * 1103515245L + 12345) & int.MaxValue);
        string clip = NwnSwings.Swing(data, appearance?.Type ?? 0, right, left, a.Ranged, offHand, distance, _roll >> 8);
        var target = world.AppearanceOf(a.TargetId);
        int armour = target?.Equipment.FirstOrDefault(e => e.Slot == ChestSlot) is { ArmorParts.Length: > 2 } chest
            && float.TryParse(data.TwoDA("parts_chest", chest.ArmorParts[2], "ACBONUS"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var ac) ? (int)ac : 0;
        var sounds = NwnSwings.Sounds(data, offHand ? left : right, appearance?.Type ?? 0, clip, target?.Type ?? 0, armour,
            hit, critical, parried, a.Ranged, _roll >> 4);
        At(start, Notice("attack", attacker, new JsonObject
        {
            ["pending"] = false, ["attacker"] = attacker, ["target"] = a.TargetId, ["hit"] = hit, ["critical"] = critical,
            ["damage"] = (int)a.Damage, ["dealt"] = (int)a.Damage, ["ranged"] = a.Ranged, ["animation"] = clip,
            ["parried"] = parried, ["concealed"] = a.Result is NwnCombatLog.ResultConcealed or NwnCombatLog.ResultMissChance,
            ["killed"] = a.Killing, ["offHand"] = offHand, ["kind"] = "normal",
            ["sounds"] = new JsonArray(sounds.Select(s => (JsonNode)s).ToArray()),
        }));
    }

    /// <summary>
    /// Damage dealt. A weapon blow's numbers float with its attack, so its hp-change is marked as combat damage and
    /// goes with the swing; anything else (spells, scripts, a hit whose damage never came) floats on its own.
    /// </summary>
    void Damage(ClientSideMessageComplexDamage d)
    {
        var (total, _) = NwnCombatLog.DamageParts(d.Damages, data);
        double? swing = null;
        if (_pendingHits.TryGetValue((d.Effector, d.Target), out var waiting))
        {
            while (waiting.Count > 0 && swing == null)
            {
                double start = waiting.Dequeue();
                if (start + HitWait >= _clock) swing = start;
            }
            if (waiting.Count == 0) _pendingHits.Remove((d.Effector, d.Target));
        }
        At(swing ?? _clock, Notice("hp-change", d.Target, new JsonObject
        {
            ["target"] = d.Target, ["source"] = d.Effector, ["amount"] = -total, ["type"] = NwnCombatLog.MainType(d.Damages), ["combat"] = swing != null,
        }));
    }

    // -- visual effects -----------------------------------------------------

    /// <summary>A fire-and-forget visual effect on an object or at a place (impacts, blood, a death's spray).</summary>
    void VisualEffect(GameObjUpdateMessages.GameObjUpdateVisEffect v)
    {
        var type = data.TwoDA("visualeffects", v.VisualEffectId, "Type_FD");
        if (!type.Equals("F", StringComparison.OrdinalIgnoreCase)) return;
        var json = new JsonObject { ["vfx"] = (int)v.VisualEffectId, ["duration"] = 0f, ["id"] = 0L };
        if (world.KindOf(v.TargetId) is not null)
        {
            Notify("vfx", v.TargetId, json);
            return;
        }
        var (x, y, z) = v.TargetLocation;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) return;
        json["pos"] = new JsonArray(x, y, z);
        Notify("vfx", world.AreaId, json);
    }

    /// <summary>
    /// A creature's lasting visual effects coming and going (change 'A' added, 'D' removed). Each one shown gets its
    /// own id; a removal ends the latest of that effect on the creature.
    /// </summary>
    void DurationEffects(uint id, List<GameObjUpdateSharedMessages.VisualEffectUpdate> effects)
    {
        foreach (var e in effects)
        {
            // Beams are drawn between two objects (NwnPresentation).
            if (NwnPresentation.IsBeam(data, e.EffectId)) continue;
            var key = (id, e.EffectId);
            if (e.ChangeType == 'A')
            {
                long effect = (1L << 40) | ++_nextEffect;
                if (!_effects.TryGetValue(key, out var shown)) _effects[key] = shown = new();
                shown.Push(effect);
                _afterList.Add(Notice("vfx", id, new JsonObject { ["vfx"] = (int)e.EffectId, ["duration"] = -1f, ["id"] = effect }));
            }
            else if (e.ChangeType == 'D' && _effects.TryGetValue(key, out var shown) && shown.TryPop(out var effect))
            {
                if (shown.Count == 0) _effects.Remove(key);
                _afterList.Add(Notice("effect-removed", id, new JsonObject { ["id"] = effect, ["type"] = 0, ["linkId"] = "", ["icon"] = 0 }));
            }
        }
    }

    // -- the HUD, labels and reactions --------------------------------------

    /// <summary>Called ten times a second: swings and damage due, and the HUD, labels and reactions when they changed.</summary>
    /// <summary>The player's action queue as the HUD shows it, from the abilities bridge.</summary>
    public void ActionQueue(JsonArray queue)
    {
        _queue = queue;
        _hudDirty = true;
    }

    JsonArray _queue = [];

    /// <summary>The other players in the player's party (NwnParty), shown in the party bar before the associates.</summary>
    public void PartyPlayers(IReadOnlyList<(uint Id, string Name, string Portrait)> players)
    {
        _partyPlayers = players;
        _hudDirty = true;
    }

    IReadOnlyList<(uint Id, string Name, string Portrait)> _partyPlayers = [];

    public void Tick(float seconds)
    {
        _clock += seconds;
        for (int i = 0; i < _later.Count; i++)
        {
            if (_later[i].At > _clock) continue;
            emit(_later[i].Message);
            _later.RemoveAt(i--);
        }
        if (world.Controlled == 0) return;
        Reactions();
        Labels();
        if (!_hudDirty || _clock < _nextHud || _clock < _hudHeld) return;
        _hudDirty = false;
        _nextHud = _clock + 0.25;
        var hud = Hud().ToJsonString();
        if (hud == _lastHud) return;
        _lastHud = hud;
        emit(new NotifyMessage { Kind = "hud", Subject = world.Controlled, Json = hud }.Write());
    }

    JsonObject Hud()
    {
        uint me = world.Controlled;
        var self = Of(me);
        var party = new JsonArray();
        foreach (var (id, name, portrait) in _partyPlayers)
            party.Add(_creatures.TryGetValue(id, out var pc) ? Member(id, pc)
                : new JsonObject { ["id"] = id, ["name"] = name, ["portrait"] = portrait, ["hp"] = 0, ["maxHp"] = 0, ["dead"] = false, ["pc"] = true, ["associate"] = 0, ["master"] = InvalidObject });
        foreach (var (id, c) in _creatures)
            if (id != me && c.Master == me) party.Add(Member(id, c));
        // Experience as the character sheet last reported it, within its level's band of exptable.2da.
        int Exp(int level) => level > 0 && int.TryParse(data.TwoDA("exptable", level - 1, "XP"), out var xp) ? xp : 0;
        int xpNow = (int)(_xp ?? 0), xpLevel = _xp == null ? 0 : Exp(self.Level), xpNext = _xp == null ? 0 : Math.Max(xpNow, Exp(self.Level + 1));
        return new JsonObject
        {
            ["self"] = new JsonObject
            {
                ["id"] = me, ["name"] = Name(me), ["portrait"] = self.Portrait, ["hp"] = self.Hp ?? 0, ["maxHp"] = self.MaxHp ?? 0,
                ["tempHp"] = self.TempHp ?? 0, ["dead"] = self.Condition == ObjectFlags.Dead, ["level"] = self.Level,
                ["xp"] = xpNow, ["xpLevel"] = xpLevel, ["xpNext"] = xpNext, ["canLevelUp"] = _canLevelUp, ["gold"] = _gold, ["ac"] = _ac,
                ["queue"] = _queue.DeepClone(), ["effects"] = new JsonArray(_icons.Select(i => (JsonNode)(int)i.Icon).ToArray()),
            },
            ["party"] = party,
            ["target"] = Target(),
        };
    }

    /// <summary>A party member (the player's associate): dead at the "dead" damage level, never dying.</summary>
    JsonObject Member(uint id, Creature c)
    {
        var (hp, max) = Health(c);
        return new JsonObject
        {
            ["id"] = id, ["name"] = Name(id), ["portrait"] = c.Portrait, ["hp"] = Math.Max(0, hp), ["maxHp"] = max,
            ["dead"] = c.Condition == ObjectFlags.Dead, ["pc"] = c.IsPc, ["associate"] = c.Associate, ["master"] = c.Master,
        };
    }

    /// <summary>Exact hit points where the server sends them, otherwise a share of 100 from the damage level.</summary>
    static (int Hp, int Max) Health(Creature c) =>
        c.Hp is { } hp && c.MaxHp is { } max and > 0 ? (hp, max) : ((int)MathF.Round(LevelHealth(c.DamageLevel) * 100), 100);

    /// <summary>
    /// The health a damagelevels.2da row stands for, within the band the client words it by (uninjured, barely
    /// injured, wounded, badly wounded, near death, dead).
    /// </summary>
    static float LevelHealth(int level) => level switch { <= 0 => 1f, 1 => 0.85f, 2 => 0.6f, 3 => 0.35f, 4 => 0.1f, _ => 0f };

    JsonObject? Target()
    {
        if (_target == 0 || world.KindOf(_target) is not { } kind || kind is not (ObjectKind.Creature or ObjectKind.Door or ObjectKind.Placeable)) return null;
        var c = kind == ObjectKind.Creature ? Of(_target) : null;
        bool exact = c != null && (_target == world.Controlled || c.Master == world.Controlled) && c.Hp != null;
        float health = c == null ? 1 : exact && c.MaxHp is > 0 ? Math.Clamp(c.Hp!.Value / (float)c.MaxHp.Value, 0, 1) : LevelHealth(c.DamageLevel);
        return new JsonObject
        {
            ["id"] = _target, ["name"] = Name(_target), ["portrait"] = c?.Portrait ?? "", ["health"] = health,
            ["hp"] = exact ? c!.Hp : -1, ["maxHp"] = exact ? c!.MaxHp : -1, ["level"] = c?.Level ?? 0,
            ["attitude"] = c == null ? "object" : c.Reaction switch { Hostile => "hostile", Friendly => "friendly", _ => "neutral" },
            ["dead"] = c?.Condition == ObjectFlags.Dead, ["plot"] = false,
        };
    }

    /// <summary>
    /// "reactions": how each creature rates the player, for highlight and name colours. The server rates the
    /// player's own party friendly; a friendly faction alone is still neutral (blue), as OpenWinter's server keeps it.
    /// </summary>
    void Reactions()
    {
        var changes = new JsonObject();
        if (_reactionArea != world.AreaId)
        {
            (_reactionArea, changes["reset"]) = (world.AreaId, true);
            _reactionsSent.Clear();
        }
        foreach (var (id, c) in _creatures)
        {
            if (id == world.Controlled || world.KindOf(id) != ObjectKind.Creature) continue;
            if (_reactionsSent.TryGetValue(id, out var sent) ? sent == c.Reaction : c.Reaction == Neutral) continue;
            _reactionsSent[id] = c.Reaction;
            changes[id.ToString()] = c.Reaction;
        }
        if (changes.Count > 0) Notify("reactions", world.Controlled, changes);
    }

    /// <summary>"creature-labels": the name and health bar (damagelevels.2da row) shown over each creature.</summary>
    void Labels()
    {
        var changes = new JsonObject();
        if (_labelArea != world.AreaId)
        {
            (_labelArea, changes["reset"]) = (world.AreaId, true);
            _labelsSent.Clear();
        }
        foreach (var (id, c) in _creatures)
        {
            if (c.DamageLevel < 0 || world.KindOf(id) != ObjectKind.Creature) continue;
            var label = (Name(id), c.DamageLevel);
            if (_labelsSent.TryGetValue(id, out var sent) && sent == label) continue;
            // A name the server changes (SetName, and back) is the HUD's too.
            if (sent.Item1 is { } before && before != label.Item1) _hudDirty |= id == world.Controlled || c.Master == world.Controlled || id == _target;
            _labelsSent[id] = label;
            changes[id.ToString()] = new JsonObject { ["name"] = label.Item1, ["playerName"] = "", ["damageLevel"] = label.DamageLevel };
        }
        _gone.Clear();
        foreach (var id in _labelsSent.Keys)
            if (!_creatures.ContainsKey(id)) _gone.Add(id);
        foreach (var id in _gone)
        {
            _labelsSent.Remove(id);
            changes[id.ToString()] = null;
        }
        if (changes.Count > 0) Notify("creature-labels", world.Controlled, changes);
    }

    // -- helpers ------------------------------------------------------------

    Creature Of(uint id)
    {
        if (!_creatures.TryGetValue(id, out var c)) _creatures[id] = c = new Creature();
        return c;
    }

    string Name(uint id) => world.NameOf(id);

    void Log(string? line)
    {
        if (!string.IsNullOrEmpty(line)) emit(new ChatMessage { Channel = ChatChannel.Server, Text = line }.Write());
    }

    /// <summary>Sends a message now (after the object list being read) or when it is due.</summary>
    void At(double when, byte[] message)
    {
        if (when > _clock) _later.Add((when, message));
        else if (_inList) _afterList.Add(message);
        else emit(message);
    }

    static byte[] Notice(string kind, uint subject, JsonObject json) =>
        new NotifyMessage { Kind = kind, Subject = subject, Json = json.ToJsonString() }.Write();

    void Notify(string kind, uint subject, JsonObject json) => emit(Notice(kind, subject, json));
}
