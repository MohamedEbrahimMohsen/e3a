using 'main.bicep'

param baseName = 'e3a'
param location = 'westeurope'
// sqlAdminPassword is supplied by CI: az deployment sub create ... --parameters sqlAdminPassword=$SQL_ADMIN_PASSWORD
param sqlAdminPassword = ''
