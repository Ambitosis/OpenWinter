using System.Text.Json;
using OpenWinter.Content;

namespace OpenWinter.Client.Gameplay;

/// <summary>Sends only edited slots, preserving original QBList entries the client cannot display.</summary>
public sealed class QuickbarSync
{
    readonly string[] _sent = new string[36];
    readonly HashSet<int> _pending = new();
    public bool Ready { get; private set; }

    public void Reset() { Ready = false; _pending.Clear(); }

    public void Receive(IReadOnlyList<QuickbarSlot?> slots)
    {
        for (int i = 0; i < _sent.Length; i++) _sent[i] = JsonSerializer.Serialize(i < slots.Count ? slots[i] : null);
        Ready = true;
    }

    public IEnumerable<(int Index, QuickbarSlot? Slot)> Changes(IEnumerable<QuickbarSlot?> slots, int force = -1)
    {
        if (force is >= 0 and < 36) _pending.Add(force);
        if (!Ready) yield break;
        int index = 0;
        foreach (var slot in slots.Take(_sent.Length))
        {
            string json = JsonSerializer.Serialize(slot);
            if (_pending.Remove(index) || json != _sent[index])
            {
                _sent[index] = json;
                yield return (index, slot);
            }
            index++;
        }
    }
}
