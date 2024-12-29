# Script de pr�paration de l'environnement
param(
    [string]$SolutionName = "IoTHighPerf"
)

Write-Host "Cr�ation/Mise � jour de la solution $SolutionName..." -ForegroundColor Green

# Cr�ation de la solution si elle n'existe pas
if (-not (Test-Path "$SolutionName.sln")) {
    dotnet new sln -n $SolutionName
}

# Fonction pour cr�er ou mettre � jour un projet
function Ensure-Project {
    param(
        [string]$ProjectName,
        [string]$Template,
        [hashtable]$Properties = @{}
    )

    if (-not (Test-Path $ProjectName)) {
        Write-Host "Cr�ation du projet $ProjectName..." -ForegroundColor Yellow
        mkdir $ProjectName | Out-Null
        

    }

        # Cr�ation du fichier csproj si n�cessaire
        if (-not (Test-Path "$ProjectName/$ProjectName.csproj")) {
            $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
$(if ($Properties.ContainsKey("OutputType")) {"    <OutputType>$($Properties.OutputType)</OutputType>"})
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
"@
            $csprojContent | Out-File "$ProjectName/$ProjectName.csproj" -Encoding utf8
        }



    # Ajout � la solution si pas d�j� pr�sent
    $slnContent = Get-Content "$SolutionName.sln" -Raw
    if (-not ($slnContent -match [regex]::Escape($ProjectName))) {
        Write-Host "Ajout du projet $ProjectName � la solution..." -ForegroundColor Yellow
        dotnet sln add "$ProjectName/$ProjectName.csproj"
    }
}

# Cr�ation/Mise � jour des projets
Ensure-Project "$SolutionName.Api" "webapi" @{ OutputType = "Exe" }
Ensure-Project "$SolutionName.Core" "classlib"
Ensure-Project "$SolutionName.Infrastructure" "classlib"
Ensure-Project "$SolutionName.UnitTests" "xunit"
Ensure-Project "$SolutionName.IntegrationTests" "xunit"
Ensure-Project "$SolutionName.ActivityGenerator" "worker" @{ OutputType = "Exe" }


# Ajout/Mise � jour des packages NuGet
function Add-PackageIfMissing {
    param(
        [string]$ProjectPath,
        [string]$Package
    )
    
    $csproj = Get-Content "$ProjectPath/$ProjectPath.csproj" -Raw
    if (-not ($csproj -match [regex]::Escape($Package))) {
        Write-Host "Ajout du package $Package � $ProjectPath..." -ForegroundColor Yellow
        Set-Location $ProjectPath
        dotnet add package $Package
        Set-Location ..
    }
}

function Add-ProjectReferenceIfMissing {
    param(
        [string]$ProjectPath,
        [string]$Reference
    )
    
    $csproj = Get-Content "$ProjectPath/$ProjectPath.csproj" -Raw
    if (-not ($csproj -match [regex]::Escape($Reference))) {
        Write-Host "Ajout de la r�f�rence $Reference � $ProjectPath..." -ForegroundColor Yellow
        Set-Location $ProjectPath
        dotnet add reference "../$Reference/$Reference.csproj"
        Set-Location ..
    }
}



# Core
Add-PackageIfMissing "$SolutionName.Core" "System.IO.Pipelines"


# Infrastructure
Add-PackageIfMissing "$SolutionName.Infrastructure" "Microsoft.FASTER.Core"
Add-PackageIfMissing "$SolutionName.Infrastructure" "Microsoft.Extensions.ObjectPool"
Add-PackageIfMissing "$SolutionName.Infrastructure" "System.Threading.Channels"
Add-PackageIfMissing "$SolutionName.Infrastructure" "NetMQ"
Add-ProjectReferenceIfMissing "$SolutionName.Infrastructure" "$SolutionName.Core"

# Api
Add-PackageIfMissing "$SolutionName.Api" "Microsoft.IO.RecyclableMemoryStream"
Add-PackageIfMissing "$SolutionName.Api" "Microsoft.FASTER.Core"
Add-PackageIfMissing "$SolutionName.Api" "System.IO.Pipelines"
Add-PackageIfMissing "$SolutionName.Api" "prometheus-net.AspNetCore"
Add-PackageIfMissing "$SolutionName.Api" "Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets"
Add-PackageIfMissing "$SolutionName.Api" "NetMQ"

 

Add-ProjectReferenceIfMissing "$SolutionName.Api" "$SolutionName.Core"
Add-ProjectReferenceIfMissing "$SolutionName.Api" "$SolutionName.Infrastructure"



# Tests
Add-PackageIfMissing "$SolutionName.UnitTests" "Microsoft.NET.Test.Sdk"
Add-PackageIfMissing "$SolutionName.UnitTests" "xunit"
Add-PackageIfMissing "$SolutionName.UnitTests" "xunit.runner.visualstudio"
Add-PackageIfMissing "$SolutionName.UnitTests" "Moq"
Add-ProjectReferenceIfMissing "$SolutionName.UnitTests" "$SolutionName.Core"

Add-PackageIfMissing "$SolutionName.IntegrationTests" "Microsoft.NET.Test.Sdk"
Add-PackageIfMissing "$SolutionName.IntegrationTests" "xunit"
Add-PackageIfMissing "$SolutionName.IntegrationTests" "xunit.runner.visualstudio"
Add-PackageIfMissing "$SolutionName.IntegrationTests" "Microsoft.AspNetCore.Mvc.Testing"
Add-ProjectReferenceIfMissing "$SolutionName.IntegrationTests" "$SolutionName.Api"



# Generateur activité
Add-PackageIfMissing "$SolutionName.ActivityGenerator" "Microsoft.FASTER.Core"
Add-PackageIfMissing "$SolutionName.ActivityGenerator" "Microsoft.Extensions.Hosting"
Add-PackageIfMissing "$SolutionName.ActivityGenerator" "Microsoft.Extensions.Configuration"
Add-PackageIfMissing "$SolutionName.ActivityGenerator" "System.Threading.Tasks.Dataflow"
Add-ProjectReferenceIfMissing "$SolutionName.ActivityGenerator" "$SolutionName.Core"

# dotnet new console -n IoTHighPerf.Client
# cd IoTHighPerf.Client
# dotnet add package NetMQ 
# dotnet add package CommandLineParser --version 2.9.1

Write-Host "Configuration de la solution termin�e avec succ�s!" -ForegroundColor Green