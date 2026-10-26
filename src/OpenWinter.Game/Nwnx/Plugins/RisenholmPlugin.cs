using System.Diagnostics;
using OpenWinter.Aurora.Formats;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using JsonValue = OpenWinter.Game.Scripting.JsonValue;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Risenholm, the plugin of the Scars of Risenholm persistent world (its nwnx_risenholm.nss documents each
/// function). Measured on the fork's server where no player is needed: parity suites nwnx_risenholm,
/// nwnx_risenholm_more and nwnx_risenholm_fx.
/// </summary>
public static class RisenholmPlugin
{
    const string P = "Risenholm";

    sealed class State
    {
        public int MessageLimit = 400;
        public byte[]? Snapshot;
        public uint SnapshotOf = GameObject.InvalidId;
        public readonly Dictionary<uint, (uint Owner, HashSet<uint> Items)> Concealed = new();
        public HashSet<int> ShelterRows = new();
        public bool ShelterHooked;
        public readonly Dictionary<uint, Area> ShelterPlaced = new();
        public readonly HashSet<uint> Trimmed = new();
        public string? RpcRequest;
        public string? RpcResponse;
    }

    static readonly ConditionalWeakTable<NwnxRegistry, State> States = new();
    static State Get(NwnxCall n) => States.GetValue(n.Registry, _ => new State());
    static State Get(World w) => States.GetValue(w.System<NwnxRegistry>(), _ => new State());

    static Creature? Pc(GameObject? o) => o is Creature { IsPC: true } c ? c : null;

    // ---- players and clients

    /// <summary>Like or dislike on the player list only: the reputations that make players hostile stay as they are.</summary>
    [NwnxFunc(P, "SetPCLikeStatus")]
    static void SetPCLikeStatus(NwnxCall n)
    {
        var a = Pc(n.PopObject()); var b = Pc(n.PopObject()); bool like = n.PopInt() != 0; bool reciprocal = n.PopInt() != 0;
        if (a == null || b == null || a == b) return;
        void Set(Creature x, Creature y) { if (like) x.Dislikes.Remove(y.Id); else x.Dislikes.Add(y.Id); }
        Set(a, b);
        if (reciprocal) Set(b, a);
        Society.PartyService.Of(n.World).PlayerListChanged();
    }

    [NwnxFunc(P, "ForceUpdateMageArmorStats", UnsupportedReason = "the plugin's MAGE_ARMOR armour rule is not documented; the creature's stats are only recomputed")]
    static void ForceUpdateMageArmorStats(NwnxCall n)
    {
        if (n.PopObject<Creature>() is { } c) n.World.MarkChanged(c);
    }

    /// <summary>OpenWinter's clients keep no appearance cache to go stale: re-sending the creature is enough.</summary>
    [NwnxFunc(P, "ForceAppearanceUpdate")]
    static void ForceAppearanceUpdate(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); n.PopInt();
        if (c != null) n.World.MarkChanged(c);
    }

    [NwnxFunc(P, "RefreshBodyParts")]
    static void RefreshBodyParts(NwnxCall n)
    {
        if (n.PopObject<Creature>() is { } c) n.World.MarkChanged(c);
    }

    /// <summary>OpenWinter sends each player's object changes every server tick, so there is nothing waiting to flush.</summary>
    [NwnxFunc(P, "FlushObjectUpdates")]
    static void FlushObjectUpdates(NwnxCall n) => n.PopObject();

    /// <summary>The limit is kept and reported (400 to begin with); OpenWinter's updates are not split by size.</summary>
    [NwnxFunc(P, "SetObjectUpdateMessageLimit")]
    static void SetObjectUpdateMessageLimit(NwnxCall n)
    {
        int limit = n.PopInt();
        var st = Get(n);
        n.Push(st.MessageLimit);
        if (limit > 0) st.MessageLimit = limit;
    }

    [NwnxFunc(P, "SetResourceOverride")]
    static void SetResourceOverride(NwnxCall n)
    {
        var pc = Pc(n.PopObject()); int type = n.PopInt(); var old = n.PopString(); var replacement = n.PopString();
        if (pc == null) { n.Push(0); return; }
        n.World.Output?.Notify("resource-override", pc, new { type, old, @new = replacement }, pc);
        n.Push(1);
    }

    [NwnxFunc(P, "ForceExamineWindow")]
    static void ForceExamineWindow(NwnxCall n)
    {
        var pc = Pc(n.PopObject()); var target = n.PopObject();
        if (pc != null && target != null) Requests.Examine.Send(n.World, pc, target);
    }

    [NwnxFunc(P, "StartLevelUp")]
    static void StartLevelUp(NwnxCall n)
    {
        if (Pc(n.PopObject()) is { } pc) n.World.Output?.Notify("levelup-open", pc, new { }, pc);
    }

    [NwnxFunc(P, "RefreshPlayerListEntry")]
    static void RefreshPlayerListEntry(NwnxCall n)
    {
        n.PopObject();
        Society.PartyService.Of(n.World).PlayerListChanged();
    }

    /// <summary>The creature whose inventory the player's other-inventory panel shows (NWNX_Player_OpenInventory), if open.</summary>
    [NwnxFunc(P, "GetOtherInventoryOwner")]
    static void GetOtherInventoryOwner(NwnxCall n)
    {
        var pc = Pc(n.PopObject());
        n.PushObject(pc != null && n.State(pc).TryGetValue("otherInventory", out var v) && v.Value is uint id && n.World.Get(id) != null
            ? id : GameObject.InvalidId);
    }

    [NwnxFunc(P, "ConcealItemFromViewer")]
    static void ConcealItemFromViewer(NwnxCall n)
    {
        uint viewer = n.PopObjectId(); uint owner = n.PopObjectId(); uint item = n.PopObjectId();
        var st = Get(n);
        // A different owner replaces what was registered for the previous one.
        if (!st.Concealed.TryGetValue(viewer, out var entry) || entry.Owner != owner) st.Concealed[viewer] = entry = (owner, new HashSet<uint>());
        entry.Items.Add(item);
    }

    [NwnxFunc(P, "ClearConcealedItems")] static void ClearConcealedItems(NwnxCall n) => Get(n).Concealed.Remove(n.PopObjectId());

    [NwnxFunc(P, "GetIsItemConcealedFrom")]
    static void GetIsItemConcealedFrom(NwnxCall n)
    {
        uint viewer = n.PopObjectId(); uint item = n.PopObjectId();
        n.Push(Get(n).Concealed.TryGetValue(viewer, out var entry) && entry.Items.Contains(item));
    }

    /// <summary>Whether an item is registered as concealed from a viewer looking at its owner's inventory.</summary>
    public static bool IsConcealed(World w, uint viewer, uint owner, uint item) =>
        Get(w).Concealed.TryGetValue(viewer, out var entry) && entry.Owner == owner && entry.Items.Contains(item);

    /// <summary>A DM avatar that has appeared to players (always false for other creatures).</summary>
    [NwnxFunc(P, "GetIsDMManifested")]
    static void GetIsDMManifested(NwnxCall n) => n.Push(n.PopObject<Creature>() is { DMOwner: not null, DMHidden: false });

    // ---- appearance

    static string Cut16(string s) => s.Length > 16 ? s[..16] : s;

    /// <summary>The swap ReplaceObjectAnimation made, both names kept to 16 characters and matched exactly.</summary>
    [NwnxFunc(P, "GetAnimationReplace")]
    static void GetAnimationReplace(NwnxCall n)
    {
        var o = n.PopObject(); var old = Cut16(n.PopString());
        var swap = o?.AnimationReplacements?.FirstOrDefault(kv => Cut16(kv.Key) == old);
        n.Push(swap is { Key: not null } kv2 ? Cut16(kv2.Value) : "");
    }

    /// <summary>Any phenotype 0-255, on any creature.</summary>
    [NwnxFunc(P, "SetPhenoType")]
    static void SetPhenoType(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int pheno = n.PopInt();
        if (c == null || pheno is < 0 or > 255) { n.Push(0); return; }
        c.Phenotype = (byte)pheno;
        n.World.MarkChanged(c);
        n.Push(1);
    }

    const int HeadPart = 20; // CREATURE_PART_HEAD

    /// <summary>The head drawn instead of the real one: (drawn, the real head when it was set).</summary>
    static (int Drawn, int Real)? DisplayedHead(NwnxCall n, Creature c) =>
        n.State(c).TryGetValue("risenholm:head", out var v) && v.Value is ValueTuple<int, int> h ? h : null;

    static int RealHead(Creature c) => c.BodyParts.Length > HeadPart ? c.BodyParts[HeadPart] : 0;

    [NwnxFunc(P, "SetDisplayedHead")]
    static void SetDisplayedHead(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int head = n.PopInt();
        if (c == null || head is < 0 or > ushort.MaxValue) { n.Push(0); return; }
        if (head == 0) n.State(c).Remove("risenholm:head");
        else n.State(c)["risenholm:head"] = new NwnxValue(NwnxType.Int, (head, RealHead(c)));
        n.World.Output?.Notify("creature-override", c, new { displayedHead = head == 0 ? RealHead(c) : head });
        n.World.MarkChanged(c);
        n.Push(1);
    }

    /// <summary>The drawn head; changing the real head (SetCreatureBodyPart) undoes the override.</summary>
    [NwnxFunc(P, "GetDisplayedHead")]
    static void GetDisplayedHead(NwnxCall n)
    {
        if (n.PopObject<Creature>() is not { } c) { n.Push(-1); return; }
        n.Push(DisplayedHead(n, c) is { } h && h.Real == RealHead(c) ? h.Drawn : RealHead(c));
    }

    /// <summary>The health text row a creature shows: rows 0-4 shifted by its SetDamageLevelBase block; dead (5) passes.</summary>
    public static int ShiftDamageLevel(World w, Creature c, int level)
    {
        if (level is < 0 or > 4) return level;
        var st = w.System<NwnxRegistry>().ObjectState(c.Id);
        return st.TryGetValue("risenholm:damageLevelBase", out var v) && v.Value is int b && b > 0 ? b + level : level;
    }

    [NwnxFunc(P, "SetDamageLevelBase")]
    static void SetDamageLevelBase(NwnxCall n)
    {
        var o = n.PopObject(); int b = n.PopInt();
        if (o == null) return;
        if (b <= 0) n.State(o).Remove("risenholm:damageLevelBase");
        else n.State(o)["risenholm:damageLevelBase"] = NwnxValue.Int(Math.Min(b, 251));
        n.World.MarkChanged(o);
    }

    // ---- server

    /// <summary>Runs a program and returns what it printed. The arguments after the first empty one are left out.</summary>
    [NwnxFunc(P, "ExecuteCommand")]
    static void ExecuteCommand(NwnxCall n)
    {
        var cmd = n.PopString();
        var args = new List<string>();
        for (int i = 0; i < 6; i++) args.Add(n.PopString());
        if (cmd.Length == 0) { n.Push(""); return; }
        var psi = new ProcessStartInfo(cmd) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args.TakeWhile(a => a.Length > 0)) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi);
            if (p == null) { n.Push(""); return; }
            p.ErrorDataReceived += (_, _) => { };
            p.BeginErrorReadLine();
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(30000)) { try { p.Kill(true); } catch (InvalidOperationException) { } }
            n.Push(output.Wait(1000) ? output.Result : "");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            n.Registry.Log($"[nwnx] Risenholm: cannot run {cmd}: {e.Message}");
            n.Push("");
        }
    }

    /// <summary>
    /// A file named "shutdown" in the server's user folder asks for a shutdown; it is deleted when found. (Where the
    /// plugin looks is not documented; this is OpenWinter's choice.)
    /// </summary>
    [NwnxFunc(P, "CheckForShutdownFile")]
    static void CheckForShutdownFile(NwnxCall n)
    {
        var path = Path.Combine(n.World.System<PersistenceSystem>().UserRoot, "shutdown");
        bool found = File.Exists(path);
        if (found)
            try { File.Delete(path); }
            catch (IOException e) { n.Registry.Log($"[nwnx] Risenholm: cannot delete {path}: {e.Message}"); }
        n.Push(found);
    }

    /// <summary>
    /// OpenWinter keeps no per-frame AI update lists, so nothing is visited needlessly; this counts the objects each
    /// enabled NWNX_RISENHOLM_TRIM_AI_* switch covers, once each (0 with no switch set, as the plugin).
    /// </summary>
    [NwnxFunc(P, "TrimAILists")]
    static void TrimAILists(NwnxCall n)
    {
        var r = n.Registry;
        bool statics = r.EnvBool("NWNX_RISENHOLM_TRIM_AI_STATIC_PLACEABLES"), idle = r.EnvBool("NWNX_RISENHOLM_TRIM_AI_IDLE_PLACEABLES");
        bool items = r.EnvBool("NWNX_RISENHOLM_TRIM_AI_ITEMS"), doors = r.EnvBool("NWNX_RISENHOLM_TRIM_AI_IDLE_DOORS");
        bool triggers = r.EnvBool("NWNX_RISENHOLM_TRIM_AI_IDLE_TRIGGERS");
        var st = Get(n);
        int removed = 0;
        foreach (var o in n.World.AllObjects.ToList())
        {
            if (o.IsDestroyed || st.Trimmed.Contains(o.Id) || o.Actions.Count > 0) continue;
            bool heartbeat = o.Scripts.TryGetValue("OnHeartbeat", out var hb) && hb.Length > 0;
            bool trim = o switch
            {
                Placeable p when p.Static => statics && p.Effects.Count == 0,
                Placeable p => idle && !heartbeat && !p.HasInventory && p.Effects.Count == 0,
                Item i => items && i.Effects.Count == 0,
                Door d => doors && !heartbeat && d.Effects.Count == 0,
                Trigger => triggers && !heartbeat,
                _ => false,
            };
            if (trim && st.Trimmed.Add(o.Id)) removed++;
        }
        n.Push(removed);
    }

    /// <summary>The body of the RPC request the running script answers, or "".</summary>
    [NwnxFunc(P, "GetRpcRequest")] static void GetRpcRequest(NwnxCall n) => n.Push(Get(n).RpcRequest ?? "");

    [NwnxFunc(P, "SetRpcResponse")]
    static void SetRpcResponse(NwnxCall n)
    {
        var response = n.PopString();
        var st = Get(n);
        if (st.RpcRequest != null) st.RpcResponse = response;
    }

    /// <summary>Runs an RPC script with the module as OBJECT_SELF; its answer, or null if it set none.</summary>
    public static string? RunRpc(World w, string script, string body)
    {
        var st = Get(w);
        st.RpcRequest = body;
        st.RpcResponse = null;
        try
        {
            w.Scripts?.Run(script, w.Module.Id);
            return st.RpcResponse;
        }
        finally { st.RpcRequest = null; st.RpcResponse = null; }
    }

    // ---- effects

    /// <summary>The tag exactly, or any effect at all, the engine's hidden ones included, for "" (parity suite nwnx_risenholm_fx).</summary>
    static bool TagMatches(Effect e, string tag) => tag.Length == 0 || e.Tag == tag;

    [NwnxFunc(P, "GetHasEffectByTag")]
    static void GetHasEffectByTag(NwnxCall n)
    {
        var o = n.PopObject(); var tag = n.PopString();
        n.Push(o != null && o.Effects.Any(e => TagMatches(e, tag)));
    }

    /// <summary>String parameters 0-5: EffectRunScript's data, applied, removed and interval scripts, then two empty ones.</summary>
    [NwnxFunc(P, "GetHasEffectWithStringParam")]
    static void GetHasEffectWithStringParam(NwnxCall n)
    {
        var o = n.PopObject(); int index = n.PopInt(); var value = n.PopString();
        n.Push(o != null && index is >= 0 and <= 5 && o.Effects.Any(e => !e.Hidden && (index < e.Strings.Length ? e.Strings[index] : "") == value));
    }

    /// <summary>Removes the matching effects; the count is of applications, a linked effect counting once.</summary>
    [NwnxFunc(P, "RemoveEffectsByTag")]
    static void RemoveEffectsByTag(NwnxCall n)
    {
        var o = n.PopObject(); var tag = n.PopString();
        if (o == null) { n.Push(0); return; }
        var matches = o.Effects.Where(e => TagMatches(e, tag)).ToList();
        int applications = matches.Select(e => e.LinkId.Length > 0 ? e.LinkId : "#" + e.UniqueId.ToString(System.Globalization.CultureInfo.InvariantCulture)).Distinct().Count();
        n.World.System<EffectSystem>().RemoveWhere(o, matches.Contains, includeHidden: true);
        n.Push(applications);
    }

    // ---- items

    [NwnxFunc(P, "FixItemDestroySkipUseableState")]
    static void FixItemDestroySkipUseableState(NwnxCall n) => n.PopObject();

    static bool Dormant(Item item) => item.Locals.Ints.GetValueOrDefault("LOADOUT_DORMANT") != 0;

    [NwnxFunc(P, "ApplyItemProperties")]
    static void ApplyItemProperties(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var item = n.PopObject<Item>();
        if (c == null || item == null || !c.Equipment.ContainsValue(item) || Dormant(item)) { n.Push(0); return; }
        n.World.MarkChanged(c);
        n.Push(1);
    }

    /// <summary>Casts an item's single-use Cast Spell property at once, as an ordinary use would, with no action or animation.</summary>
    [NwnxFunc(P, "UseItemInstant")]
    static void UseItemInstant(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var item = n.PopObject<Item>(); var target = n.PopObject();
        var inv = n.World.Inventory;
        var prop = item?.Properties.FirstOrDefault(p => p.Type == Inventory.ItemRules.PropCastSpell && p.CostTable == 3 && p.CostTableValue == 1);
        if (c == null || item == null || target == null || prop == null || inv.Owner(item) != c || target.Area != c.Area
            || c.Equipment.ContainsValue(item) && Dormant(item)
            || c.Actions.Count > 0 && c.Actions[0] is Spells.CastSpellAction or Inventory.UseItemAction
            || !inv.CanUseItemProperty(item, prop) || !n.World.System<Skills.SkillSystem>().CanUseItem(c, item, out _))
        {
            n.Push(0);
            return;
        }
        n.Push(inv.UseItemProperty(c, item, prop, target.Id, target.Location, decrement: true));
    }

    [NwnxFunc(P, "SetStoreMarkUp")]
    static void SetStoreMarkUp(NwnxCall n)
    {
        var store = n.PopObject<Store>(); int markUp = n.PopInt();
        if (store != null && markUp is >= 1 and <= 30000) store.MarkUp = markUp;
    }

    // ---- combat

    [NwnxFunc(P, "AddAttackOfOpportunity")]
    static void AddAttackOfOpportunity(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var target = n.PopObject<Creature>();
        if (c != null && target != null && c != target && !c.IsDead && !target.IsDead) n.World.System<Combat.CombatSystem>().AttackOfOpportunity(c, target);
    }

    [NwnxFunc(P, "SetDamageBonusLimit")]
    static void SetDamageBonusLimit(NwnxCall n)
    {
        int limit = n.PopInt();
        if (limit >= 0) n.World.Limits.DamageBonus = limit;
    }

    [NwnxFunc(P, "SetScriptedCleave")] static void SetScriptedCleave(NwnxCall n) => n.World.System<Combat.CombatSystem>().ScriptedCleave = n.PopInt() != 0;

    [NwnxFunc(P, "DoCleaveAttack")]
    static void DoCleaveAttack(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var killed = n.PopObject();
        n.Push(c != null && n.World.System<Combat.CombatSystem>().QueueCleave(c, killed));
    }

    [NwnxFunc(P, "RemoveAttackImmunityFeedback", UnsupportedReason = "it is called from the NWNX_Damage attack event, which OpenWinter's combat does not run; immunity is reported with the hit")]
    static void RemoveAttackImmunityFeedback(NwnxCall n)
    {
        n.PopObject(); n.PopInt(); n.PopInt();
    }

    // ---- movement

    /// <summary>Forced walking, kept with the NWNX_Player setting of the same name (alwaysWalk) for the client.</summary>
    [NwnxFunc(P, "SetAlwaysWalk")]
    static void SetAlwaysWalk(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); bool walk = n.PopInt() != 0;
        if (c == null) return;
        n.State(c)["alwaysWalk"] = NwnxValue.Int(walk ? 1 : 0);
        if (c.IsPC) n.World.Output?.Notify("nwnx-player", c, new { op = "always-walk", walk }, c);
    }

    // ---- shelters

    /// <summary>placeables.2da rows (comma separated) whose placeables mark their area's SHELTER_LIVE_DIRTY when they come or go.</summary>
    [NwnxFunc(P, "SetShelterAppearances")]
    static void SetShelterAppearances(NwnxCall n)
    {
        var rows = n.PopString();
        var st = Get(n);
        st.ShelterRows = rows.Split(',').Select(r => int.TryParse(r.Trim(), out int v) ? v : -1).Where(v => v >= 0).ToHashSet();
        if (st.ShelterHooked) return;
        st.ShelterHooked = true;
        var w = n.World;
        w.ObjectAdded += o =>
        {
            if (o is not Placeable p || !st.ShelterRows.Contains(p.Appearance) || p.Area is not { } area) return;
            if (st.ShelterPlaced.TryGetValue(p.Id, out var was) && was == area) return;
            st.ShelterPlaced[p.Id] = area;
            area.Locals.Ints["SHELTER_LIVE_DIRTY"] = 1;
        };
        w.ObjectRemoved += o =>
        {
            if (o is not Placeable p || !st.ShelterPlaced.Remove(p.Id, out var area) || area.IsDestroyed || area.Destroying) return;
            area.Locals.Ints["SHELTER_LIVE_DIRTY"] = 1;
        };
    }

    // ---- afterimages

    [NwnxFunc(P, "PrepareAfterimage")]
    static void PrepareAfterimage(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        var st = Get(n);
        st.Snapshot = c == null ? null : ObjectSerializer.ToBytes(n.World, c, saveObjectState: true);
        st.SnapshotOf = st.Snapshot == null ? GameObject.InvalidId : c!.Id;
        n.Push(st.Snapshot?.Length ?? 0);
    }

    [NwnxFunc(P, "ReleaseAfterimage")]
    static void ReleaseAfterimage(NwnxCall n)
    {
        var st = Get(n);
        st.Snapshot = null;
        st.SnapshotOf = GameObject.InvalidId;
    }

    /// <summary>
    /// A copy of the creature with its effects, actions and equipment but no backpack or variables: plot, unusable,
    /// unlootable, at full hit points, in the given engine faction (STANDARD_FACTION_* + 1), marked IS_SET_PIECE and
    /// IS_VFX, invisible-looking, and (extraordinary effects) never hitting and a ghost (parity suite nwnx_risenholm).
    /// </summary>
    [NwnxFunc(P, "CreateAfterimage")]
    static void CreateAfterimage(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var area = n.PopObject<Area>();
        float x = n.PopFloat(), y = n.PopFloat(), z = n.PopFloat(), facing = n.PopFloat();
        int faction = n.PopInt(); float speed = n.PopFloat();
        var st = Get(n);
        var w = n.World;
        if (c == null || area == null) { n.PushObject(GameObject.InvalidId); return; }
        var data = st.SnapshotOf == c.Id && st.Snapshot != null ? st.Snapshot : ObjectSerializer.ToBytes(w, c, saveObjectState: true);
        var at = new Location(area.Id, new System.Numerics.Vector3(x, y, z), facing);
        if (data == null || ObjectSerializer.FromBytes(w, data, at, null, loadObjectState: true) is not Creature image) { n.PushObject(GameObject.InvalidId); return; }
        foreach (var item in image.Inventory.ToList()) w.Destroy(item);
        image.Inventory.Clear();
        image.Locals.Ints.Clear(); image.Locals.Floats.Clear(); image.Locals.Strings.Clear(); image.Locals.Objects.Clear(); image.Locals.Locations.Clear();
        foreach (var key in image.Locals.Jsons.Keys.ToList()) image.Locals.Jsons.Remove(key);
        image.Locals.Ints["IS_SET_PIECE"] = 1;
        image.Locals.Ints["IS_VFX"] = 1;
        image.Plot = true;
        image.Lootable = false;
        w.Inventory.UseableOverrides[image.Id] = false;
        image.CurrentHitPoints = image.MaxHitPoints;
        int f = faction is >= 1 and <= 4 ? w.Factions.StandardFaction(faction - 1) : faction;
        if (w.Factions.IsValidFaction(f)) image.Faction = f;
        w.AddToArea(image, area, at.Position, facing * MathF.PI / 180f, exact: true, enterEvent: false);
        var k = EffectConstants.Of(w);
        var fx = w.System<EffectSystem>();
        var vfx = new Effect { Type = k.VisualEffect, SubType = k.Magical, Creator = image.Id };
        vfx.Ints[0] = k["VFX_DUR_INVISIBILITY"];
        vfx.Floats[0] = 1f;
        fx.Apply(vfx, k.Permanent, image, 0);
        var miss = new Effect { Type = k["EFFECT_TYPE_MISS_CHANCE"], SubType = k.Extraordinary, Creator = image.Id };
        miss.Ints[0] = 100;
        fx.Apply(miss, k.Permanent, image, 0);
        fx.Apply(new Effect { Type = k["EFFECT_TYPE_CUTSCENEGHOST"], SubType = k.Extraordinary, Creator = image.Id }, k.Permanent, image, 0);
        if (speed != 1f)
        {
            image.VisualTransforms ??= new();
            int transform = k["OBJECT_VISUAL_TRANSFORM_ANIMATION_SPEED"];
            image.VisualTransforms[(0, transform)] = new WorldObjects.VisualTransform { From = speed, To = speed, StartTime = w.Time, PauseWithGame = true };
            w.Output?.Notify("visual-transform", image, new { transform, scope = 0, from = speed, value = speed, lerp = 0, duration = 0f, pauseWithGame = true, behavior = 0, repeats = 0 });
        }
        w.MarkChanged(image);
        n.PushObject(image.Id);
    }

    // ---- level history

    static JsonArray SpellLists(GffStruct entry, string prefix)
    {
        var all = new JsonArray();
        for (int lvl = 0; lvl < 10; lvl++)
            all.Add(new JsonArray(entry.GetList(prefix + lvl).Select(s => (JsonNode?)(int)s.GetInt("Spell")).ToArray()));
        return all;
    }

    /// <summary>
    /// Every level the creature has taken (its level history) and its classes, in the plugin's JSON layout.
    /// </summary>
    public static JsonObject LevelHistory(World w, Creature c)
    {
        CreatureRules.EnsureInit(w, c);
        int skills = w.Resources.TwoDA("skills").RowCount;
        var levels = new JsonArray();
        var history = c.Template.GetList("LvlStatList");
        if (history.Count > 0)
            foreach (var e in history)
            {
                var ranks = e.GetList("SkillList").Select(s => (int)s.GetInt("Rank")).ToList();
                while (ranks.Count < skills) ranks.Add(0);
                levels.Add(new JsonObject
                {
                    ["ability"] = (int)e.GetInt("LvlStatAbility", 6),
                    ["class"] = (int)e.GetInt("LvlStatClass"),
                    ["epic"] = (int)e.GetInt("EpicLevel"),
                    ["feats"] = new JsonArray(e.GetList("FeatList").Select(f => (JsonNode?)(int)f.GetInt("Feat")).ToArray()),
                    ["hitdie"] = (int)e.GetInt("LvlStatHitDie"),
                    ["known"] = SpellLists(e, "KnownList"),
                    ["skillpoints"] = (int)e.GetInt("SkillPoints"),
                    ["skills"] = new JsonArray(ranks.Select(r => (JsonNode?)r).ToArray()),
                    ["unknown"] = SpellLists(e, "KnownRemoveList"),
                });
            }
        else if (c.Classes.Count > 0)
        {
            // A creature with no stored history has one blank record, of its first class, whatever its level
            // (parity suite nwnx_risenholm_more).
            var empty = new GffStruct();
            levels.Add(new JsonObject
            {
                ["ability"] = 6, ["class"] = c.Classes[0].Class, ["epic"] = 0, ["feats"] = new JsonArray(), ["hitdie"] = 0,
                ["known"] = SpellLists(empty, "KnownList"), ["skillpoints"] = 0,
                ["skills"] = new JsonArray(Enumerable.Repeat(0, skills).Select(r => (JsonNode?)r).ToArray()),
                ["unknown"] = SpellLists(empty, "KnownList"),
            });
        }
        var classes = new JsonArray(c.Classes.Select(cc => (JsonNode?)new JsonObject
        {
            ["class"] = cc.Class, ["domain1"] = Math.Max(0, cc.Domain1), ["domain2"] = cc.Domain2 >= 0 ? cc.Domain2 : 1, ["school"] = Math.Max(0, cc.School),
        }).ToArray());
        return new JsonObject { ["classes"] = classes, ["levels"] = levels };
    }

    [NwnxFunc(P, "GetLevelHistory")]
    static void GetLevelHistory(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(new JsonValue(c == null ? new JsonObject { ["classes"] = new JsonArray(), ["levels"] = new JsonArray() } : LevelHistory(n.World, c)));
    }

    static int Int(JsonNode? node, string key, int fallback = -1) =>
        node is JsonObject o && o[key] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue(out int i) ? i : fallback;

    /// <summary>
    /// Re-applies the stored levels above the creature's own (no level-up event), as far as the XP pays for and the
    /// level cap allows, then sets the XP. -1, changing nothing, for a malformed history or one whose earlier levels
    /// are not the creature's own class for class.
    /// </summary>
    [NwnxFunc(P, "RestoreLevelHistory")]
    static void RestoreLevelHistory(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var history = n.PopJson(); int xp = n.PopInt();
        if (c == null || history.Node is not JsonObject root || root["levels"] is not JsonArray levels || root["classes"] is not JsonArray classes
            || levels.Any(l => Int(l, "class") < 0) || classes.Any(cl => Int(cl, "class") < 0))
        {
            n.Push(-1);
            return;
        }
        var w = n.World;
        var current = LevelHistory(w, c)["levels"]!.AsArray();
        if (current.Count > levels.Count) { n.Push(-1); return; }
        for (int i = 0; i < current.Count; i++)
            if (Int(current[i], "class") != Int(levels[i], "class")) { n.Push(-1); return; }
        int restored = 0;
        for (int i = current.Count; i < levels.Count; i++)
        {
            int level = i + 1;
            if (level > CreatureRules.MaxLevel || xp < CreatureRules.XPForLevel(w, level)) break;
            var l = levels[i]!.AsObject();
            int cls = Int(l, "class");
            var ch = new Progression.LevelChoice { Class = cls, Ability = Int(l, "ability") is var a and >= 0 and <= 5 ? a : -1 };
            if (l["skills"] is JsonArray ranks)
                for (int s = 0; s < ranks.Count; s++)
                    if (ranks[s] is System.Text.Json.Nodes.JsonValue rv && rv.TryGetValue(out int r) && r > 0) ch.Skills[s] = r;
            if (l["feats"] is JsonArray feats) ch.Feats.AddRange(feats.OfType<System.Text.Json.Nodes.JsonValue>().Select(f => f.TryGetValue(out int fi) ? fi : -1).Where(f => f >= 0));
            if (l["known"] is JsonArray known)
                for (int sl = 0; sl < known.Count; sl++)
                    if (known[sl] is JsonArray spells && spells.Count > 0)
                        ch.Spells[sl] = spells.OfType<System.Text.Json.Nodes.JsonValue>().Select(s => s.TryGetValue(out int si) ? si : -1).Where(s => s >= 0).ToList();
            if (classes.FirstOrDefault(cl => Int(cl, "class") == cls) is JsonObject info)
            {
                ch.Domain1 = Int(info, "domain1"); ch.Domain2 = Int(info, "domain2"); ch.School = Int(info, "school");
            }
            var plan = Progression.ProgressionRules.Plan(w, c, ch, player: false, checkVariables: false);
            int hitDie = Int(l, "hitdie", 0);
            Progression.LevelApplier.Apply(w, c, ch, plan, hitDie > 0 ? hitDie : null);
            restored++;
        }
        c.Experience = Math.Max(0, xp);
        w.MarkChanged(c);
        n.Push(restored);
    }
}
