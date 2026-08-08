namespace OpcPlc.Tests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using OpcPlc.Configuration;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tests for binding the OpcPlc configuration section from configuration sources.
/// </summary>
[TestFixture]
public class ConfigurationBindingTests
{
    private static OpcPlcConfiguration Bind(params KeyValuePair<string, string>[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return configuration.GetSection(OpcPlcConfiguration.SectionName).Get<OpcPlcConfiguration>()
            ?? new OpcPlcConfiguration();
    }

    [Test]
    public void Bind_TrustedUserCertBase64_PopulatesConfigList()
    {
        var config = Bind(new KeyValuePair<string, string>("OpcPlc:OpcUa:TrustedUserCertificateBase64Strings:0", "Zm9vYmFyYmFzZTY0"));

        config.OpcUa.TrustedUserCertificateBase64Strings.Should().NotBeNull();
        config.OpcUa.TrustedUserCertificateBase64Strings.Count.Should().Be(1);
        config.OpcUa.TrustedUserCertificateBase64Strings.Single().Should().Be("Zm9vYmFyYmFzZTY0");
    }

    [Test]
    public void Bind_TrustedUserCertFile_PopulatesConfigFileList()
    {
        var config = Bind(new KeyValuePair<string, string>("OpcPlc:OpcUa:TrustedUserCertificateFileNames:0", "/tmp/some-cert.der"));

        config.OpcUa.TrustedUserCertificateFileNames.Should().NotBeNull();
        config.OpcUa.TrustedUserCertificateFileNames.Count.Should().Be(1);
        config.OpcUa.TrustedUserCertificateFileNames.Single().Should().Be("/tmp/some-cert.der");
    }

    [Test]
    public void Bind_ServerPortAndSimulation_OverridesDefaults()
    {
        var config = Bind(
            new("OpcPlc:OpcUa:ServerPort", "51234"),
            new("OpcPlc:Simulation:AddAlarmSimulation", "true"),
            new("OpcPlc:Simulation:EventInstanceCount", "7"));

        config.OpcUa.ServerPort.Should().Be(51234);
        config.Simulation.AddAlarmSimulation.Should().BeTrue();
        config.Simulation.EventInstanceCount.Should().Be(7);
    }

    [Test]
    public void Bind_EmptySection_UsesDefaults()
    {
        var config = Bind();

        config.OpcUa.ServerPort.Should().Be(50000);
        config.Simulation.AddReferenceTestSimulation.Should().BeTrue();
        config.FastNodes.NodeCount.Should().Be(1);
        config.TagWriter.Enabled.Should().BeTrue();
    }
}
