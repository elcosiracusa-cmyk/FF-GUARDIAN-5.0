using System.Net;
using System.Text.RegularExpressions;

namespace FFGuardian;

internal static class ErrorMessageFormatter
{
    public static (string Message, MessageBoxIcon Icon) Format(Exception exception)
    {
        string raw = exception.ToString();
        string decoded = WebUtility.HtmlDecode(raw);
        string normalized = Regex.Replace(decoded, "<[^>]+>", " ");
        normalized = Regex.Replace(normalized, @"_x[0-9A-Fa-f]{4}_", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        bool defenderBusy = ContainsAny(normalized,
            "Start-MpScan",
            "analisi in corso",
            "scansione in corso",
            "scan is already running",
            "another scan is already running",
            "MI_RESULT 16");

        if (defenderBusy)
        {
            return (
                "Microsoft Defender sta già eseguendo una scansione.\n\n" +
                "FF GUARDIAN non avvierà una seconda analisi contemporaneamente. " +
                "Attendi il completamento della scansione in corso e riprova tra qualche minuto.\n\n" +
                "Il PC continua a essere protetto.",
                MessageBoxIcon.Information);
        }

        bool accessDenied = ContainsAny(normalized,
            "accesso negato",
            "access is denied",
            "unauthorizedaccessexception");

        if (accessDenied)
        {
            return (
                "L'operazione richiede i privilegi di amministratore.\n\n" +
                "Chiudi FF GUARDIAN e riaprilo scegliendo «Esegui come amministratore».",
                MessageBoxIcon.Warning);
        }

        string safe = normalized;
        if (safe.Length > 700)
            safe = safe[..700] + "…";

        return (
            string.IsNullOrWhiteSpace(safe)
                ? "Si è verificato un errore controllato. Riprova l'operazione."
                : safe,
            MessageBoxIcon.Error);
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
