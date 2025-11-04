namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;

/// <summary>
/// Interface for option groups that can register their options with the option set.
/// </summary>
public interface IOptionGroup
{
    /// <summary>
    /// Register options with the provided option set.
    /// </summary>
    /// <param name="options">The option set to register options with.</param>
    void RegisterOptions(OptionSet options);
}
