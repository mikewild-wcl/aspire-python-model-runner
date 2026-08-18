using Aspire.Hosting.Publishing;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.PythonModelRunner.AppHost.ParameterDefaults;

[SuppressMessage("Performance", "CA1812", Justification = "Available for future use")]
internal sealed class EmptyParameterDefault : ParameterDefault
{
    public override string GetDefaultValue()
    {
        return string.Empty;
    }

    public override void WriteToManifest(ManifestPublishingContext context)
    {
    }
}