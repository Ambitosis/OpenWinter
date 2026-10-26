using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>NWNX_ELC, NWNX_SkillRanks, NWNX_Weapon and NWNX_Damage behaviour that the NWNX parity lab can't reach.</summary>
public class NwnxCombatPluginTests
{
    static World ScriptWorld()
    {
        var w = new World(TestData.Module);
        w.Register(w.Module);
        w.EnableScripts();
        return w;
    }

    static void Script(World w, string name, string body)
    {
        var code = NssCompiler.Compile("void main(){" + body + "}", name, w.Scripts!.Definitions, _ => null, out var error);
        Assert.True(code != null, error);
        w.Scripts.Register(name, new NcsProgram(name, code!));
    }

    static void Call(World w, string plugin, string function, params NwnxValue[] args)
    {
        var reg = w.System<NwnxRegistry>();
        foreach (var a in args) reg.Stack.Push(a);
        Assert.True(reg.Call(plugin, function));
    }

    /// <summary>A legal first-level character's bic with one feat it never gained.</summary>
    static GffStruct IllegalBic(World w, int feat)
    {
        var n = ProgressionTests.Spec(w, 6, 4);
        var result = CharacterFactory.Build(w, n);
        Assert.True(result.Ok, string.Join("; ", result.Errors));
        var bic = Gff.Read(Gff.Write(result.Bic!, "BIC "));
        var feats = bic.GetList("FeatList");
        feats.Add(new GffStruct().SetWord("Feat", feat));
        return bic;
    }

    [DataFact]
    public void ElcScriptHearsEachFailureAndMaySkipIt()
    {
        var w = ScriptWorld();
        var bic = IllegalBic(w, NwRules.FeatArtist);
        var errors = LegalCharacterCheck.Check(w, bic);
        Assert.Contains(errors, e => e.Code == "feats" && e.Feat == NwRules.FeatArtist);
        var pc = w.Creatures.FromTemplate(bic);
        w.Register(pc);

        // Without a script the plain check decides.
        var failure = ElcPlugin.Validate(w, pc, errors);
        Assert.NotNull(failure);
        Assert.Equal(ElcPlugin.TypeFeat, failure!.Type);
        Assert.Equal(ElcPlugin.SubFeatList, failure.SubType);

        // The script reads the failure and skips feat failures only.
        Script(w, "elc_skip", "NWNXCall(\"NWNX_ELC\",\"GetValidationFailureType\");int t=NWNXPopInt();"
            + "NWNXCall(\"NWNX_ELC\",\"GetValidationFailureFeatID\");SetLocalInt(GetModule(),\"feat\",NWNXPopInt());"
            + "NWNXCall(\"NWNX_ELC\",\"GetValidationFailureSubType\");SetLocalInt(GetModule(),\"sub\",NWNXPopInt());"
            + "if(t==4)NWNXCall(\"NWNX_ELC\",\"SkipValidationFailure\");");
        Call(w, "ELC", "SetELCScript", NwnxValue.String("elc_skip"));
        Assert.Null(ElcPlugin.Validate(w, pc, errors));
        Assert.Equal(NwRules.FeatArtist, w.Module.Locals.Ints["feat"]);
        Assert.Equal(ElcPlugin.SubFeatList, w.Module.Locals.Ints["sub"]);

        // The custom check fails unless skipped, with the message the script chose.
        Call(w, "ELC", "EnableCustomELCCheck", NwnxValue.Int(1));
        Script(w, "elc_custom", "NWNXCall(\"NWNX_ELC\",\"GetValidationFailureType\");int t=NWNXPopInt();"
            + "if(t==4)NWNXCall(\"NWNX_ELC\",\"SkipValidationFailure\");"
            + "if(t==6){NWNXPushInt(125);NWNXCall(\"NWNX_ELC\",\"SetValidationFailureMessageStrRef\");}");
        Call(w, "ELC", "SetELCScript", NwnxValue.String("elc_custom"));
        failure = ElcPlugin.Validate(w, pc, errors);
        Assert.NotNull(failure);
        Assert.Equal(ElcPlugin.TypeCustom, failure!.Type);
        Assert.Equal(125, failure.StrRef);
    }

    [DataFact]
    public void ElcEnforcesEventScriptsAndDialogOnALegalCharacter()
    {
        var w = ScriptWorld();
        var pc = new Creature { Conversation = "chat" };
        pc.Scripts["ScriptHeartbeat"] = "mine";
        w.Register(pc);
        var o = ElcPlugin.OptionsOf(w);
        o.EnforceDefaultEventScripts = o.EnforceEmptyDialog = true;
        Assert.Null(ElcPlugin.Validate(w, pc, Array.Empty<RuleError>()));
        Assert.Equal("default", pc.Script("ScriptHeartbeat"));
        Assert.Equal("default", pc.Script("ScriptDeath"));
        Assert.Equal("", pc.Conversation);
    }

    [Fact]
    public void MaxLevelComesFromTheEnvironmentWithinItsRange()
    {
        Assert.Equal(40, CreatureRules.MaxLevelFrom(null));
        Assert.Equal(40, CreatureRules.MaxLevelFrom("40"));
        Assert.Equal(45, CreatureRules.MaxLevelFrom(" 45 "));
        Assert.Equal(60, CreatureRules.MaxLevelFrom("60"));
        Assert.Equal(40, CreatureRules.MaxLevelFrom("61"));
        Assert.Equal(40, CreatureRules.MaxLevelFrom("fifty"));
    }

    [DataFact]
    public void SkillFeatsRoundTripAndFeedTheRank()
    {
        var w = ScriptWorld();
        var c = new Creature { RulesInitialised = true };
        c.Skills = new int[w.Resources.TwoDA("skills").RowCount];
        c.Classes.Add(new CreatureClass { Class = 4, Level = 6 });
        const int heal = 4;
        int before = CreatureRules.SkillRank(w, c, heal);

        // A new skill feat: +3 Heal and half the fighter levels.
        Call(w, "SkillRanks", "SetSkillFeat", NwnxValue.Int(1), NwnxValue.Int(0), NwnxValue.Int(0), NwnxValue.Int(0), NwnxValue.Int(0),
            NwnxValue.Int(0), NwnxValue.Float(0.5f), NwnxValue.String("10000"), NwnxValue.Int(0), NwnxValue.Int(3),
            NwnxValue.Int(NwRules.FeatArtist), NwnxValue.Int(heal));
        c.Feats.Add(NwRules.FeatArtist);
        Assert.Equal(before + 3 + 3, CreatureRules.SkillRank(w, c, heal));

        Call(w, "SkillRanks", "GetSkillFeat", NwnxValue.Int(NwRules.FeatArtist), NwnxValue.Int(heal));
        var reg = w.System<NwnxRegistry>();
        Assert.Equal(3, reg.PopAs(NwnxType.Int));
        Assert.Equal(0, reg.PopAs(NwnxType.Int));
        Assert.Equal("10000", reg.PopAs(NwnxType.String));
        Assert.Equal(0.5f, reg.PopAs(NwnxType.Float));
        reg.Stack.Clear();

        // An area modifier counts for everyone in the area.
        var area = new OpenWinter.Game.Areas.Area { ResRef = "lib", Width = 1, Height = 1 };
        w.Register(area);
        w.Areas.Add(area);
        w.AddToArea(c, area, new System.Numerics.Vector3(1, 1, 0), 0, exact: true);
        Call(w, "SkillRanks", "SetAreaModifier", NwnxValue.Int(10), NwnxValue.Int(heal), NwnxValue.Object(area.Id));
        Assert.Equal(before + 16, CreatureRules.SkillRank(w, c, heal));
    }

    [DataFact]
    public void WeaponFeatsForOtherBaseItemsCountOnce()
    {
        var w = ScriptWorld();
        var rules = (CreatureCombatRules)w.System<CombatSystem>().Rules;
        var c = new Creature { RulesInitialised = true };
        c.Feats.Add(90); // Weapon Focus (dagger)
        var shortsword = rules.Stats(0);
        Assert.False(rules.HasWeaponFeat(c, shortsword, WeaponFeat.Focus));
        Call(w, "Weapon", "SetWeaponFocusFeat", NwnxValue.Int(90), NwnxValue.Int(0));
        Assert.True(rules.HasWeaponFeat(c, shortsword, WeaponFeat.Focus));
        Call(w, "Weapon", "SetGreaterWeaponFocusFeat", NwnxValue.Int(90), NwnxValue.Int(0));
        Call(w, "Weapon", "SetOption", NwnxValue.Int(3), NwnxValue.Int(0));
        Assert.Equal(3, rules.Overrides.GreaterFocusAttackBonus);
        Call(w, "Weapon", "SetWeaponIsMonkWeapon", NwnxValue.Int(0));
        Assert.Contains(0, rules.Overrides.MonkWeapons);
        Call(w, "Weapon", "SetWeaponFinesseSize", NwnxValue.Int(3), NwnxValue.Int(3));
        Call(w, "Weapon", "GetWeaponFinesseSize", NwnxValue.Int(3));
        Assert.Equal(3, w.System<NwnxRegistry>().PopAs(NwnxType.Int));
        Assert.True(rules.Finessable(new Creature { Size = 3 }, rules.Stats(3)));
        Assert.False(rules.Finessable(new Creature { Size = 2 }, rules.Stats(3)));
    }

    [DataFact]
    public void DamageEventDataRoundTripsThroughTheStack()
    {
        var w = ScriptWorld();
        var target = new Creature { RulesInitialised = true, CurrentHitPoints = 50, MaxHitPoints = 50 };
        w.Register(target);
        // The script doubles the fire and drops the cold.
        Script(w, "dmg_ev", "NWNXCall(\"NWNX_Damage\",\"GetDamageEventData\");object d=NWNXPopObject();int i;int v;int fire;"
            + "for(i=0;i<32;i++){v=NWNXPopInt();if(i==8)fire=v;SetLocalInt(GetModule(),\"v\"+IntToString(i),v);}"
            + "SetLocalInt(GetModule(),\"spell\",NWNXPopInt());"
            + "for(i=31;i>=0;i--){v=GetLocalInt(GetModule(),\"v\"+IntToString(i));if(i==8)v=fire*2;if(i==5)v=-1;NWNXPushInt(v);}"
            + "NWNXCall(\"NWNX_Damage\",\"SetDamageEventData\");");
        Call(w, "Damage", "SetEventScript", NwnxValue.Object(GameObject.InvalidId), NwnxValue.String("dmg_ev"), NwnxValue.String("DAMAGE"));
        var changed = DamagePlugin.RunDamageEvent(w, target, GameObject.InvalidId, new Dictionary<int, int> { [256] = 4, [32] = 3 }, 107);
        Assert.NotNull(changed);
        Assert.Equal(8, changed![256]);
        Assert.False(changed.ContainsKey(32));
        Assert.Equal(107, w.Module.Locals.Ints["spell"]);
        Assert.Equal(-1, w.Module.Locals.Ints["v0"]);
    }
}
