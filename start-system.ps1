#
# Dieses Skript automatisiert die Einrichtung und den Start des Chat-Systems.
# Es prüft und startet den RabbitMQ-Docker-Container, baut die .NET-Solution,
# und startet dann den Server und zwei Clients in separaten PowerShell-Fenstern.
#
# WICHTIG: Führen Sie dieses Skript direkt aus dem Wurzelverzeichnis aus
# oder stellen Sie sicher, dass Ihr Terminal-Pfad auf dieses Verzeichnis gesetzt ist.
#

Write-Host "--- Chat System Starter ---" -ForegroundColor Green

# --- Schritt 1: RabbitMQ Docker-Container neu starten oder erstellen ---
$containerName = "some-rabbit"
Write-Host "Prüfe auf existierenden RabbitMQ-Container '$containerName'..."
$existingContainerId = docker ps -a -q -f "name=$containerName"

if ($existingContainerId) {
    Write-Host "Existierender RabbitMQ-Container '$containerName' gefunden. Wird neu gestartet..."
    docker restart $containerName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Fehler beim Neustarten des RabbitMQ-Containers. Bitte stellen Sie sicher, dass Docker läuft."
        Read-Host "Drücken Sie Enter, um das Skript zu beenden."
        exit 1
    }
} else {
    Write-Host "RabbitMQ-Container '$containerName' nicht gefunden. Er wird neu erstellt und gestartet..."
    docker run -d --hostname my-rabbit --name $containerName -p 5672:5672 -p 15672:15672 rabbitmq:3-management
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Fehler beim Starten des RabbitMQ-Containers. Bitte stellen Sie sicher, dass Docker läuft."
        Read-Host "Drücken Sie Enter, um das Skript zu beenden."
        exit 1
    }
}

Write-Host "Warte 15 Sekunden, damit RabbitMQ initialisiert werden kann..."
Start-Sleep -Seconds 15

# --- Schritt 2: .NET-Solution bauen ---
Write-Host "Baue die .NET-Solution..."
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build ist fehlgeschlagen. Bitte prüfen Sie die Fehler."
    Read-Host "Drücken Sie Enter, um das Skript zu beenden."
    exit 1
}

# --- Schritt 3: Server und Clients starten ---
Write-Host "Starte Server und Clients in neuen Fenstern..."

$basePath = $PSScriptRoot | Resolve-Path

# Starte den Server in einem neuen Fenster
Start-Process powershell -WorkingDirectory $basePath -ArgumentList "-NoExit", "-Command", "Write-Host '--- Chat Server ---' -ForegroundColor Yellow; dotnet run --project ChatServer"

# Warte einen Moment, damit der Server bereit ist
Write-Host "Warte 5 Sekunden auf den Serverstart..."
Start-Sleep -Seconds 5

# Starte Client 1 in einem neuen Fenster
Start-Process powershell -WorkingDirectory $basePath -ArgumentList "-NoExit", "-Command", "Write-Host '--- Chat Client 1 ---' -ForegroundColor Cyan; dotnet run --project ChatClient"

# Starte Client 2 in einem neuen Fenster
Start-Process powershell -WorkingDirectory $basePath -ArgumentList "-NoExit", "-Command", "Write-Host '--- Chat Client 2 ---' -ForegroundColor Cyan; dotnet run --project ChatClient"

# Starte Client 3 in einem neuen Fenster
Start-Process powershell -WorkingDirectory $basePath -ArgumentList "-NoExit", "-Command", "Write-Host '--- Chat Client 3 ---' -ForegroundColor Cyan; dotnet run --project ChatClient"


Write-Host "--- System erfolgreich gestartet! ---" -ForegroundColor Green
