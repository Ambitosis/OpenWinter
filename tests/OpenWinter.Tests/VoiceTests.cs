using System.Buffers.Binary;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class VoiceTests
{
    static byte[] Ssf()
    {
        var data = new byte[88];
        "SSF V1.0"u8.CopyTo(data);
        void U(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at), value);
        U(8,2); U(12,40); U(40,68); U(44,48); // deliberately reverse data order
        "second"u8.CopyTo(data.AsSpan(48)); U(64,uint.MaxValue);
        "first"u8.CopyTo(data.AsSpan(68)); U(84,0x01000042);
        return data;
    }
    [Fact] public void SsfUsesOffsetsAndPreservesCustomStringReferences()
    {
        var set=SoundSet.Read(Ssf());
        Assert.Equal(new SoundSet.Entry("first",0x01000042),set.Get(0));
        Assert.Equal(new SoundSet.Entry("second",uint.MaxValue),set.Get(1));
        Assert.Equal(new SoundSet.Entry("",uint.MaxValue),set.Get(-1));
        Assert.Equal(new SoundSet.Entry("",uint.MaxValue),set.Get(2));
    }
    [Theory] [InlineData(8)] [InlineData(12)] [InlineData(40)]
    public void SsfRejectsOverflowingCountsAndOffsets(int at)
    {
        var data=Ssf(); BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(at),uint.MaxValue);
        Assert.Throws<InvalidDataException>(()=>SoundSet.Read(data));
    }
    [Fact] public void QuickChatPathsAndCommandsMatchPublishedOriginalSlots()
    {
        var combat=QuickChat.Groups.Single(g=>g.Key=='W');
        Assert.Equal(0,combat.Entries.Single(e=>e.Key=='E').Slot);
        Assert.Equal(34,QuickChat.Groups.Single(g=>g.Key=='S').Entries.Single(e=>e.Key=='S').Slot);
        Assert.Equal(4713u,combat.Entries.Single(e=>e.Key=='D').StringRef);
        Assert.Equal(-3,QuickChat.AssociateCommand(0)); Assert.Equal(-5,QuickChat.AssociateCommand(22));
        Assert.False(QuickChat.IsSelectable(18)); Assert.False(QuickChat.IsSelectable(-1));
    }
    [DataFact] public void AllInstalledSoundsetsParseAndAllAuthoredChantsExist()
    {
        var resources=ResMan.ForClient(TestData.Paths,[],"");
        var table=resources.TwoDA("soundset"); int parsed=0;
        for(int row=0;row<table.RowCount;row++)
            if(resources.Read(table.Get(row,"RESREF"),ResType.Ssf) is {} bytes)
            { Assert.Equal(49,SoundSet.Read(bytes).Entries.Count); parsed++; }
        Assert.True(parsed>300);
        var spells=resources.TwoDA("spells"); int chants=0;
        foreach(string col in new[]{"ConjSoundMale","ConjSoundFemale"})
            for(int row=0;row<spells.RowCount;row++)
                if(spells.Get(row,col) is {Length:>0} sound) { Assert.True(resources.Contains(sound,ResType.Wav),$"{row}/{col}: {sound}"); chants++; }
        Assert.True(chants>800);
    }
    [DataFact] public void CurrentNpcSoundsetIsUsedAndQuickChatCannotChooseDeathBarks()
    {
        var resources=ResMan.ForClient(TestData.Paths,[],"");
        var world=new World(resources); var output=new Output(); world.Output=output;
        var area=new Area(); world.Register(area); world.Areas.Add(area);
        var pc=world.Creatures.FromTemplate(new GffStruct()); world.AddToArea(pc,area,default,0);
        var table=resources.TwoDA("soundset");
        int first=Enumerable.Range(0,table.RowCount).First(r=>VoiceService.Resolve(resources,r,34).Sound.Length>0);
        int second=Enumerable.Range(first+1,table.RowCount-first-1).First(r=>VoiceService.Resolve(resources,r,34).Sound.Length>0);
        pc.SoundSet=first; VoiceService.Play(world,pc,34);
        Assert.Equal(VoiceService.Resolve(resources,first,34).Sound,output.Notes.Last().GetProperty("sound").GetString());
        pc.SoundSet=second; VoiceService.Play(world,pc,34);
        Assert.Equal(VoiceService.Resolve(resources,second,34).Sound,output.Notes.Last().GetProperty("sound").GetString());
        int count=output.Notes.Count;
        ClientRequests.Handle(world,pc,"voice-chat","{\"id\":18}"); Assert.Equal(count,output.Notes.Count);
        ClientRequests.Handle(world,pc,"voice-chat","{\"id\":34}"); Assert.Equal(count+1,output.Notes.Count);
        pc.IsDead=true; ClientRequests.Handle(world,pc,"voice-chat","{\"id\":34}"); Assert.Equal(count+1,output.Notes.Count);
        pc.IsDead=false;
        var hench=world.Creatures.FromTemplate(new GffStruct()); world.AddToArea(hench,area,default,0); hench.Master=pc.Id;
        var other=world.Creatures.FromTemplate(new GffStruct()); world.AddToArea(other,area,default,0);
        foreach(var (slot,command) in new[]{(0,-3),(4,-4),(9,-7),(10,-2),(22,-5)})
        {
            ClientRequests.Handle(world,pc,"voice-chat",JsonSerializer.Serialize(new {id=slot}));
            Assert.Equal(command,hench.Society.LastAssociateCommand);
            Assert.Equal(0,other.Society.LastAssociateCommand);
        }

    }
    [DataFact] public void AutomaticDeathUsesDeathSlotOnceWithoutScriptCaption()
    {
        var resources=ResMan.ForClient(TestData.Paths,[],"");
        var world=new World(resources); var output=new Output(); world.Output=output;
        var area=new Area(); world.Register(area); world.Areas.Add(area);
        var creature=world.Creatures.FromTemplate(new GffStruct()); world.AddToArea(creature,area,default,0);
        creature.SoundSet=Enumerable.Range(0,resources.TwoDA("soundset").RowCount)
            .First(r=>VoiceService.Resolve(resources,r,18).Sound.Length>0);
        OpenWinter.Game.Rules.HitPoints.Die(world,creature,GameObject.InvalidId);
        var note=Assert.Single(output.Notes);
        Assert.Equal(18,note.GetProperty("id").GetInt32());
        Assert.Equal(VoiceService.Resolve(resources,creature.SoundSet,18).Sound,note.GetProperty("sound").GetString());
        Assert.Empty(output.Speech);
        OpenWinter.Game.Rules.HitPoints.Die(world,creature,GameObject.InvalidId);
        Assert.Single(output.Notes);
    }
    [DataFact] public void AttackGruntsUseCurrentSoundsetOnMissesWithoutConsumingCombatDiceOrCaptions()
    {
        var resources=ResMan.ForClient(TestData.Paths,[],"");
        var sets=Enumerable.Range(0,resources.TwoDA("soundset").RowCount)
            .Where(r=>Enumerable.Range(11,3).All(slot=>VoiceService.Resolve(resources,r,slot).Sound.Length>0)).Take(2).ToArray();
        Assert.Equal(2,sets.Length);
        foreach(bool player in new[]{false,true})
        foreach(bool ranged in new[]{false,true})
        {
            var world=new World(resources); var output=new Output(); world.Output=output;
            var area=new Area(); world.Register(area); world.Areas.Add(area);
            var attacker=world.Creatures.FromTemplate(new GffStruct());
            var (_,serverEnd)=LoopbackConnection.CreatePair();
            if(player) attacker.Player=new PlayerSession { Connection=serverEnd };
            var target=world.Creatures.FromTemplate(new GffStruct());
            world.AddToArea(attacker,area,default,0); world.AddToArea(target,area,new(0,1,0),0);
            var combat=world.System<CombatSystem>();
            int choices=ranged?15:6, audible=ranged?6:3;
            Item? bow=null;
            if(ranged)
            {
                bow=new Item { BaseItem=8, Possessor=attacker.Id };
                var arrows=new Item { BaseItem=20, StackSize=choices, Possessor=attacker.Id };
                world.Register(bow); world.Register(arrows);
                attacker.Equipment[EquipSlot.RightHand]=bow;
                attacker.Equipment[EquipSlot.Arrows]=arrows;
            }
            var combatDice=new ScriptedDice(1,Enumerable.Repeat(1,choices).ToArray());
            var voiceDice=new ScriptedDice(1,Enumerable.Range(1,choices).ToArray());
            combat.Dice=combatDice; combat.VoiceDice=voiceDice;
            for(int i=0;i<choices;i++)
            {
                attacker.SoundSet=sets[i%2];
                var outcome=combat.Resolve(combat.Rules.Context(attacker,target,bow,false,combat.Dice));
                Assert.Equal(1,outcome.Roll); Assert.False(outcome.Hit);
                Assert.Equal(ranged,outcome.Context.Ranged);
                Assert.Equal(Math.Min(i+1,audible),output.Notes.Count);
                if(i<audible)
                {
                    Assert.Equal(11+i%3,output.Notes[i].GetProperty("id").GetInt32());
                    Assert.Equal(VoiceService.Resolve(resources,attacker.SoundSet,11+i%3).Sound,
                        output.Notes[i].GetProperty("sound").GetString());
                }
            }
            Assert.Equal(0,combatDice.Remaining);
            Assert.Equal(0,voiceDice.Remaining);
            if(ranged)
            {
                var unusedCombat=new ScriptedDice(1,20); var unusedVoice=new ScriptedDice(1,1);
                combat.Dice=unusedCombat; combat.VoiceDice=unusedVoice;
                var noAmmo=combat.Resolve(combat.Rules.Context(attacker,target,bow,false,combat.Dice));
                Assert.True(noAmmo.OutOfAmmo); Assert.Equal(audible,output.Notes.Count);
                Assert.Equal(1,unusedCombat.Remaining); Assert.Equal(1,unusedVoice.Remaining);
            }
            Assert.Empty(output.Speech);
        }
    }
    [DataFact] public void DamagingMeleePainUsesVictimsCurrentSoundsetOnceWithoutCombatDiceOrCaptions()
    {
        var resources=ResMan.ForClient(TestData.Paths,[], "");
        var sets=Enumerable.Range(0,resources.TwoDA("soundset").RowCount)
            .Where(r=>Enumerable.Range(14,3).All(slot=>VoiceService.Resolve(resources,r,slot).Sound.Length>0)).Take(2).ToArray();
        Assert.Equal(2,sets.Length);
        foreach(bool player in new[]{false,true})
        {
            var world=new World(resources); var output=new Output(); world.Output=output;
            var area=new Area(); world.Register(area); world.Areas.Add(area);
            var attacker=world.Creatures.FromTemplate(new GffStruct());
            attacker.Classes.Add(new CreatureClass { Class=4, Level=20 });
            var target=world.Creatures.FromTemplate(new GffStruct());
            target.MaxHitPoints=target.CurrentHitPoints=100;
            if(player) { var (_,connection)=LoopbackConnection.CreatePair(); target.Player=new PlayerSession { Connection=connection }; }
            world.AddToArea(attacker,area,default,0); world.AddToArea(target,area,new(0,1,0),0);
            var combat=world.System<CombatSystem>();
            var rolls=new ScriptedDice(1,10,1,10,1,10,1,10,1);
            var voices=new ScriptedDice(1,6,1,1,6,1,2,6,1,3,6,100);
            combat.Dice=rolls; combat.VoiceDice=voices;
            for(int i=0;i<4;i++)
            {
                target.SoundSet=sets[i%2];
                var outcome=combat.Resolve(combat.Rules.Context(attacker,target,null,false,rolls));
                Assert.True(outcome.Hit); Assert.True(outcome.Dealt>0); Assert.False(outcome.Killed);
                Assert.Equal(Math.Min(i+1,3),output.Notes.Count);
                if(i<3)
                {
                    Assert.Equal(target,output.Subjects[i]);
                    Assert.Equal(14+i,output.Notes[i].GetProperty("id").GetInt32());
                    Assert.Equal(VoiceService.Resolve(resources,target.SoundSet,14+i).Sound,
                        output.Notes[i].GetProperty("sound").GetString());
                }
            }
            Assert.Equal(0,rolls.Remaining); Assert.Equal(0,voices.Remaining);
            Assert.Empty(output.Speech);
        }
    }
    [DataFact] public void ScriptedDamageAndAbsorbedMeleeDoNotEmitPainOrRollItsChance()
    {
        var resources=ResMan.ForClient(TestData.Paths,[], "");
        var world=new World(resources); var output=new Output(); world.Output=output;
        var area=new Area(); world.Register(area); world.Areas.Add(area);
        var attacker=world.Creatures.FromTemplate(new GffStruct());
        attacker.Classes.Add(new CreatureClass { Class=4, Level=20 });
        var target=world.Creatures.FromTemplate(new GffStruct());
        target.MaxHitPoints=target.CurrentHitPoints=100;
        target.SoundSet=Enumerable.Range(0,resources.TwoDA("soundset").RowCount)
            .First(r=>VoiceService.Resolve(resources,r,14).Sound.Length>0);
        world.AddToArea(attacker,area,default,0); world.AddToArea(target,area,new(0,1,0),0);
        var combat=world.System<CombatSystem>();
        var voices=new ScriptedDice(1,6,1); combat.VoiceDice=voices;
        Assert.True(HitPoints.Damage(world,target,attacker.Id,5,CombatK.DamageSlashing)>0);
        Assert.Empty(output.Notes); Assert.Equal(2,voices.Remaining);
        target.Plot=true;
        combat.Dice=new ScriptedDice(1,10,1);
        var outcome=combat.Resolve(combat.Rules.Context(attacker,target,null,false,combat.Dice));
        Assert.True(outcome.Hit); Assert.Equal(0,outcome.Dealt);
        Assert.Empty(output.Notes); Assert.Equal(1,voices.Remaining);
    }
    [DataFact] public void DamagingLongbowHitDoesNotEmitOrRollMeleePain()
    {
        var resources=ResMan.ForClient(TestData.Paths,[], "");
        var world=new World(resources); var output=new Output(); world.Output=output;
        var area=new Area(); world.Register(area); world.Areas.Add(area);
        var attacker=world.Creatures.FromTemplate(new GffStruct());
        attacker.Classes.Add(new CreatureClass { Class=4, Level=20 });
        var target=world.Creatures.FromTemplate(new GffStruct());
        target.MaxHitPoints=target.CurrentHitPoints=100;
        target.SoundSet=Enumerable.Range(0,resources.TwoDA("soundset").RowCount)
            .First(r=>VoiceService.Resolve(resources,r,14).Sound.Length>0);
        world.AddToArea(attacker,area,default,0); world.AddToArea(target,area,new(0,6,0),0);
        var bow=new Item { BaseItem=8, Possessor=attacker.Id };
        var arrows=new Item { BaseItem=20, StackSize=2, Possessor=attacker.Id };
        world.Register(bow); world.Register(arrows);
        attacker.Equipment[EquipSlot.RightHand]=bow; attacker.Equipment[EquipSlot.Arrows]=arrows;
        var combat=world.System<CombatSystem>();
        var voices=new ScriptedDice(1,15,1); combat.VoiceDice=voices;
        combat.Dice=new ScriptedDice(1,10,1);
        var outcome=combat.Resolve(combat.Rules.Context(attacker,target,bow,false,combat.Dice));
        Assert.True(outcome.Context.Ranged); Assert.True(outcome.Hit); Assert.True(outcome.Dealt>0);
        Assert.Equal(1,arrows.StackSize); Assert.Empty(output.Notes);
        Assert.Equal(1,voices.Remaining);
    }
    [DataFact] public void FatalMeleeUsesOnlyDeathVoiceWithoutRollingPainChance()
    {
        var resources=ResMan.ForClient(TestData.Paths,[], "");
        var world=new World(resources); var output=new Output(); world.Output=output;
        var area=new Area(); world.Register(area); world.Areas.Add(area);
        var attacker=world.Creatures.FromTemplate(new GffStruct());
        attacker.Classes.Add(new CreatureClass { Class=4, Level=20 });
        var target=world.Creatures.FromTemplate(new GffStruct());
        target.MaxHitPoints=target.CurrentHitPoints=1;
        target.SoundSet=Enumerable.Range(0,resources.TwoDA("soundset").RowCount)
            .First(r=>VoiceService.Resolve(resources,r,18).Sound.Length>0);
        world.AddToArea(attacker,area,default,0); world.AddToArea(target,area,new(0,1,0),0);
        var combat=world.System<CombatSystem>();
        var voices=new ScriptedDice(1,6,1); combat.VoiceDice=voices;
        combat.Dice=new ScriptedDice(1,10,1);
        var outcome=combat.Resolve(combat.Rules.Context(attacker,target,null,false,combat.Dice));
        Assert.True(outcome.Hit); Assert.True(outcome.Killed); Assert.True(target.IsDead);
        var note=Assert.Single(output.Notes);
        Assert.Equal(18,note.GetProperty("id").GetInt32());
        Assert.Equal(target,Assert.Single(output.Subjects));
        Assert.Equal(1,voices.Remaining); Assert.Empty(output.Speech);
    }
    sealed class Output : IWorldOutput
    {
        public readonly List<JsonElement> Notes=[];
        public readonly List<string> Speech=[];
        public readonly List<GameObject?> Subjects=[];
        public void Notify(string kind,GameObject? subject,object data,Creature? player=null) { if(kind=="voice-chat") { Notes.Add(JsonSerializer.SerializeToElement(data)); Subjects.Add(subject); } }
        public void Speak(GameObject speaker,string text,int volume) { Speech.Add(text); }
        public void PlayAnimation(GameObject o,int animation,float speed,float duration) { }
        public void SendMessage(Creature pc,string text) { }
        public void FloatingText(Creature over,string text,bool toFaction) { }
    }
}
