targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module env_acr 'env-acr/env-acr.bicep' = {
  name: 'env-acr'
  scope: rg
  params: {
    location: location
  }
}

module env 'env/env.bicep' = {
  name: 'env'
  scope: rg
  params: {
    location: location
    env_acr_outputs_name: env_acr.outputs.name
    userPrincipalId: principalId
  }
}

module service_bus 'service-bus/service-bus.bicep' = {
  name: 'service-bus'
  scope: rg
  params: {
    location: location
  }
}

module python_app_identity 'python-app-identity/python-app-identity.bicep' = {
  name: 'python-app-identity'
  scope: rg
  params: {
    location: location
  }
}

module python_app_roles_service_bus 'python-app-roles-service-bus/python-app-roles-service-bus.bicep' = {
  name: 'python-app-roles-service-bus'
  scope: rg
  params: {
    location: location
    service_bus_outputs_name: service_bus.outputs.name
    principalId: python_app_identity.outputs.principalId
  }
}

module backend_identity 'backend-identity/backend-identity.bicep' = {
  name: 'backend-identity'
  scope: rg
  params: {
    location: location
  }
}

module backend_roles_service_bus 'backend-roles-service-bus/backend-roles-service-bus.bicep' = {
  name: 'backend-roles-service-bus'
  scope: rg
  params: {
    location: location
    service_bus_outputs_name: service_bus.outputs.name
    principalId: backend_identity.outputs.principalId
  }
}

output env_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN

output env_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = env.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID

output env_AZURE_CONTAINER_REGISTRY_ENDPOINT string = env.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT

output env_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = env.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID

output python_app_identity_id string = python_app_identity.outputs.id

output service_bus_serviceBusEndpoint string = service_bus.outputs.serviceBusEndpoint

output service_bus_serviceBusHostName string = service_bus.outputs.serviceBusHostName

output python_app_identity_clientId string = python_app_identity.outputs.clientId

output backend_identity_id string = backend_identity.outputs.id

output backend_identity_clientId string = backend_identity.outputs.clientId