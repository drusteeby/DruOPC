namespace OpcPlc.Configuration.Validators;

using Mono.Options;

/// <summary>
/// Interface for option validators.
/// </summary>
/// <typeparam name="T">The type of value to validate.</typeparam>
public interface IOptionValidator<T>
{
    /// <summary>
    /// Validates the given value.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="optionName">The name of the option being validated.</param>
    /// <exception cref="OptionException">Thrown when validation fails.</exception>
    void Validate(T value, string optionName);
}
