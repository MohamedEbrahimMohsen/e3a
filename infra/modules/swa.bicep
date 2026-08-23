param baseName string
param location string

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'swa-${baseName}'
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // Deployed via GitHub Actions (Azure/static-web-apps-deploy); no repo link here
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Enabled'
  }
}

output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
