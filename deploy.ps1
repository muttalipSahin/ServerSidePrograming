# Check if Azure CLI is installed
if (!(Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "Azure CLI is not installed. Please install it from https://aka.ms/InstallAzureCli"
    exit
}

# Define variables
$baseResourceGroupName = "cloud-minor-ssp"
$uniqueId = Get-Date -Format "yyyyMMddHHmmss"
$resourceGroupName = "$baseResourceGroupName-$uniqueId"
$location = "westeurope"
$storageAccountName = ("ssp$($uniqueId.Substring(6))").ToLower()
$functionAppName = "weather-func-$($uniqueId.Substring(6))"
$functionProjectPath = ".\ServerSidePrograming.csproj"
$dotnetVersion = "net6.0"
$publishFolder = "bin/Release/net6.0/publish"
$publishZip = "publish.zip"

# Login to Azure (if not logged in)
Write-Host "Logging in to Azure..."
az login --use-device-code

# Set the subscription
Write-Host "Setting Azure subscription..."
az account set --subscription "b3fa1f42-4224-4e75-9188-926fb8b53c5c"

# Create a unique resource group
Write-Host "Creating new resource group: $resourceGroupName"
az group create --name $resourceGroupName --location $location

# Deploy Azure resources
Write-Host "Deploying Azure resources..."
az deployment group create --resource-group $resourceGroupName `
    --template-file azure-resources.bicep `
    --parameters "storageAccountName=$storageAccountName" "functionAppName=$functionAppName" "location=$location"

# Retrieve storage account connection string
Write-Host "Retrieving storage account connection string..."
$storageConnectionString = az storage account show-connection-string --name $storageAccountName --resource-group $resourceGroupName --query connectionString --output tsv

# Create the Function App
Write-Host "Creating Azure Function App..."
az functionapp create `
  -n $functionAppName `
  --storage-account $storageAccountName `
  --consumption-plan-location $location `
  --app-insights "$functionAppName-ai" `
  --runtime dotnet-isolated `
  -g $resourceGroupName

# Build and publish the function app
Write-Host "Building and publishing the function app..."
dotnet publish -c Release

# Create the zip package
if (Test-Path $publishZip) { Remove-Item $publishZip }
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($publishFolder, $publishZip)

# Deploy the zipped package
Write-Host "Deploying function code to Azure..."
az functionapp deployment source config-zip `
 -g $resourceGroupName -n $functionAppName --src $publishZip

# Read the settings from local.settings.json
$localSettings = Get-Content local.settings.json | ConvertFrom-Json

# Check if the 'Values' property exists and is an object
if ($localSettings.PSObject.Properties["Values"]) {
    Write-Host "Setting environment variables in Azure..."
    $localSettings.Values.PSObject.Properties | ForEach-Object {
        $key = $_.Name
        $value = $_.Value
        if ($key -match "unsplash") {
            Write-Host "Setting: $key=$value"
            az functionapp config appsettings set --name $functionAppName --resource-group $resourceGroupName --settings "$key=$value"
        }
    }
    # Add storage connection string
    Write-Host "Setting storage connection string..."
    az functionapp config appsettings set --name $functionAppName --resource-group $resourceGroupName --settings "AzureWebJobsStorage=$storageConnectionString"
} else {
    Write-Host "No environment variables found in local.settings.json."
}

# Show the deployed function URL
Write-Host "Deployment completed successfully!"
az functionapp show --name $functionAppName --resource-group $resourceGroupName --query "defaultHostName" --output tsv
