@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param backend_containerimage string

param backend_identity_outputs_id string

param backend_containerport string

param service_bus_outputs_servicebusendpoint string

param service_bus_outputs_servicebushostname string

param backend_identity_outputs_clientid string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

resource backend 'Microsoft.App/containerApps@2025-10-02-preview' = {
  name: 'backend'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: int(backend_containerport)
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
          image: backend_containerimage
          name: 'backend'
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
              value: backend_containerport
            }
            {
              name: 'ConnectionStrings__service-bus'
              value: service_bus_outputs_servicebusendpoint
            }
            {
              name: 'SERVICE_BUS_HOST'
              value: service_bus_outputs_servicebushostname
            }
            {
              name: 'SERVICE_BUS_URI'
              value: service_bus_outputs_servicebusendpoint
            }
            {
              name: 'PYTHON_APP_HTTP'
              value: 'https://python-app.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__python-app__http__0'
              value: 'https://python-app.internal.${env_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: backend_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
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
      '${backend_identity_outputs_id}': { }
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}