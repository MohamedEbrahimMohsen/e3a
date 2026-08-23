targetScope = 'subscription'

@description('Base name used to derive all resource names')
param baseName string = 'e3a'

@description('Azure region for all resources')
param location string = 'westeurope'

@secure()
@description('SQL admin password (CI supplies from secret)')
param sqlAdminPassword string

param sqlAdminLogin string = 'e3aadmin'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${baseName}'
  location: location
}

module storage 'modules/storage.bicep' = {
  scope: rg
  name: 'storage'
  params: {
    baseName: baseName
    location: location
  }
}

module sql 'modules/sql.bicep' = {
  scope: rg
  name: 'sql'
  params: {
    baseName: baseName
    location: location
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
  }
}

module functions 'modules/functions.bicep' = {
  scope: rg
  name: 'functions'
  params: {
    baseName: baseName
    location: location
    storageAccountName: storage.outputs.storageAccountName
  }
}

module swa 'modules/swa.bicep' = {
  scope: rg
  name: 'swa'
  params: {
    baseName: baseName
    location: location
  }
}

output functionAppName string = functions.outputs.functionAppName
output staticWebAppName string = swa.outputs.staticWebAppName
output storageAccountName string = storage.outputs.storageAccountName
output sqlServerFqdn string = sql.outputs.sqlServerFqdn
