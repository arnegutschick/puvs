# Belegaufgabe 2025: Erweiterung eines Chat-Systems

Willkommen zur Belegaufgabe im Modul "Parallele und Verteilte Systeme". In dieser Aufgabe werden Sie die in der Vorlesung und den Übungen behandelten Konzepte praktisch anwenden, um ein bestehendes Chat-System zu erweitern.

## Ausgangssituation: Die Basis-Anwendung

Sie erhalten eine funktionierende Basis-Implementierung einer Chat-Anwendung. Dieses System besteht aus:

* Einem **ChatServer**: Eine Konsolenanwendung, die als zentrale Vermittlungsstelle dient.

* Einem **ChatClient**: Eine Konsolenanwendung, von der mehrere Instanzen gestartet werden können.

Die Kommunikation zwischen den Komponenten ist bereits mittels **RabbitMQ** und der Bibliothek **EasyNetQ** realisiert. Eine Anleitung zum Starten des Systems finden Sie in der `README.md` im Wurzelerzeichnis.

## Aufgabenstellung

Ihre Aufgabe ist es, die bereitgestellte Basis-Implementierung zu erweitern. Jede Arbeitsgruppe muss dabei insgesamt **fünf (5) Erweiterungen** umsetzen.

Gruppen mit nur **vier Teilnehmern** brauchen insgesamt nur **vier (4) Erweiterungen** umzusetzen.

> Mindestens **zwei (2) Erweiterungen** müssen bei **jeder** Arbeitsgruppe aus der Kategorie **"Mittlerer Aufwand"** stammen.

Diese Aufteilung stellt sicher, dass sowohl grundlegende als auch fortgeschrittenere Konzepte praktisch angewendet werden. Wählen Sie aus den folgenden Listen die von Ihnen favorisierten Erweiterungen aus.

---

## Kategorie: Leichter Aufwand

### 1. Zeitstempel für Nachrichten

* **Erweiterung:** Jede Nachricht wird mit einem serverseitig festgelegten Zeitstempel versehen, um eine einheitliche und nicht manipulierbare Chronologie der Konversation für alle Teilnehmer sicherzustellen.

* **Beschreibung:** Um Zeitunterschiede oder manipulierte Systemzeiten auf den Client-Rechnern zu umgehen, soll der Server zum alleinigen Zeitgeber werden. Sobald eine `ChatMessage` vom Server empfangen wird, ignoriert er einen eventuell vorhandenen Client-Zeitstempel und setzt stattdessen den genauen UTC-Zeitpunkt des Empfangs. Dieser Zeitstempel wird dann an alle Clients verteilt. In der Client-Anzeige sollte der Zeitstempel gut lesbar vor dem Benutzernamen stehen, z.B. `[14:32:55] Anton: Hallo Welt`. Dies schafft eine klare und nachvollziehbare Reihenfolge aller Nachrichten.  
Zugleich soll im Zuge dieser Änderung ein Teilnehmer seine eigenen Nachrichten nicht mehr erhalten.
* **Relevante Konzepte:**
  * **Woche 5 (Asynchrone Programmierung):** Der Datenfluss (`ChatMessage` kommt an, wird modifiziert und neu gesendet) ist eine Kernkomponente der asynchronen Verarbeitung.
  * **Verteilte Systeme:** Dieses Feature etabliert den Server als "Single Source of Truth" für die Zeit, ein wichtiges Konzept, um Konsistenz in verteilten Systemen zu gewährleisten.

### 2. Grundlegende Emoticons/Emojis

* **Erweiterung:** Unterstützung für eine kleine Menge von textbasierten Emoticons (z.B. `:)`, `:(`), die im Client durch Grafiken oder Unicode-Emojis ersetzt werden.

* **Beschreibung:** Um die Kommunikation emotionaler und visueller zu gestalten, soll der Client eine simple Textersetzung implementieren. Beispielsweise wird die Zeichenfolge `:)` vor der Anzeige auf der Konsole durch ein passendes Unicode-Emoji (z.B. `🙂`) oder eine andere grafische Darstellung ersetzt. Diese Transformation soll ausschließlich auf der Client-Seite stattfinden; der Server und die Kommunikation bleiben von dieser Logik unberührt. Eine kleine, feste Liste von 3-5 Emoticons ist für diese Aufgabe ausreichend.
* **Tipp:** Achten Sie darauf, dass die Ersetzung effizient geschieht und auch bei mehrfachen Emoticons in einer Nachricht korrekt funktioniert.
* **Relevante Konzepte:**
  * **Woche 5 (Asynchrone Programmierung, UI-Updates):** Obwohl es sich um eine Konsolenanwendung handelt, ist die Manipulation der Darstellung eine Form des UI-Updates, das im asynchronen Empfangs-Thread stattfindet.

### 3. Nachrichtenfilterung/Zensur

* **Erweiterung:** Implementierung einer grundlegenden, serverseitigen Filterung für bestimmte Schlüsselwörter, um unangemessene Inhalte vor der Verteilung an die Clients zu zensieren.

* **Beschreibung:** Der Server agiert als Moderator und prüft jede eingehende Nachricht auf eine Liste von unerwünschten Wörtern (Blacklist). Wird ein Wort aus der Liste gefunden, wird es unkenntlich gemacht (z.B. `schimpfwort` -> `***********`), bevor die Nachricht an die anderen Teilnehmer weitergeleitet wird. Dies erhöht die Kontrolle über die Kommunikation im öffentlichen Chat. Die Blacklist soll als separate Textdatei menschenlesbar und somit extern modifizierbar sein. Die Groß- und Kleinschreibung sollte bei der Prüfung ignoriert werden.
* **Tipp:** Listen mit "Schimpfwörtern" finden sich im Internet. Ich überlasse es Ihrer Phantasie diese zu finden.
* **Relevante Konzepte:**
  * **Woche 2 (Nebenläufigkeit, Server-seitige Verarbeitung):** Dies ist ein klassisches Beispiel für serverseitige Logik, die auf nebenläufig eintreffende Datenströme angewendet wird. Der Server agiert als "Filter" im Nachrichtenfluss.

### 4. Asynchrones Nachrichtensenden

* **Erweiterung:** Sicherstellen, dass das Senden von Nachrichten auf der Client-Seite vollständig asynchron erfolgt, um eine reaktionsfähige Benutzeroberfläche zu gewährleisten.  
Verbessern Sie in diesem Rahmen auch die Benutzeroberfläche der Chat-Anwendung, um eine benutzerfreundlichere Erfahrung zu bieten. Fügen Sie Funktionen wie automatisches Scrollen und Echtzeitaktualisierungen hinzu, um neue Nachrichten ohne manuelles Aktualisieren
anzuzeigen.

* **Beschreibung:** Auch in einer Konsolenanwendung kann das Warten auf eine I/O-Operation (wie das Senden einer Nachricht über das Netzwerk) die Anwendung blockieren. Ziel ist es, die `PublishAsync`-Methode von EasyNetQ korrekt mit `async/await` zu verwenden, sodass der Haupt-Thread des Clients theoretisch für andere Aufgaben frei bliebe. Auch wenn die `Console.ReadLine()`-Schleife blockierend ist, soll die eigentliche Sendeoperation non-blocking implementiert werden, wie es für GUI-Anwendungen unerlässlich wäre.  
Sie können in diesem Rahmen auch auf TUI-Oberflächen (wie [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui)) oder klassische GUI zurückgreifen.
* **Relevante Konzepte:**
  * **Woche 5 (Asynchrone Programmierung mit `async/await`):** Dies ist die Kernaufgabe, um I/O-gebundene Operationen (Netzwerkkommunikation) effizient und ohne Blockieren des aufrufenden Threads auszuführen.

### 5. Befehlsverarbeitung

* **Erweiterung:** Implementierung eines einfachen Befehlssystems, bei dem Benutzer mit `/` beginnende Kommandos eingeben können, z.B. `/help` oder `/time`.

* **Beschreibung:** Statt jede Eingabe als Chat-Nachricht zu behandeln, soll der Client Zeilen, die mit `/` beginnen, als Befehle interpretieren und mit Kommando-Nachrichten behandeln. Ein `/help`-Befehl soll eine Liste der verfügbaren Befehle nur für den anfragenden Benutzer anzeigen. Ein `/time`-Befehl soll den Server veranlassen, seine aktuelle Uhrzeit an den Client zurückzusenden. Dies erfordert eine gezielte Antwort vom Server an einen **einzelnen** Client.
* **Relevante Konzepte:**
  * **Woche 2 (Nebenläufigkeit):** Der Server verarbeitet Befehle parallel zu normalen Chat-Nachrichten.
  * **Architekturen:** Unterscheidung zwischen Publish/Subscribe (für öffentlichen Chat) und Request/Response (für gezielte Befehle).

### 6. Nutzerliste & Status

* **Erweiterung:** Eine Liste der aktuell verbundenen Nutzer wird beim Beitritt angezeigt und bei Änderungen (Verbinden/Trennen eines Nutzers) dynamisch aktualisiert.

* **Beschreibung:** Um den sozialen Aspekt des Chats zu stärken, sollen die Clients jederzeit wissen, wer online ist. Der Server ist die einzige Quelle der Wahrheit für diese Information. Er sendet eine "Nutzer-Status"-Nachricht, wann immer jemand den Chat betritt oder verlässt. Die Clients empfangen diese Benachrichtigung und geben sie in einer auffälligen Weise aus, z.B. `*** Berta hat den Chat betreten ***`, und aktualisieren die angezeigte Liste oder geben sie neu aus.
* **Relevante Konzepte:**
  * **Woche 2 (Nebenläufigkeit) & Woche 3 (Synchronisation):** Die zugrundeliegende Nutzerliste auf dem Server muss thread-sicher sein, da Logins und Logouts gleichzeitig stattfinden können.
  * **Woche 5 (Asynchrone Programmierung):** Das Broadcasting von Status-Updates ist ein typischer Anwendungsfall des asynchronen Publish/Subscribe-Musters.

---

## Kategorie: Mittlerer Aufwand

*(Wählen Sie mindestens zwei aus dieser Kategorie)*

### 7. Eindeutige Benutzernamen & Farbkodierung

* **Erweiterung:** Sicherstellen, dass jeder Nutzer einen eindeutigen Benutzernamen (ohne Leerzeichen) hat und ihm serverseitig eine unveränderliche Farbe zugewiesen wird, in der seine Nachrichten erscheinen.

* **Beschreibung:** Anonyme oder doppelte Benutzernamen sollen verhindert werden. Der Server lehnt Verbindungsversuche mit bereits vergebenen Namen strikt ab und informiert den Client darüber. Bei Erfolg weist der Server dem Nutzer eine Farbe aus einem vordefinierten Pool zu (z.B. zyklisch). Diese Farbe wird Teil jeder Nachricht dieses Nutzers, sodass der Client den Absender schnell visuell identifizieren kann. Beispiel: Antons Nachrichten erscheinen immer blau, Bertas immer grün.
* **Relevante Konzepte:**
  * **Woche 3 (Synchronisation von Server-Zustand):** Der kritischste Teil ist der atomare "prüfen und einfügen"-Vorgang in der Nutzerliste, um Race Conditions bei gleichzeitigen Login-Versuchen mit demselben Namen zu verhindern.

### 8. Private Nachrichten (1:1 Chat)

* **Erweiterung:** Nutzer können private Nachrichten an einen bestimmten anderen Nutzer senden (z.B. über `/msg <Empfängername> <Nachricht>`), die für andere unsichtbar sind.

* **Beschreibung:** Zusätzlich zum öffentlichen Chat soll eine private Kommunikation möglich sein. Der Client parst die Benutzereingabe. Erkennt er das `/msg`-Kommando, wird eine spezielle "private" Nachricht an den Server gesendet. Der Server darf diese Nachricht nicht an alle verteilen, sondern muss den benannten Empfänger validieren und die Nachricht gezielt nur an diesen einen Client weiterleiten. Sender und Empfänger sollten die private Nachricht klar als solche erkennen können, z.B. durch eine andere Farbe oder einen Hinweis wie `[privat von Anton]`.
* **Relevante Konzepte:**
  * **Woche 10 (Architekturen, Nachrichten-Routing):** Dies ist eine Kernaufgabe zum Verständnis von Nachrichtenmustern jenseits von simplem Broadcasting, insbesondere Direct- oder Topic-Exchanges.

### 9. Persistente Chat-Historie

* **Erweiterung:** Eine begrenzte Anzahl der letzten öffentlichen Nachrichten (z.B. die letzten 20, soll konfigurierbar sein) wird auf dem Server gespeichert und an neue Nutzer bei deren Beitritt gesendet.

* **Beschreibung:** Damit neu verbundene Benutzer nicht in eine leere Konversation einsteigen, soll der Server eine kurze Historie der letzten öffentlichen Nachrichten vorhalten. Direkt nach einem erfolgreichen Login sendet der Server dem neuen Client diese Nachrichten-Historie als eine Serie von Nachrichten oder als ein gebündeltes Objekt. Erst danach empfängt der Client die Live-Nachrichten. Dies verbessert den Einstieg in eine laufende Diskussion erheblich.
* **Relevante Konzepte:**
  * **Woche 3 (Synchronisation):** Der Puffer für die Historie ist eine geteilte Ressource, die von allen Nachrichten-Threads sicher beschrieben werden muss.
  * **Datenstrukturen:** Die Wahl einer geeigneten, nebenläufigen Datenstruktur ist entscheidend.

### 10. Statistik-Funktion

* **Erweiterung:** Implementierung einer serverseitigen Statistik-Funktion, die auf Anfrage Nutzungsdaten des Chats anzeigt.

* **Beschreibung:** Um Einblicke in die Chat-Aktivität zu erhalten, soll der Server die Anzahl der von jedem Benutzer gesendeten Nachrichten protokollieren. Ein Benutzer kann jederzeit den Befehl `/statistik` eingeben. Als Reaktion darauf berechnet der Server aktuelle Statistiken und sendet sie an den anfragenden Client zurück, der sie dann anzeigt.  
Die Statistiken sollen

  * die Gesamtzahl aller Nachrichten,
  * die durchschnittliche Anzahl von Nachrichten pro Benutzer und
  * eine Top-3-Liste der aktivsten Chatter

  enthalten.
* **Relevante Konzepte:**
  * **Woche 3 (Synchronisation):** Die Statistik-Datenbank ist eine geteilte Ressource, die sicher von nebenläufigen Nachrichten-Threads aktualisiert werden muss. `ConcurrentDictionary` ist hierfür ideal.
  * **Architekturen:** Erneute Anwendung des Request/Response-Musters für eine gezielte Datenabfrage.
  * **Datenverarbeitung:** Nutzung von LINQ zur Aggregation und Auswertung von Daten auf dem Server.

### 11. Grundlegende Transportverschlüsselung

* **Erweiterung:** Die Kommunikation zwischen Client und Server wird durch eine grundlegende Verschlüsselung geschützt, sodass die Nachrichteninhalte nicht im Klartext über das Netzwerk gesendet werden.

* **Beschreibung:** Um die Sicherheit und den Datenschutz zu erhöhen, sollen die Inhalte von `ChatMessage`-Objekten verschlüsselt werden. Anstatt echter Ende-zu-Ende-Verschlüsselung wird ein hybrider Ansatz empfohlen: Jeder Client sendet beim Login seinen öffentlichen RSA-Schlüssel an den Server. Der Server generiert für jeden Client einen einzigartigen symmetrischen AES-Schlüssel, verschlüsselt diesen mit dem öffentlichen Schlüssel des Clients und sendet ihn zurück. Alle weiteren Nachrichtentexte vom Client zum Server (und umgekehrt) werden mit diesem AES-Schlüssel ver- und entschlüsselt.
* Tipps für die **technische Umsetzung:**
  * **Kryptografie-API:** Nutzen Sie die Klassen aus `System.Security.Cryptography` in .NET (z.B. `RSA`, `AES`).
  * **Handshake:** Der Login-Prozess (RPC) muss um den Schlüsselaustausch erweitert werden.
  * **Server & Client:** Beide müssen Logik zum Ver- und Entschlüsseln der `Text`-Eigenschaft von Nachrichten implementieren. Die symmetrischen Schlüssel müssen serverseitig sicher den jeweiligen Clients zugeordnet werden.
  * **Contracts:** Das `ChatMessage`-Objekt könnte anstelle eines `string` ein `byte[]` für den verschlüsselten Inhalt verwenden.
* **Relevante Konzepte:**
  * **Sicherheit in verteilten Systemen:** Grundlagen der symmetrischen und asymmetrischen Verschlüsselung und des Schlüsselaustauschs.
  * **Woche 10 (Architekturen):** Implementierung eines sicheren Handshake-Protokolls.

### 12. Einfacher Dateiaustausch

* **Erweiterung:** Ermöglichen, dass Nutzer kleine Textdateien (oder in Base64 kodierte Bilder) über den Chat senden können.

* **Beschreibung:** Benutzer sollen die Möglichkeit haben, eine Datei durch Angabe des lokalen Pfades (z.B. `/sendfile <pfad>`) zu versenden. Der Client liest diese Datei ein, kodiert ihren Inhalt als Base64-String und versendet sie in einem speziellen `FileMessage`-Objekt. Andere Clients empfangen diese Nachricht, erkennen sie als Datei und bieten dem Benutzer an, sie unter dem ursprünglichen Dateinamen zu speichern. Aufgrund von Nachrichtengrößen-Limits sollte dies auf kleine Dateien (`< 1MB`) beschränkt sein.
* **Relevante Konzepte:**
  * **Woche 5 (Asynchrones File-I/O):** Das Lesen und Schreiben von Dateien sollte asynchron erfolgen, um die Anwendung nicht zu blockieren.
  * **Woche 10 (Architekturen, Nachrichten-Payload):** Umgang mit binären Daten und größeren Nachrichten-Nutzlasten in einem Nachrichtensystem.

### 13. Resilienz: Grundlegende Wiederverbindungslogik

* **Erweiterung:** Wenn der Client die Verbindung zum Server (bzw. RabbitMQ) verliert, werden automatische Wiederverbindungsversuche mit einer exponentiellen Verzögerung implementiert.

* **Beschreibung:** Netzwerkprobleme sind in verteilten Systemen normal. Der Client soll bei einem Verbindungsabbruch nicht einfach abstürzen, sondern autonom versuchen, die Verbindung wiederherzustellen. Um den Server bei einem Massen-Ausfall nicht zu überlasten, soll die Wartezeit zwischen den Versuchen exponentiell ansteigen (z.B. 2s, 4s, 8s, ...). Der Benutzer sollte über diese Versuche informiert werden.
* Tipps für die **technische Umsetzung:**
  * Die manuelle Implementierung von Retry-Logiken ist komplex. Die Verwendung einer Resilienz-Bibliothek wie **Polly** wird dringend empfohlen. Die Aufgabe besteht dann darin, die Bibliothek zu integrieren und eine passende `WaitAndRetryAsync`-Policy zu konfigurieren, die den initialen Verbindungsaufbau (`RabbitHutch.CreateBus`) und potenziell auch fehlgeschlagene Sende-Operationen umschließt.
  * **Client:** Die Policy muss so konfiguriert werden, dass sie auf spezifische Exceptions (z.B. `BrokerUnreachableException` von EasyNetQ) reagiert. Im `onRetry`-Handler der Policy kann eine Statusmeldung für den Benutzer ausgegeben werden.
* **Relevante Konzepte:**
  * **Woche 9 (Fehlertoleranz und Resilienz):** Kernkonzept von verteilten Systemen. Anwendung der Retry-Strategie (Exponential Backoff), um robuste Clients zu bauen.
  * **Bibliotheks-Anwendung:** Fähigkeit, eine externe Bibliothek (Polly) zur Lösung eines komplexen Problems zu nutzen.

---

## 3. Allgemeine Anforderungen an die Abgabe (Wie ist abzugeben?)

Zusätzlich zur funktionalen Implementierung werden die folgenden qualitativen Anforderungen an die Abgabe gestellt.

* **Stellen Sie sicher, dass Ihr Code lauffähig ist.** Achten Sie auf eine saubere und nachvollziehbare Code-Struktur.

* **Code-Stil und Inline-Dokumentation:** Der gesamte Code muss den offiziellen [C# Coding Conventions von Microsoft](https://docs.microsoft.com/de-de/dotnet/csharp/fundamentals/coding-style/coding-conventions) folgen. Zusätzlich müssen **alle öffentlichen Klassen, Methoden und Eigenschaften** eine aussagekräftige XML-Inline-Dokumentation (`/// <summary>...`) aufweisen. Alle Compiler-Warnungen müssen behoben sein.
* **Konfigurationsdatei:** Hardcodierte "magische Werte" wie der Hostname von RabbitMQ oder die Anzahl der Nachrichten in der Chat-Historie sind zu vermeiden. Diese Werte sollen stattdessen aus einer `appsettings.json`-Datei ausgelesen werden.
* **Umfassende Fehlerbehandlung:** Die Anwendung muss auch außerhalb des "Happy Path" robust sein. Behandeln Sie mögliche Fehlerzustände sinnvoll (z.B. was passiert, wenn ein ungültiger Befehl eingegeben oder ein nicht-existierender Nutzer angeschrieben wird?).
* **Dokumentation**
   Die Dokumentation als PDF-Datei im Wurzelverzeichnis Ihres Projekts muss folgende Punkte beinhalten:
    1. Eine Auflistung der **von Ihnen implementierten Erweiterungen**.
    2. Für jede dieser Erweiterungen: Eine kurze Beschreibung Ihrer **Design-Entscheidungen**.
    3. **Architektur-Diagramm:** Erstellen Sie für **eine** Ihrer "Mittlerer Aufwand"-Erweiterungen ein einfaches Diagramm (z.B. als PNG-Datei), das die beteiligten Komponenten und den Nachrichtenfluss visualisiert.
    4. Eine Tabelle oder Liste, die die **Aufteilung der Aufgaben auf die Teammitglieder** darstellt (z.B. "Feature X: Konzeption und Server-Logik (Max Mustermann), Client-Implementierung und Tests (Erika Mustermann)").
    5. Eine **"Lessons Learned"** Abschnitt der Ihre Herausforderungen und Erfahrungen beschreibt.
* **Abgabeformat:** Die gesamte Abgabe erfolgt als **einzelnes ZIP-Archiv**, das aller Artefakte (Code und Dokumentation) enthält.
* Bereiten Sie sich darauf vor, Ihre Ergebnisse (Code + Funktionalität in **kleinem Szenario**) im Januar 2026 zu präsentieren.

**Viel Erfolg!**
