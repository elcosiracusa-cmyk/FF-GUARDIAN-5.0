using System.Text.Json;

namespace FFGuardian.Engine10;

/// <summary>
/// Catalogo minimo incorporato e verificabile usato al primo avvio.
/// Contiene esclusivamente firme di test pubbliche e sicure; le firme malware
/// operative devono arrivare tramite il canale HTTPS firmato di FFGuardian.
/// </summary>
internal static class BaselineSignatureCatalog10
{
    public const string Version = "10.0.1-baseline-elco";
    public const string EicarSha256 = "275A021BBFB6489E54D471899F7DB9D1663FC695EC2FE2A2C4538AABF651FD0F";

    public static SignatureDatabaseDocument10 Create()
    {
        string generatedUtc = DateTime.UtcNow.ToString("O");
        string json = $$"""
        {
          "schemaVersion": 1,
          "databaseVersion": "{{Version}}",
          "generatedUtc": "{{generatedUtc}}",
          "signatures": [
            {
              "id": "ELCO-TEST-EICAR-SHA256",
              "sha256": "{{EicarSha256}}",
              "detectionName": "Test.EICAR",
              "confidence": 100,
              "enabled": true
            }
          ],
          "allowListSha256": []
        }
        """;

        SignatureDatabaseDocument10? document = JsonSerializer.Deserialize<SignatureDatabaseDocument10>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return document ?? throw new InvalidDataException(
            "Impossibile creare il database firme iniziale FFGuardian.");
    }
}
