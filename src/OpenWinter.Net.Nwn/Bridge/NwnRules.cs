using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net.Nwn.Messages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The character rules an NWN client works out itself (the classes a level may take, the skills, feats and spells it
/// offers, what a new character may be): OpenWinter's own rules, run on a world without a module that reads the
/// bridge's game data (the server's haks and talk table once it names them). The answers go to the client as the
/// notifies the OpenWinter server sends, about the player's creature.
/// </summary>
public sealed class NwnRules(INwnBridgeData data, Action<byte[]> emit) : IWorldOutput
{
    World? _world;
    ResMan? _resources;

    /// <summary>The object the answers are about: the player's creature, or none before one is chosen.</summary>
    public uint Subject { get; set; } = GameObjUpdateMessages.InvalidObjectId;

    /// <summary>The rules world for the current game data; null without game data.</summary>
    public World? World
    {
        get
        {
            if (data.Resources is not { } rm) return null;
            if (_world == null || !ReferenceEquals(rm, _resources))
            {
                _world = new World(rm) { Output = this };
                _resources = rm;
            }
            return _world;
        }
    }

    /// <summary>True when the game data changed since <paramref name="world"/> was made.</summary>
    public bool Stale(World world) => !ReferenceEquals(world, World);

    /// <summary>Sends a message to the client.</summary>
    public void Emit(byte[] message) => emit(message);

    /// <summary>Sends a notify about the player's creature.</summary>
    public void Notify(string kind, object payload) =>
        emit(new NotifyMessage { Kind = kind, Subject = Subject, Json = payload as string ?? JsonSerializer.Serialize(payload) }.Write());

    void IWorldOutput.Notify(string kind, GameObject? subject, object payload, Creature? player) => Notify(kind, payload);
    void IWorldOutput.Speak(GameObject speaker, string text, int volume) { }
    void IWorldOutput.PlayAnimation(GameObject o, int animation, float speed, float duration) { }
    void IWorldOutput.SendMessage(Creature pc, string text) { }
    void IWorldOutput.FloatingText(Creature over, string text, bool toFaction) { }
}
