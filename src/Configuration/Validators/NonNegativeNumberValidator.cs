namespace OpcPlc.Configuration.Validators;

using Mono.Options;
using System;

/// <summary>
/// Validates that a numeric value is non-negative (greater than or equal to zero).
/// </summary>
/// <typeparam name="T">The numeric type to validate.</typeparam>
public class NonNegativeNumberValidator<T> : IOptionValidator<T> where T : IComparable
{
    private readonly T _zero;

    public NonNegativeNumberValidator(T zero)
    {
        _zero = zero;
    }

    public void Validate(T value, string optionName)
    {
        if (value.CompareTo(_zero) < 0)
        {
            throw new OptionException($"The {optionName} must be larger or equal 0.", optionName);
        }
    }
}
