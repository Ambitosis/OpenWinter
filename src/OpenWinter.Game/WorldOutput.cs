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
    /// A feedback or combat log line for a player: <paramref name="kind"/> is its <see cref="FeedbackKind"/> and
    /// <paramref name="message"/> its feedback message id (NWNX_FEEDBACK_*, -1 for none), which decide whether the
    /// server hides it from that player.
    /// </summary>
    void SendFeedback(Creature pc, string text, int kind, int message = -1) => SendMessage(pc, text);

    /// <summary>
    /// Anything else clients should know about (visual effects, sounds, music,
    /// weather, door animations, camera, journal, NUI...). <paramref name="kind"/>
    /// names the event; <paramref name="data"/> is serialised as JSON. Sent to the
    /// players in the subject's area, or to one player when <paramref name="player"/> is set.
    /// </summary>
    void Notify(string kind, GameObject? subject, object data, Creature? player = null);
}

/// <summary>Speech from the world's objects.</summary>
public static class WorldSpeech
{
    /// <summary>
    /// An object speaks (SpeakString, ActionSpeakString, conversation one-liners): the NWNX chat
    /// script sees every line, silent ones too, and may skip it (parity suite nwnx_chat); then the clients and the
    /// listening creatures get it.
    /// </summary>
    public static void Speak(this World world, GameObject speaker, string text, int volume)
    {
        if (Nwnx.Plugins.ChatPlugin.Intercept(world, Nwnx.Plugins.ChatPlugin.Channel(volume), text, speaker)) return;
        world.Output?.Speak(speaker, text, volume);
    }
}

/// <summary>Kinds of feedback line, numbered as the original's combat log message types (NWNX_FEEDBACK_COMBATLOG_*).</summary>
public static class FeedbackKind
{
    public const int Adjective = 1, Damage = 2, ComplexDamage = 3, Death = 4, Attack = 5, SpecialAttack = 6, SavingThrow = 7,
        CastSpell = 8, UseSkill = 9, SpellResistance = 10, Message = 11, Counterspell = 12, TouchAttack = 13, Initiative = 14,
        DispelMagic = 15;
}

/// <summary>A feature system ticked every world step. Implementations need a constructor taking World; they are discovered automatically.</summary>
public interface IWorldSystem
{
    /// <summary>Lower runs earlier.</summary>
    int Order => 0;
    void Step(float dt);
}
