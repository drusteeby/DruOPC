namespace OpcPlc.Configuration.Validators;

using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates that a string value is one of the allowed values.
/// </summary>
public class EnumValidator : IOptionValidator<string>
{
    private readonly List<string> _allowedValues;
    private readonly bool _caseInsensitive;

    public EnumValidator(IEnumerable<string> allowedValues, bool caseInsensitive = true)
    {
        _allowedValues = allowedValues.ToList();
        _caseInsensitive = caseInsensitive;
    }

    public void Validate(string value, string optionName)
    {
        var comparer = _caseInsensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        if (!_allowedValues.Contains(value, comparer))
        {
            throw new OptionException(
                $"The {optionName} must be one of: {string.Join(", ", _allowedValues)}",
                optionName);
        }
    }
}
