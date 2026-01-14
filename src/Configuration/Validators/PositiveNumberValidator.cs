namespace OpcPlc.Configuration.Validators;

using Mono.Options;
using System;

/// <summary>
/// Validates that a numeric value is positive.
/// </summary>
/// <typeparam name="T">The numeric type to validate.</typeparam>
public class PositiveNumberValidator<T> : IOptionValidator<T> where T : IComparable
{
    private readonly T _zero;

    public PositiveNumberValidator(T zero)
    {
        _zero = zero;
    }

    public void Validate(T value, string optionName)
    {
        if (value.CompareTo(_zero) <= 0)
        {
            throw new OptionException($"The {optionName} must be a positive number.", optionName);
        }
    }
}
