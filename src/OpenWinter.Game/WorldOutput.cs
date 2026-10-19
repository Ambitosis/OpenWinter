using OpenWinter.Game.Objects;

namespace OpenWinter.Game;

/// <summary>What the world tells clients about that isn't object state (speech, messages, one-off animations).</summary>
public interface IWorldOutput
{
    void Speak(GameObject speaker, string text, int volume);
    void PlayAnimation(GameObject o, int animation, float speed, float duration);
    void SendMessage(Creature pc, string text);
    void FloatingText(Creature over, string text, bool toFaction);

    /// <summary>
    /// Anything else clients should know about (visual effects, sounds, music,
    /// weather, door animations, camera, journal, NUI...). <paramref name="kind"/>
    /// names the event; <paramref name="data"/> is serialised as JSON. Sent to the
    /// players in the subject's area, or to one player when <paramref name="player"/> is set.
    /// </summary>
    void Notify(string kind, GameObject? subject, object data, Creature? player = null);
}

/// <summary>A feature system ticked every world step. Implementations need a constructor taking World; they are discovered automatically.</summary>
public interface IWorldSystem
{
    /// <summary>Lower runs earlier.</summary>
    int Order => 0;
    void Step(float dt);
}
