# Fonction pour générer un nom aléatoire
function Get-RandomFileName {
    return [System.IO.Path]::GetRandomFileName().Replace(".", "") + ".bin"
}

# Fonction pour générer un contenu aléatoire de 4KB
function Get-RandomContent {
    $bytes = New-Object byte[] 4096
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $rng.GetBytes($bytes)
    return $bytes
}

# Fonction pour calculer le hash SHA256
function Get-FastHash {
    param([string]$filePath)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $fileStream = [System.IO.File]::OpenRead($filePath)
    try {
        return [System.BitConverter]::ToString($sha256.ComputeHash($fileStream)).Replace("-", "").ToLower()
    }
    finally {
        $fileStream.Dispose()
        $sha256.Dispose()
    }
}

# Création du répertoire principal
$mainDir = New-Item -ItemType Directory -Path ".\GeneratedFiles" -Force
$manifestDir = New-Item -ItemType Directory -Path "$($mainDir.FullName)\Manifest" -Force

# Liste pour stocker les informations des fichiers
$fileInfos = @()

# Mesure du temps de début
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Génération des 400 fichiers .bin
1..400 | ForEach-Object {
    $fileName = Get-RandomFileName
    $filePath = Join-Path $mainDir.FullName $fileName
    $content = Get-RandomContent
    [System.IO.File]::WriteAllBytes($filePath, $content)
    
    $fileInfos += @{
        Path = $filePath
        Name = $fileName
    }
}


# Création des 400 fichiers manifestes
1..400 | ForEach-Object {

# Sélection aléatoire de 20 fichiers
$selectedFiles = $fileInfos | Get-Random -Count 20

    $manifestContent = @{
        Resources = $selectedFiles | ForEach-Object {
            @{
                Id = $_.Name  # Utilisation du nom du fichier comme Id au lieu d'un GUID
                Size = (Get-Item $_.Path).Length / 1024 # Taille en Ko
                Version = "1"
                Hash = Get-FastHash $_.Path
            }
        }
    } | ConvertTo-Json -Depth 10
    
    $manifestPath = Join-Path $manifestDir.FullName "manifest_$_.json"
    [System.IO.File]::WriteAllText($manifestPath, $manifestContent)
}

$sw.Stop()

# Affichage des résultats
Write-Host "Opération terminée en $($sw.ElapsedMilliseconds) ms"
Write-Host "Fichiers générés dans: $($mainDir.FullName)"
Write-Host "Manifestes générés dans: $($manifestDir.FullName)"