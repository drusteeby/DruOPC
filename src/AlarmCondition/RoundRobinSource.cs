namespace OpcPlc.AlarmCondition;

using Opc.Ua.Test;

/// <summary>
/// Returns sequential numbers in a round-robin fashion.
/// </summary>
public class RoundRobinSource : IRandomSource
{
    int _seed;

    /// <summary>
    /// Initializes the source with a seed.
    /// </summary>
    public RoundRobinSource(int seed)
    {
        _seed = seed;
    }

    public void NextBytes(byte[] bytes, int offset, int count)
    {
        // Deterministic but unique per call: used e.g. for event id GUIDs,
        // which must not collide (they key the alarm acknowledge lookup).
        for (int i = 0; i < count; i++)
        {
            bytes[offset + i] = unchecked((byte)_seed++);
        }
    }

    public int NextInt32(int max)
    {
        return _seed++ % max;
    }
}
