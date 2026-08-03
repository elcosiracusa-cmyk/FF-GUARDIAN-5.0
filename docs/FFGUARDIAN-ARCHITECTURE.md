# FFGUARDIAN — Architettura commerciale

## Obiettivo

Separare interfaccia, servizio privilegiato, Engine10, protezioni attive, archiviazione e aggiornamenti. La UI non deve eseguire operazioni privilegiate o analisi pesanti.

## Moduli

```text
FFGuardian.UI.exe
    |
    | Named Pipes autenticati + messaggi versionati
    v
FFGuardian.Service.exe
    |-- Engine10 Coordinator
    |-- Real-Time Protection
    |-- Ransom Shield
    |-- USB Shield
    |-- Firewall Manager
    |-- Quarantine Manager
    |-- Update Manager
    |-- Event Store
    |
    | Filter communication port (fase driver)
    v
FFGuardian.Minifilter.sys
```

## Responsabilità

### UI

- Dashboard e navigazione.
- CPU, RAM, disco, stato motori e firme.
- Comandi scansione rapida, completa e personalizzata.
- Cronologia, rapporti e quarantena.
- Nessun accesso diretto a driver, quarantena o chiavi.

### Windows Service

- Autorizzazione delle richieste della UI.
- Coordinamento delle scansioni.
- Persistenza di log e configurazione.
- Comunicazione con minifilter e Windows Filtering Platform.
- Gestione quarantena e aggiornamenti atomici.

### Engine10

Pipeline consigliata:

1. Normalizzazione percorso e controllo esclusioni.
2. Cache su File ID, dimensione e data modifica.
3. SHA-256 e confronto firme locali.
4. Verifica Authenticode.
5. YARA precompilato.
6. ClamAV opzionale con timeout.
7. Analisi statica ed euristica.
8. Decisione aggregata con confidence score.

### Ransom Shield

Segnali combinati:

- frequenza elevata di scrittura;
- rinomina massiva;
- aumento di entropia;
- diffusione su cartelle sensibili;
- processo non firmato o appena scaricato;
- manomissione di backup o copie shadow.

Le azioni distruttive richiedono conferma, tranne policy amministrative esplicite. Il servizio conserva sempre log e possibilità di rollback.

### USB Shield

- rilevamento montaggio volume;
- identificazione seriale e policy dispositivo;
- controllo autorun, shortcut, script ed eseguibili;
- scansione rapida automatica o previa conferma.

### Firewall Manager

- gestione tramite Windows Filtering Platform;
- regole per applicazione, protocollo, porta e profilo rete;
- log connessioni e associazione processo/endpoint.

## IPC

Canali:

```text
FFGuardian.Control.v1
FFGuardian.Events.v1
```

Requisiti:

- ACL per SYSTEM, amministratori e utente autorizzato;
- verifica token del client;
- protocollo versionato;
- messaggi con dimensione massima;
- timeout e cancellazione;
- validazione e normalizzazione di ogni percorso.

## Archiviazione

```text
%ProgramData%\FFGuardian\Data\guardian.db
%ProgramData%\FFGuardian\Quarantine\objects\
%ProgramData%\FFGuardian\Signatures\
```

SQLite conserva eventi, sessioni, risultati, quarantena e impostazioni. Le firme hash su larga scala devono essere archiviate separatamente in un formato binario indicizzato o memory-mapped.

Gli oggetti in quarantena devono essere cifrati, rinominati con ID casuale, protetti tramite ACL e verificati con SHA-256. La chiave principale è gestita dal servizio e protetta tramite DPAPI macchina.

## Updater

1. Download manifest tramite HTTPS.
2. Verifica firma crittografica.
3. Controllo anti-downgrade.
4. Download in staging.
5. Verifica SHA-256.
6. Test regole YARA e pacchetto firme.
7. Sostituzione atomica.
8. Health check.
9. Rollback automatico.

## Fasi di realizzazione

### Fase 1 — User mode stabile

- UI commerciale unica.
- Windows Service.
- IPC autenticato.
- Engine10 modulare.
- SQLite.
- Quarantena cifrata.
- YARA e ClamAV.
- Updater firmato.

### Fase 2 — Driver

- File-system minifilter WDK.
- Comunicazione driver-servizio.
- Ransom Shield preventivo.
- Test di stress, Driver Verifier e HLK.

### Fase 3 — Distribuzione commerciale

- firma digitale del software;
- installer MSI/WiX;
- canali Stable e Beta;
- laboratorio falsi positivi;
- aggiornamenti differenziali;
- telemetria esclusivamente su consenso.
