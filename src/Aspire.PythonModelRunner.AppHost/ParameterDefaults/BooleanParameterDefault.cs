using Aspire.Hosting.Publishing;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.PythonModelRunner.AppHost.ParameterDefaults;

[SuppressMessage("Performance", "CA1812", Justification = "Available for future use")]
internal sealed class BooleanParameterDefault(bool defaultValue = false)
    : ParameterDefault
{
    public override string GetDefaultValue()
    {
        return defaultValue.ToString();
    }

    public override void WriteToManifest(ManifestPublishingContext context)
    {
    }
}