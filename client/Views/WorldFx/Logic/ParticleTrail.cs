namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Spatial birth intervals: measured nonlocal Fountain trails use particles per metre.</summary>
public static class ParticleTrail
{
    public const int MaxParticles = 2000;
    public readonly record struct Births(int Count, double First, double Step)
    {
        public float Fraction(int index) => (float)Math.Clamp(First + index * Step, 0, 1);
    }

    public static Births Advance(double distance, double density, ref double carry)
    {
        double amount = distance * density;
        if (!double.IsFinite(amount) || amount <= 0) return default;
        double before = Math.Clamp(carry, 0, 1);
        double total = Math.Floor(before + amount + 1e-9);
        carry = Math.Clamp(before + amount - total, 0, 1);
        int count = (int)Math.Min(total, MaxParticles);
        // On an extreme displacement retain the newest births within the pool ceiling.
        return new(count, (1 - before + total - count) / amount, 1 / amount);
    }
}
