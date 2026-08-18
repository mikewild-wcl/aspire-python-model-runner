using System.Globalization;

namespace Aspire.PythonModelRunner.AppHost.Extensions;

internal static class ResourceBuilderExtensions
{
    extension(IResourceBuilder<ParameterResource> parameter)
    {
        internal string? GetValue(CancellationToken? cancellationToken = null) => parameter.Resource
            .GetValueAsync(cancellationToken ?? CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        internal T? GetValue<T>() where T : struct, IParsable<T>
        {
            var value = parameter.GetValue();
            return T.TryParse(value, CultureInfo.InvariantCulture, out T result)
                ? result
                : null;
        }
    }

    extension(IResourceBuilder<ProjectResource> builder)
    {
        internal IResourceBuilder<ProjectResource> WithFeatureFlag(
            IResourceBuilder<ParameterResource> featureFlagParameter,
            string? name = null,
            bool useNewStyleConfiguration = true)
        {
            name ??= featureFlagParameter.Resource.Name;
            var value = featureFlagParameter.GetValue();

            if (value is not null)
            {
                if (useNewStyleConfiguration)
                {
                    builder
                        .WithEnvironment("feature_management__feature_flags__0__id", name)
                        .WithEnvironment("feature_management__feature_flags__0__enabled", value);
                }
                else
                {
                    // Old style feature flag config still works, but new style is preferred.
                    builder
                        .WithEnvironment($"FeatureManagement:{name}", value);
                }
            }

            return builder;
        }
    }
}
