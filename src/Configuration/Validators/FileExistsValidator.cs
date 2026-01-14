namespace OpcPlc.Configuration.Validators;

using Mono.Options;
using System.IO;

/// <summary>
/// Validates that a file exists.
/// </summary>
public class FileExistsValidator : IOptionValidator<string>
{
    public void Validate(string filePath, string optionName)
    {
        if (!File.Exists(filePath))
        {
            throw new OptionException($"The file '{filePath}' does not exist.", optionName);
        }
    }
}
