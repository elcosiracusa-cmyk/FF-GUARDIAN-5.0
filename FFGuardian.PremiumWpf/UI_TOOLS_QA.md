# FFGuardian — Contratto QA pagina Strumenti

La pagina `ToolsView` deve mantenere i binding e i command reali già disponibili nel `MainViewModel`.

Controlli obbligatori:

- `ProtectionText` e `ProtectionDetail` visibili con contrasto elevato.
- `Components` utilizzato sia per lo stato sistema sia per le card componenti.
- `RefreshCommand` collegato a Verifica strumenti e Aggiorna stato.
- `NavigateCommand` collegato ai dettagli e alla risoluzione problemi.
- apertura log limitata a `%LocalAppData%/FFGuardian/Logs`.
- nessun valore operativo simulato: `IsOperational` determina verde, rosso o warning.
- tutti i testi espliciti usano `TextBrush` o `MutedBrush`.
- nessun testo nero su sfondo scuro.
- `AutomationProperties.Name`, tooltip e navigazione Tab presenti sulle azioni.

Risoluzioni da verificare nel test manuale: 1366×768, 1600×900, 1920×1080, 2560×1440.
DPI da verificare: 125%, 150%, 175%.
