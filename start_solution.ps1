param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('build', 'run', 'test', 'perf')]
    [string]$Action,
    [string]$Configuration = "Release"
)

$SolutionName = "IoTHighPerf"

function Build-Solution {
    Write-Host "Building solution in $Configuration configuration..." -ForegroundColor Yellow
    dotnet build -c $Configuration
}

function Run-Solution {
    Write-Host "Running API in $Configuration configuration..." -ForegroundColor Yellow
    Set-Location "$SolutionName.Api"
    dotnet run -c $Configuration
}

function Run-Tests {
    Write-Host "Running all tests..." -ForegroundColor Yellow
    dotnet test -c $Configuration --no-build
}

function Run-PerfTests {
    Write-Host "Running performance tests..." -ForegroundColor Yellow
    
    # Vérifier si k6 est installé
    if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) {
        Write-Host "k6 n'est pas installé. Installation..." -ForegroundColor Red
        # Instructions d'installation de k6 selon votre système
        Exit 1
    }

    # Exécuter les tests de performance avec k6
    Set-Location "$SolutionName.IntegrationTests/Performance"
    k6 run load-test.js
}

switch ($Action) {
    'build' { Build-Solution }
    'run' { 
        Build-Solution
        Run-Solution 
    }
    'test' { 
        Build-Solution
        Run-Tests 
    }
    'perf' { 
        #Build-Solution
        Run-PerfTests 
    }
}