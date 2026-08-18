@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param frontend_containerimage string

param frontend_containerport string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

resource frontend 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'frontend'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: int(frontend_containerport)
        transport: 'http'
      }
      registries: [
        {
          server: env_outputs_azure_container_registry_endpoint
          identity: env_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: env_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: frontend_containerimage
          name: 'frontend'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: frontend_containerport
            }
            {
              name: 'feature_management__feature_flags__0__id'
              value: 'UseEntraAuthentication'
            }
            {
              name: 'feature_management__feature_flags__0__enabled'
              value: 'False'
            }
            {
              name: 'BACKEND_HTTP'
              value: 'https://backend.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__backend__http__0'
              value: 'https://backend.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'BACKEND_HTTPS'
              value: 'https://backend.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__backend__https__0'
              value: 'https://backend.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}