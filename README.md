# Basis-Chat-Anwendung mit C#, RabbitMQ und EasyNetQ

Dies ist eine einfache, konsolenbasierte Chat-Anwendung, die als Basis für die Semesterendarbeit im Kurs "Parallele und Verteilte Systeme" dient. Sie besteht aus einem zentralen `ChatServer` und beliebig vielen `ChatClient`-Instanzen.

Die Kommunikation erfolgt asynchron über eine RabbitMQ-Message-Broker-Instanz und wird durch die .NET-Bibliothek `EasyNetQ` vereinfacht.

> Die [Belegaufgabe](Belegaufgabe_2025.md) für dieses Semester finden Sie in der separaten Datei **"Belegaufgabe_2025.md"**.

## 1. Voraussetzungen: RabbitMQ

Für die Ausführung der Anwendung wird eine laufende RabbitMQ-Instanz benötigt. Der einfachste Weg, diese zu starten, ist über Docker.

**Befehl zum Starten eines RabbitMQ-Containers:**

```bash
docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

* `-p 5672:5672`: Port für die AMQP-Kommunikation (wird von EasyNetQ genutzt).

* `-p 15672:15672`: Port für die Web-Management-Oberfläche (optional, aber hilfreich). Sie können die Oberfläche unter `http://localhost:15672` mit dem Benutzernamen `guest` und dem Passwort `guest` aufrufen.

## 2. Automatischer Start mit PowerShell (Empfohlen)

Für eine einfache Inbetriebnahme wurde das PowerShell-Skript `start-system.ps1` hinzugefügt. Dieses Skript automatisiert den gesamten Startvorgang.

**Funktionen des Skripts:**

1. Prüft, ob der `some-rabbit` Docker-Container läuft, und (re-)startet diesen.

2. Baut die .NET-Solution mit `dotnet build`.
3. Öffnet drei neue PowerShell-Fenster: eines für den Server und zwei für die Clients.

**Ausführung:**
Navigieren Sie mit einer PowerShell-Konsole in das Wurzelverzeichnis und führen Sie das Skript aus:

```powershell
./start-system.ps1
```

**Hinweis zur PowerShell Execution Policy:**
Sollte die Ausführung mit einer Fehlermeldung zur `Execution Policy` fehlschlagen, müssen Sie diese möglicherweise für den aktuellen Prozess temporär lockern. Starten Sie das System in diesem Fall mit folgendem Befehl:

```powershell
powershell -ExecutionPolicy Bypass -File ./start-system.ps1
```

## 3. Manuelles Bauen und Ausführen der Anwendung

Die Solution `ChatSolution.sln` enthält alle benötigten Projekte. Sie können sie mit einem .NET-fähigen Editor (wie Visual Studio, JetBrains Rider oder VS Code) öffnen oder die .NET CLI verwenden.

### a) Anwendung bauen

Navigieren Sie in das Wurzelverzeichnis und führen Sie den folgenden Befehl aus, um alle Projekte zu bauen:

```bash
dotnet build
```

### b) Anwendung ausführen

Die Reihenfolge ist wichtig: **Zuerst muss der Server gestartet werden.**

**1. Server starten:**
Öffnen Sie ein Terminal, navigieren Sie in das Wurzelverzeichnis und führen Sie aus:

```bash
dotnet run --project ChatServer
```

Der Server zeigt an, dass er gestartet wurde und auf Verbindungen wartet.

**2. Client(s) starten:**
Öffnen Sie für jeden Chat-Teilnehmer ein **neues, separates Terminal**. Navigieren Sie in das Wurzelverzeichnis und führen Sie aus:

```bash
dotnet run --project ChatClient
```

Jeder Client wird nach einem Benutzernamen fragen.

## 3. Test-Szenario

1. **Server starten:** Führen Sie den Server wie oben beschrieben aus.

2. **Client A starten:** Starten Sie den ersten Client und geben Sie den Namen `Anton` ein.
3. **Client B starten:** Starten Sie einen zweiten Client und geben Sie den Namen `Berta` ein.
    * *Erwartung:* Im Terminal von Client A erscheint die Nachricht, dass `Berta` den Chat betreten hat. Im Terminal von Client B erscheint, dass `Anton` bereits im Chat ist (sofern die Implementierung dies unterstützt - die Basisimplementierung sendet nur eine "joined" Nachricht).
4. **Nachricht senden:** Geben Sie im Terminal von Client A `Hallo Welt!` ein und drücken Sie Enter.
    * *Erwartung:* Die Nachricht `Anton: Hallo Welt!` erscheint im Terminal von Client B.
5. **Client verlassen:** Geben Sie im Terminal von Client B `/quit` ein und drücken Sie Enter.
    * *Erwartung:* Im Terminal von Client A erscheint die Nachricht, dass `Berta` den Chat verlassen hat.

Damit ist die Funktionalität der Basis-Anwendung vollständig getestet.
