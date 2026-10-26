using System.Numerics;

namespace OpenWinter.Net;

public enum ObjectKind : byte { Creature, Placeable, Door, Item, Trigger, Waypoint, Sound, AreaOfEffect, Store, Encounter }

public sealed class HelloMessage
{
    public string PlayerName = "";
    /// <summary>Development shortcut: a localvault character to play without the selection screen.</summary>
    public string CharacterResRef = "";
    /// <summary>The player's persistent public key (like a CD key): identifies the player across sessions.</summary>
    public string PlayerKey = "";
    /// <summary>Log in as a Dungeon Master (the server checks <see cref="DMPassword"/>).</summary>
    public bool DM;
    public string DMPassword = "";
    public bool AreaReadyHandshake;
    /// <summary>Arriving through a portal (ActivatePortal's sWaypointTag): the player starts at the waypoint with this tag.</summary>
    public string PortalWaypoint = "";
    /// <summary>The server's player password, when it has one.</summary>
    public string Password = "";

    public byte[] Write() => new MessageWriter(MessageType.Hello).Str(PlayerName).Str(CharacterResRef).Str(PlayerKey).Bool(DM).Str(DMPassword).Bool(AreaReadyHandshake).Str(PortalWaypoint).Str(Password).ToArray();
    public static HelloMessage Read(MessageReader r)
    {
        var m = new HelloMessage { PlayerName = r.Str(), CharacterResRef = r.Str(), PlayerKey = r.Remaining > 0 ? r.Str() : "" };
        if (r.Remaining > 0) { m.DM = r.Bool(); m.DMPassword = r.Str(); }
        if (r.Remaining > 0) m.AreaReadyHandshake = r.Bool();
        if (r.Remaining > 0) m.PortalWaypoint = r.Str();
        if (r.Remaining > 0) m.Password = r.Str();
        return m;
    }
}

public sealed class WelcomeMessage
{
    public string ModuleName = "";
    public string CustomTlk = "";
    public List<string> Haks = new();
    /// <summary>Files the server can send (haks, custom TLK) with sizes and hashes; empty when it serves none.</summary>
    public List<ContentFile> Files = new();

    public byte[] Write()
    {
        var w = new MessageWriter(MessageType.Welcome).Str(ModuleName).Str(CustomTlk).I32(Haks.Count);
        foreach (var h in Haks) w.Str(h);
        w.I32(Files.Count);
        foreach (var f in Files) w.U8((byte)f.Kind).Str(f.Name).I64(f.Size).Str(f.Sha1);
        return w.ToArray();
    }

    public static WelcomeMessage Read(MessageReader r)
    {
        var m = new WelcomeMessage { ModuleName = r.Str(), CustomTlk = r.Str() };
        int n = r.Count();
        for (int i = 0; i < n; i++) m.Haks.Add(r.Str());
        if (r.Remaining > 0)
        {
            int files = r.Count(11);
            for (int i = 0; i < files; i++) m.Files.Add(new ContentFile { Kind = (ContentKind)r.U8(), Name = r.Str(), Size = r.I64(), Sha1 = r.Str() });
        }
        return m;
    }
}

public enum ContentKind : byte { Hak, Tlk, ModuleResources }

/// <summary>A downloadable file: what it is, its size and SHA-1 (hex) so clients can tell whether theirs matches.</summary>
public sealed class ContentFile
{
    public ContentKind Kind;
    public string Name = "";
    public long Size;
    public string Sha1 = "";
}

/// <summary>Client → server: send me this file (by hash).</summary>
public sealed class ContentRequestMessage
{
    public string Sha1 = "";
    public byte[] Write() => new MessageWriter(MessageType.ContentRequest).Str(Sha1).ToArray();
    public static ContentRequestMessage Read(MessageReader r) => new() { Sha1 = r.Str() };
}

/// <summary>Server → client: one piece of a requested file.</summary>
public sealed class ContentChunkMessage
{
    public string Sha1 = "";
    public long Offset, Total;
    public byte[] Data = Array.Empty<byte>();
    public byte[] Write() => new MessageWriter(MessageType.ContentChunk).Str(Sha1).I64(Offset).I64(Total).Bytes(Data).ToArray();
    public static ContentChunkMessage Read(MessageReader r) => new() { Sha1 = r.Str(), Offset = r.I64(), Total = r.I64(), Data = r.Bytes() };
}

public struct TileInfo
{
    public int Id;
    public byte Orientation;
    public sbyte Height;
    public byte MainLight1, MainLight2, SourceLight1, SourceLight2;
    public byte AnimLoops;
}

/// <summary>What a client needs to draw an area: everything static. Objects arrive separately as spawns.</summary>
public sealed class LoadAreaMessage
{
    public uint AreaId;
    public uint LoadToken;
    public string ResRef = "";
    public string Name = "";
    public string Tileset = "";
    public int Width, Height;
    public TileInfo[] Tiles = Array.Empty<TileInfo>();
    public bool IsNight, IsInterior, IsUnderground, IsNatural;
    public uint SunAmbient, SunDiffuse, SunFogColor, MoonAmbient, MoonDiffuse, MoonFogColor;
    public byte SunFogAmount, MoonFogAmount;
    public bool SunShadows, MoonShadows;
    public float FogClipDistance;
    public string SkyBox = "";
    /// <summary>Rows in ambientmusic.2da / ambientsound.2da (0 = none); volumes 0-127; delay in ms between music tracks.</summary>
    public int MusicDay, MusicNight, MusicBattle, AmbientDay, AmbientNight, AmbientDayVolume, AmbientNightVolume, MusicDelay;
    /// <summary>Lighting follows the game clock (otherwise IsNight fixes it).</summary>
    public bool DayNightCycle;
    /// <summary>Row in loadscreens.2da shown while the area loads (the ARE's LoadScreenID; 0 = pick by tileset).</summary>
    public int LoadScreen;
    /// <summary>How dark shadows are, 0-100 (the ARE's ShadowOpacity): shadowed ground keeps the rest of the light.</summary>
    public byte ShadowOpacity = 60;

    public byte[] Write()
    {
        var w = new MessageWriter(MessageType.LoadArea).U32(AreaId).Str(ResRef).Str(Name).Str(Tileset).I32(Width).I32(Height)
            .Bool(IsNight).Bool(IsInterior).Bool(IsUnderground).Bool(IsNatural)
            .U32(SunAmbient).U32(SunDiffuse).U32(SunFogColor).U32(MoonAmbient).U32(MoonDiffuse).U32(MoonFogColor)
            .U8(SunFogAmount).U8(MoonFogAmount).Bool(SunShadows).Bool(MoonShadows).F32(FogClipDistance).Str(SkyBox)
            .I32(MusicDay).I32(MusicNight).I32(MusicBattle).I32(AmbientDay).I32(AmbientNight).I32(AmbientDayVolume).I32(AmbientNightVolume).I32(MusicDelay).Bool(DayNightCycle)
            .I32(Tiles.Length);
        foreach (var t in Tiles)
            w.I32(t.Id).U8(t.Orientation).U8((byte)t.Height).U8(t.MainLight1).U8(t.MainLight2).U8(t.SourceLight1).U8(t.SourceLight2).U8(t.AnimLoops);
        w.I32(LoadScreen).U8(ShadowOpacity).U32(LoadToken);
        return w.ToArray();
    }

    public static LoadAreaMessage Read(MessageReader r)
    {
        var m = new LoadAreaMessage
        {
            AreaId = r.U32(), ResRef = r.Str(), Name = r.Str(), Tileset = r.Str(), Width = r.I32(), Height = r.I32(),
            IsNight = r.Bool(), IsInterior = r.Bool(), IsUnderground = r.Bool(), IsNatural = r.Bool(),
            SunAmbient = r.U32(), SunDiffuse = r.U32(), SunFogColor = r.U32(), MoonAmbient = r.U32(), MoonDiffuse = r.U32(), MoonFogColor = r.U32(),
            SunFogAmount = r.U8(), MoonFogAmount = r.U8(), SunShadows = r.Bool(), MoonShadows = r.Bool(), FogClipDistance = r.F32(), SkyBox = r.Str(),
            MusicDay = r.I32(), MusicNight = r.I32(), MusicBattle = r.I32(), AmbientDay = r.I32(), AmbientNight = r.I32(),
            AmbientDayVolume = r.I32(), AmbientNightVolume = r.I32(), MusicDelay = r.I32(), DayNightCycle = r.Bool(),
        };
        m.Tiles = new TileInfo[r.Count(11)];
        for (int i = 0; i < m.Tiles.Length; i++)
            m.Tiles[i] = new TileInfo
            {
                Id = r.I32(), Orientation = r.U8(), Height = (sbyte)r.U8(), MainLight1 = r.U8(), MainLight2 = r.U8(),
                SourceLight1 = r.U8(), SourceLight2 = r.U8(), AnimLoops = r.U8(),
            };
        if (r.Remaining >= 4) m.LoadScreen = r.I32();
        if (r.Remaining >= 1) m.ShadowOpacity = r.U8();
        if (r.Remaining >= 4) m.LoadToken = r.U32();
        return m;
    }
}

/// <summary>How an object looks. Which fields matter depends on the kind.</summary>
public sealed class Appearance
{
    /// <summary>Row in appearance.2da (creatures), placeables.2da (placeables) or doortypes.2da (doors, 0 = generic).</summary>
    public int Type;
    public int GenericDoorType;
    public byte Gender, Phenotype;
    /// <summary>Part numbers in CreatureFactory.BodyPartFields order. EE allows numbers above 255.</summary>
    public ushort[] BodyParts = Array.Empty<ushort>();
    /// <summary>Skin, hair, tattoo 1, tattoo 2.</summary>
    public byte[] Colors = Array.Empty<byte>();
    public int Tail, Wings;
    public float Scale = 1f;
    /// <summary>Equipped visuals as (slot, base item, model parts, colours).</summary>
    public List<ItemVisual> Equipment = new();

    public void Write(MessageWriter w)
    {
        w.I32(Type).I32(GenericDoorType).U8(Gender).U8(Phenotype).U16s(BodyParts).Bytes(Colors).I32(Tail).I32(Wings).F32(Scale).I32(Equipment.Count);
        foreach (var e in Equipment) e.Write(w);
    }

    public static Appearance Read(MessageReader r)
    {
        var a = new Appearance { Type = r.I32(), GenericDoorType = r.I32(), Gender = r.U8(), Phenotype = r.U8(), BodyParts = r.U16s(), Colors = r.Bytes(), Tail = r.I32(), Wings = r.I32(), Scale = r.F32() };
        int n = r.Count(22);
        for (int i = 0; i < n; i++) a.Equipment.Add(ItemVisual.Read(r));
        return a;
    }
}

public sealed class ItemVisual
{
    public int Slot;
    public int BaseItem;
    public ushort[] ModelParts = Array.Empty<ushort>();
    public byte[] Colors = Array.Empty<byte>();
    /// <summary>Armour part numbers in body part order, then robe; 0 = none.</summary>
    public ushort[] ArmorParts = Array.Empty<ushort>();
    /// <summary>The item's Light property: colour (iprp_color.2da row, 255 = no light) and brightness (iprp_lightcost.2da row).</summary>
    public byte LightColor = NoLight, LightLevel;
    public const byte NoLight = 255;

    public void Write(MessageWriter w) => w.I32(Slot).I32(BaseItem).U16s(ModelParts).Bytes(Colors).U16s(ArmorParts).U8(LightColor).U8(LightLevel);
    public static ItemVisual Read(MessageReader r) => new() { Slot = r.I32(), BaseItem = r.I32(), ModelParts = r.U16s(), Colors = r.Bytes(), ArmorParts = r.U16s(), LightColor = r.U8(), LightLevel = r.U8() };
}

[Flags]
public enum ObjectFlags : uint
{
    None = 0,
    Static = 1,
    Useable = 2,
    Open = 4,
    Locked = 8,
    Plot = 16,
    Dead = 32,
    Hostile = 64,
    Selectable = 128,
    /// <summary>Placeable switched on (lights, fires): plays its "on" animation.</summary>
    Activated = 256,
    /// <summary>Creature in a conversation (the original lowers a held torch meanwhile).</summary>
    Talking = 512,
    /// <summary>Door with a destination or OnClick handler; an open door uses it on left click.</summary>
    Transition = 1024,
    /// <summary>Retain the opening side for newly arriving clients.</summary>
    OpenSide2 = 2048,
    /// <summary>Creature asleep (EffectSleep): falls forward and lies face down until it wakes.</summary>
    Asleep = 4096,
    /// <summary>Creature knocked down (EffectKnockdown): falls back and stays sitting up until it ends.</summary>
    KnockedDown = 8192,
    /// <summary>Creature held (EffectParalyze): its animation stops where it is.</summary>
    Held = 16384,
    /// <summary>Creature petrified (EffectPetrify): frozen, with the stone skin (progfx Petrify).</summary>
    Petrified = 32768,
    /// <summary>Creature stunned (EffectStunned): stars circle its head.</summary>
    Stunned = 65536,
    /// <summary>With KnockedDown: knocked over forwards (the knockdown came from behind it), so it falls on its front.</summary>
    KnockedForward = 131072,
    /// <summary>Creature invisible, in sanctuary or ethereal: its own player sees it half transparent.</summary>
    Translucent = 262144,
    /// <summary>Creature blind or in darkness: its own player's view of the world goes nearly black.</summary>
    Blinded = 524288,
    /// <summary>Player character dying (0 to -9 hit points): lies in the death pose until healed, raised or dead.</summary>
    Dying = 1048576,
}

public sealed class SpawnMessage
{
    public uint ObjectId;
    public ObjectKind Kind;
    public string Tag = "";
    public string Name = "";
    public Vector3 Position;
    public float Facing;
    public ObjectFlags Flags;
    public Appearance Appearance = new();
    /// <summary>Walk/run cycle distances and speeds, so clients animate in step with the server.</summary>
    public float WalkSpeed, RunSpeed;
    public string Portrait = "";
    /// <summary>Kind-specific JSON (sound objects: their sound list and playback settings).</summary>
    public string Extra = "";

    public byte[] Write()
    {
        var w = new MessageWriter(MessageType.Spawn).U32(ObjectId).U8((byte)Kind).Str(Tag).Str(Name).V3(Position).F32(Facing).U32((uint)Flags);
        Appearance.Write(w);
        return w.F32(WalkSpeed).F32(RunSpeed).Str(Portrait).Str(Extra).ToArray();
    }

    public static SpawnMessage Read(MessageReader r) => new()
    {
        ObjectId = r.U32(), Kind = (ObjectKind)r.U8(), Tag = r.Str(), Name = r.Str(), Position = r.V3(), Facing = r.F32(),
        Flags = (ObjectFlags)r.U32(), Appearance = Appearance.Read(r), WalkSpeed = r.F32(), RunSpeed = r.F32(), Portrait = r.Str(), Extra = r.Str(),
    };
}

public enum MovementMode : byte { Stand, Walk, Run, Backward, StrafeLeft, StrafeRight, TurnLeft, TurnRight, WalkForwardLeft, WalkForwardRight, RunForwardLeft, RunForwardRight, MouseWalk, MouseRun }

/// <summary>Frequent state for a moving or changing object.</summary>
public sealed class UpdateMessage
{
    public uint ObjectId;
    public Vector3 Position;
    public float Facing;
    public float Speed;
    public MovementMode Mode;
    public ObjectFlags Flags;
    public bool InCombat;
    public int Surface, FootstepType = -1;

    public byte[] Write() =>
        new MessageWriter(MessageType.Update).U32(ObjectId).V3(Position).F32(Facing).F32(Speed).U8((byte)Mode).U32((uint)Flags).Bool(InCombat).I32(Surface).I32(FootstepType).ToArray();

    public static UpdateMessage Read(MessageReader r) => new()
    {
        ObjectId = r.U32(), Position = r.V3(), Facing = r.F32(), Speed = r.F32(), Mode = (MovementMode)r.U8(), Flags = (ObjectFlags)r.U32(), InCombat = r.Bool(),
        Surface = r.I32(), FootstepType = r.I32(),
    };
}

/// <summary>An object's look changed (equipment, body parts, appearance type): rebuild its model.</summary>
public sealed class AppearanceMessage
{
    public uint ObjectId;
    public Appearance Appearance = new();
    public byte[] Write()
    {
        var w = new MessageWriter(MessageType.Appearance).U32(ObjectId);
        Appearance.Write(w);
        return w.ToArray();
    }
    public static AppearanceMessage Read(MessageReader r) => new() { ObjectId = r.U32(), Appearance = Appearance.Read(r) };
}

/// <summary>A UI request from the client (equip, store-buy, nui-event, ...): a kind plus JSON arguments.</summary>
public sealed class ClientRequestMessage
{
    public string Kind = "";
    public string Json = "";
    public byte[] Write() => new MessageWriter(MessageType.ClientRequest).Str(Kind).Str(Json).ToArray();
    public static ClientRequestMessage Read(MessageReader r) => new() { Kind = r.Str(), Json = r.Str() };
}

public enum VaultMode : byte { Local, Server }

/// <summary>One character in a vault, as the selection screen shows it.</summary>
public sealed class CharacterEntry
{
    public string File = "";
    public string Name = "";
    public string Classes = "";
    public string Portrait = "";
    public int Level;
}

/// <summary>Server → client after Welcome: choose a character (server vault list, or upload one from the local vault).</summary>
public sealed class CharacterListMessage
{
    public VaultMode Mode;
    public bool AllowNew;
    public List<CharacterEntry> Characters = new();

    public byte[] Write()
    {
        var w = new MessageWriter(MessageType.CharacterList).U8((byte)Mode).Bool(AllowNew).U16((ushort)Characters.Count);
        foreach (var c in Characters) w.Str(c.File).Str(c.Name).Str(c.Classes).Str(c.Portrait).U16((ushort)c.Level);
        return w.ToArray();
    }

    public static CharacterListMessage Read(MessageReader r)
    {
        var m = new CharacterListMessage { Mode = (VaultMode)r.U8(), AllowNew = r.Bool() };
        int n = r.U16();
        for (int i = 0; i < n; i++) m.Characters.Add(new CharacterEntry { File = r.Str(), Name = r.Str(), Classes = r.Str(), Portrait = r.Str(), Level = r.U16() });
        return m;
    }
}

/// <summary>Client → server: the chosen character: a server vault file, or a local-vault BIC uploaded whole.</summary>
public sealed class CharacterChoiceMessage
{
    public string File = "";
    public byte[] Bic = Array.Empty<byte>();

    public byte[] Write() => new MessageWriter(MessageType.CharacterChoice).Str(File).Bytes(Bic).ToArray();
    public static CharacterChoiceMessage Read(MessageReader r) => new() { File = r.Str(), Bic = r.Bytes() };
}

public sealed class DespawnMessage
{
    public uint ObjectId;
    public bool DecayingCorpse;
    public byte[] Write() => new MessageWriter(MessageType.Despawn).U32(ObjectId).Bool(DecayingCorpse).ToArray();
    public static DespawnMessage Read(MessageReader r)
    {
        var m = new DespawnMessage { ObjectId = r.U32() };
        if (r.Remaining > 0) m.DecayingCorpse = r.Bool();
        return m;
    }
}

public sealed class ControlObjectMessage
{
    public uint ObjectId;
    public byte[] Write() => new MessageWriter(MessageType.ControlObject).U32(ObjectId).ToArray();
    public static ControlObjectMessage Read(MessageReader r) => new() { ObjectId = r.U32() };
}

public sealed class MoveInputMessage
{
    /// <summary>Desired direction in world XY (zero = stop), magnitude ≤ 1.</summary>
    public Vector3 Direction;
    public bool Walk;

    public byte[] Write() => new MessageWriter(MessageType.MoveInput).V3(Direction).Bool(Walk).ToArray();
    public static MoveInputMessage Read(MessageReader r) => new() { Direction = r.V3(), Walk = r.Bool() };
}

/// <summary>Character-relative keyboard axes, or a held-mouse world destination. Turn is positive left.</summary>
public sealed class DriveInputMessage
{
    public Vector3 Direction;
    public float Turn;
    public bool Walk, Mouse, Active;
    public byte[] Write() => new MessageWriter(MessageType.DriveInput).V3(Direction).F32(Turn).Bool(Walk).Bool(Mouse).Bool(Active).ToArray();
    public static DriveInputMessage Read(MessageReader r) => new() { Direction = r.V3(), Turn = r.F32(), Walk = r.Bool(), Mouse = r.Bool(), Active = r.Bool() };
}

public sealed class PlayAnimationMessage
{
    public uint ObjectId;
    public string Animation = "";
    public float Speed = 1f;
    /// <summary>Seconds for looping animations; 0 for a single play.</summary>
    public float Duration;
    /// <summary>Upper-body layer only.</summary>
    public bool Gesture;

    public byte[] Write() => new MessageWriter(MessageType.PlayAnimation).U32(ObjectId).Str(Animation).F32(Speed).F32(Duration).Bool(Gesture).ToArray();
    public static PlayAnimationMessage Read(MessageReader r) => new() { ObjectId = r.U32(), Animation = r.Str(), Speed = r.F32(), Duration = r.F32(), Gesture = r.Bool() };
}

/// <summary>Dialog: a conversation line or chosen answer, for the chat window only (no speech bubble).</summary>
public enum ChatChannel : byte { Talk, Shout, Whisper, Tell, Server, Party, Dm, Dialog }

/// <summary>The game clock, so clients can light areas for the time of day between updates.</summary>
public sealed class TimeMessage
{
    public int Hour, Minute, Second, MinutesPerHour = 2, DawnHour = 6, DuskHour = 18;
    public byte[] Write() => new MessageWriter(MessageType.Time).I32(Hour).I32(Minute).I32(Second).I32(MinutesPerHour).I32(DawnHour).I32(DuskHour).ToArray();
    public static TimeMessage Read(MessageReader r) => new() { Hour = r.I32(), Minute = r.I32(), Second = r.I32(), MinutesPerHour = r.I32(), DawnHour = r.I32(), DuskHour = r.I32() };
}

/// <summary>A named engine event with a JSON payload (visual effects, sounds, music, journal, camera, ...).</summary>
public sealed class NotifyMessage
{
    public string Kind = "";
    public uint Subject;
    public string Json = "{}";

    public byte[] Write() => new MessageWriter(MessageType.Notify).Str(Kind).U32(Subject).Str(Json).ToArray();
    public static NotifyMessage Read(MessageReader r) => new() { Kind = r.Str(), Subject = r.U32(), Json = r.Str() };
}

public sealed class ChatMessage
{
    public uint Speaker;
    public ChatChannel Channel;
    public string SpeakerName = "";
    public string Text = "";

    public byte[] Write() => new MessageWriter(MessageType.Chat).U32(Speaker).U8((byte)Channel).Str(SpeakerName).Str(Text).ToArray();
    public static ChatMessage Read(MessageReader r) => new() { Speaker = r.U32(), Channel = (ChatChannel)r.U8(), SpeakerName = r.Str(), Text = r.Str() };
}

public sealed class ChatSendMessage
{
    public ChatChannel Channel;
    public string Text = "";
    public byte[] Write() => new MessageWriter(MessageType.ChatSend).U8((byte)Channel).Str(Text).ToArray();
    public static ChatSendMessage Read(MessageReader r) => new() { Channel = (ChatChannel)r.U8(), Text = r.Str() };
}

public enum ActionKind : byte { Use, Attack, Talk, PickUp, Emote, ToggleStance, MoveTo, OpenDoor, CloseDoor }

public sealed class ActionRequestMessage
{
    public ActionKind Action;
    public uint Target;
    public string Argument = "";
    public Vector3 Position;
    public bool Run = true;
    public byte[] Write() => new MessageWriter(MessageType.ActionRequest).U8((byte)Action).U32(Target).Str(Argument).V3(Position).Bool(Run).ToArray();
    public static ActionRequestMessage Read(MessageReader r) => new() { Action = (ActionKind)r.U8(), Target = r.U32(), Argument = r.Str(), Position = r.V3(), Run = r.Bool() };
}

/// <summary>One step of a conversation shown to a player. Ended closes the window.</summary>
public sealed class DialogMessage
{
    public uint Owner, Speaker;
    public string SpeakerName = "", Text = "", Sound = "";
    public List<(int Index, string Text)> Replies = new();
    public bool Ended;

    public byte[] Write()
    {
        var w = new MessageWriter(MessageType.Dialog).U32(Owner).U32(Speaker).Str(SpeakerName).Str(Text).Str(Sound).Bool(Ended).I32(Replies.Count);
        foreach (var (i, t) in Replies) w.I32(i).Str(t);
        return w.ToArray();
    }

    public static DialogMessage Read(MessageReader r)
    {
        var m = new DialogMessage { Owner = r.U32(), Speaker = r.U32(), SpeakerName = r.Str(), Text = r.Str(), Sound = r.Str(), Ended = r.Bool() };
        int n = r.Count(5);
        for (int k = 0; k < n; k++) m.Replies.Add((r.I32(), r.Str()));
        return m;
    }
}

/// <summary>The player picked a reply (the index sent in DialogMessage; -1 ends the conversation).</summary>
public sealed class DialogReplyMessage
{
    public int Choice;
    public byte[] Write() => new MessageWriter(MessageType.DialogReply).I32(Choice).ToArray();
    public static DialogReplyMessage Read(MessageReader r) => new() { Choice = r.I32() };
}
