@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param env_outputs_azure_container_apps_environment_default_domain string

param env_outputs_azure_container_apps_environment_id string

param python_app_containerimage string

param python_app_identity_outputs_id string

param service_bus_outputs_servicebusendpoint string

param service_bus_outputs_servicebushostname string

param python_app_identity_outputs_clientid string

param env_outputs_azure_container_registry_endpoint string

param env_outputs_azure_container_registry_managed_identity_id string

resource python_app 'Microsoft.App/containerApps@2025-07-01' = {
  name: 'python-app'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8000
        transport: 'http'
      }
      registries: [
        {
          server: env_outputs_azure_container_registry_endpoint
          identity: env_outputs_azure_container_registry_managed_identity_id
        }
      ]
    }
    environmentId: env_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: python_app_containerimage
          name: 'python-app'
          env: [
            {
              name: 'OTEL_TRACES_EXPORTER'
              value: 'otlp'
            }
            {
              name: 'OTEL_LOGS_EXPORTER'
              value: 'otlp'
            }
            {
              name: 'OTEL_METRICS_EXPORTER'
              value: 'otlp'
            }
            {
              name: 'OTEL_PYTHON_LOGGING_AUTO_INSTRUMENTATION_ENABLED'
              value: 'true'
            }
            {
              name: 'PORT'
              value: '8000'
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
              name: 'AZURE_CLIENT_ID'
              value: python_app_identity_outputs_clientid
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
      '${python_app_identity_outputs_id}': { }
      '${env_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}