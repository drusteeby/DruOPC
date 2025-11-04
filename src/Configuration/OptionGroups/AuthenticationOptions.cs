namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using System;

/// <summary>
/// Options for authentication configuration.
/// </summary>
public class AuthenticationOptions : IOptionGroup
{
    private readonly OpcPlcConfiguration _config;

    public AuthenticationOptions(OpcPlcConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RegisterOptions(OptionSet options)
    {
        options.Add(
            "daa|disableanonymousauth",
            $"flag to disable anonymous authentication.\nDefault: {_config.DisableAnonymousAuth}",
            (s) => _config.DisableAnonymousAuth = s != null);

        options.Add(
            "dua|disableusernamepasswordauth",
            $"flag to disable username/password authentication.\nDefault: {_config.DisableUsernamePasswordAuth}",
            (s) => _config.DisableUsernamePasswordAuth = s != null);

        options.Add(
            "dca|disablecertauth",
            $"flag to disable certificate authentication.\nDefault: {_config.DisableCertAuth}",
            (s) => _config.DisableCertAuth = s != null);

        options.Add(
            "au|adminuser=",
            $"the username of the admin user.\nDefault: {_config.AdminUser}",
            (s) => _config.AdminUser = s ?? _config.AdminUser);

        options.Add(
            "ac|adminpassword=",
            $"the password of the administrator.\nDefault: {_config.AdminPassword}",
            (s) => _config.AdminPassword = s ?? _config.AdminPassword);

        options.Add(
            "du|defaultuser=",
            $"the username of the default user.\nDefault: {_config.DefaultUser}",
            (s) => _config.DefaultUser = s ?? _config.DefaultUser);

        options.Add(
            "dc|defaultpassword=",
            $"the password of the default user.\nDefault: {_config.DefaultPassword}",
            (s) => _config.DefaultPassword = s ?? _config.DefaultPassword);
    }
}
