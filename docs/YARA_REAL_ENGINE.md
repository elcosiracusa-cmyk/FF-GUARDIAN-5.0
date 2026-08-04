# FFGuardian — YARA reale su Windows x64

## Stato

L'integrazione usa esclusivamente `yara64.exe` e `yarac64.exe` locali. Non modifica il PATH, non usa PowerShell o cmd.exe e non dichiara il motore attivo finché non superano:

1. `yara64.exe --version` con exit code 0;
2. presenza di almeno una regola `.yar`/`.yara`;
3. compilazione reale con `yarac64.exe`;
4. autotest innocuo con la regola `FFGuardian_Yara_Test`.

La release ufficiale configurata è YARA 4.5.8, progetto VirusTotal/YARA, licenza BSD-3-Clause. Il pacchetto binario non viene scaricato finché la pipeline di release non inserisce URL HTTPS ufficiale, SHA-256 reale, firma RSA-PSS del manifest e chiave pubblica di verifica.

## File principali

- `YaraCore27.cs`: configurazione, modelli, manifest verifier, process runner.
- `YaraServices27.cs`: parser, regole, health check, scanner e quarantena.
- `YaraInstallRuntime27.cs`: installazione, aggiornamento, rollback e runtime.
- `YaraUiIntegration27.cs`: UI della pagina Aggiornamenti.
- `Rules/Yara/ffguardian_core.yar`: regola innocua di autotest.
- `Assets/yara-engine-manifest.json`: manifest fail-closed della release.

## Manifest firmato

La pipeline di distribuzione deve:

1. scaricare l'asset Windows x64 dalla release ufficiale VirusTotal/YARA;
2. calcolare SHA-256;
3. scrivere URL, hash e nome asset nel manifest;
4. firmare i byte esatti del JSON con RSA-PSS/SHA-256;
5. pubblicare la firma Base64 in `yara-engine-manifest.sig`;
6. fornire soltanto la chiave pubblica tramite `FFGUARDIAN_YARA_MANIFEST_PUBLIC_KEY` o configurazione equivalente.

La chiave privata non deve mai essere inserita nel repository o nell'app.

## Build

```text
dotnet restore FFGuardian.sln
dotnet build FFGuardian.sln -c Release
dotnet publish FFGuardian.App/FFGuardian.App.csproj -c Release -r win-x64 --self-contained true
```

## Test obbligatori

| # | Scenario | Esito previsto |
|---|---|---|
| 1 | YARA assente | `YARA REALE: NON INSTALLATO` |
| 2 | Installazione automatica | download HTTPS, firma e SHA-256 validi, test finale riuscito |
| 3 | Controllo versione | versione reale letta da `--version` |
| 4 | Regola valida | compilazione `.yarc` riuscita |
| 5 | Regola non valida | `REGOLE NON VALIDE`, nessuna attivazione |
| 6 | File innocuo | zero corrispondenze |
| 7 | Stringa test | rilevata `FFGuardian_Yara_Test` |
| 8 | Spazi/caratteri speciali | percorso gestito tramite `ArgumentList` |
| 9 | Annullamento | processo terminato e `OperationCanceledException` gestita |
| 10 | Aggiornamento fallito | backup ripristinato automaticamente |
| 11 | Hash non valido | installazione bloccata prima dell'estrazione |
| 12 | Ripristino quarantena | SHA-256 verificato e conferma obbligatoria |

## Limitazioni intenzionali

- Nessuna regola pubblica viene scaricata automaticamente.
- Nessun file rilevato viene eliminato automaticamente.
- Nessuna esclusione Defender viene creata.
- Nessun certificato HTTPS non valido viene accettato.
- Nessun risultato simulato viene mostrato.
