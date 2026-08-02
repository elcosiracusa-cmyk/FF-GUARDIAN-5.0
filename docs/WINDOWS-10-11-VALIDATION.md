# FF GUARDIAN — Protocollo di validazione Windows 10/11

Questo protocollo completa il laboratorio GitHub con prove su PC o macchine virtuali desktop reali.

## Ambienti minimi

- Windows 10 22H2 x64, account standard e amministratore.
- Windows 11 23H2 o successivo x64, account standard e amministratore.
- Installazione pulita e aggiornamento da una versione precedente.
- Almeno una macchina con 4 GB RAM e CPU a 2 core.

## Barriere obbligatorie

1. Installer firmato o chiaramente marcato come build di test.
2. Avvio senza crash e senza report da 0 byte.
3. Health check con codice di uscita 0 per 10 esecuzioni consecutive.
4. Rilevamento EICAR normale e dentro ZIP.
5. Nessuna eliminazione automatica senza conferma.
6. Quarantena cifrata, ripristino esatto e rifiuto dei contenitori manomessi.
7. Protezione in tempo reale attiva dopo riavvio.
8. Ransom Shield avvisa senza terminare processi sulla sola euristica.
9. Nessun falso positivo su file Windows firmati selezionati.
10. Disinstallazione completa senza FFGuardian.exe, servizi, attività pianificate o voci di avvio residue.

## Sequenza di prova

- Installare FFGuardian.
- Riavviare Windows.
- Verificare stato protezione, database firme e numero cartelle monitorate.
- Eseguire scansione rapida, completa, file e cartella.
- Provare EICAR e archivio ZIP EICAR senza eseguire il campione.
- Confermare quarantena, nuova scansione e ripristino.
- Creare uno script artificiale sospetto e verificare il verdetto euristico.
- Collegare una chiavetta USB contenente soltanto file di prova innocui ed EICAR.
- Verificare che un file Microsoft firmato non venga segnalato come minaccia.
- Arrestare e riaprire il programma dieci volte.
- Aggiornare da una build precedente conservando impostazioni e quarantena.
- Disinstallare in modalità normale e silenziosa.

## Evidenze da conservare

- Versione Windows e build.
- SHA-256 dell'installer e dell'EXE.
- Risultati dei test e tempi di avvio.
- Screenshot dei verdetti EICAR.
- Registro quarantena e prova di ripristino.
- Elenco processi, servizi, attività pianificate e chiavi Run prima e dopo la disinstallazione.
- Tutti i report di crash o stabilità.

Una release non è approvata finché tutte le barriere risultano superate su entrambi i sistemi operativi.
