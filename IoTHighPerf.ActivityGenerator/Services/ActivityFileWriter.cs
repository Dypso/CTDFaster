using System.Buffers.Binary;
using IoTHighPerf.ActivityGenerator.Models;
using Microsoft.Extensions.Options;
using System.Text;

namespace IoTHighPerf.ActivityGenerator.Services;

public class ActivityFileWriter : IActivityFileWriter
{
    private readonly ActivityFileOptions _options;
    private readonly ICounterManager _counterManager;

    public ActivityFileWriter(
        IOptions<ActivityFileOptions> options,
        ICounterManager counterManager)
    {
        _options = options.Value;
        _counterManager = counterManager;
    }

    public async Task<string> GenerateActivityFileAtomicAsync(
        IEnumerable<AuditEntry> entries, 
        CounterState counterState)
    {
        if (!entries.Any()) return string.Empty;

        var timestamp = entries.First().Timestamp;
        var date = DateOnly.FromDateTime(timestamp);
        var daysSince1987 = (date.ToDateTime(TimeOnly.MinValue) - new DateTime(1987, 1, 1)).Days;
        var counter = counterState.Counter + 1;

        var fileName = $"S{_options.CebConcentrateur}{ConvertToBase36(daysSince1987, 3)}{ConvertToBase36(counter, 4)}.BIN";
        var tempPath = Path.Combine(_options.TempPath, $"{fileName}.tmp");
        var finalPath = Path.Combine(_options.OutputPath, fileName);

        await WriteActivityFileAsync(tempPath, entries);
        File.Move(tempPath, finalPath, false);
        
        await _counterManager.SaveCounterAsync(new CounterState(date, counter, fileName));
        return fileName;
    }

    public async Task WriteActivityFileAsync(string filePath, IEnumerable<AuditEntry> entries)
    {
        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await using var writer = new BinaryWriter(fileStream);

        foreach (var entry in entries)
        {
            WriteNCRecord(writer, entry);
        }
    }

    private void WriteNCRecord(BinaryWriter writer, AuditEntry entry)
    {
        // Type d'enregistrement (2 ASCII)
        writer.Write(Encoding.ASCII.GetBytes("NC"));

        // Longueur totale (35 octets hors type et longueur)
        const ushort recordLength = 35;
        writer.Write(BinaryPrimitives.ReverseEndianness(recordLength));

        // Date (4 HEXA) - AAAAMMJJ
        var dateBytes = new byte[4];
        BinaryPrimitives.WriteInt16BigEndian(dateBytes.AsSpan(0), (short)entry.Timestamp.Year);
        dateBytes[2] = (byte)entry.Timestamp.Month;
        dateBytes[3] = (byte)entry.Timestamp.Day;
        writer.Write(dateBytes);

        // Heure (3 HEXA) - HHMMSS
        writer.Write(new byte[] 
        {
            (byte)entry.Timestamp.Hour,
            (byte)entry.Timestamp.Minute,
            (byte)entry.Timestamp.Second
        });

        // Durée de l'échange (2 HEXA)
        writer.Write((ushort)0); // Durée fixe car API REST

        // Code emplacement billettique (19 ASCII)
        var deviceIdPadded = entry.DeviceId.PadRight(19, ' ');
        writer.Write(Encoding.ASCII.GetBytes(deviceIdPadded));

        // Sens de la connexion (1 HEXA) - toujours 1 (device vers serveur)
        writer.Write((byte)1);

        // Nature de l'échange (1 HEXA)
        writer.Write(GetExchangeNature(entry.Type));

        // Compte-rendu de l'échange (1 HEXA) - toujours 0 (succès)
        writer.Write((byte)0);

        // Fichiers d'activités et paramètres (4 x 1 HEXA)
        writer.Write((byte)(entry.Type == "DOWNLOAD" ? 1 : 0)); // À décharger
        writer.Write((byte)(entry.Type == "DOWNLOAD" ? 1 : 0)); // Déchargés
        writer.Write((byte)0); // Paramètres à transférer
        writer.Write((byte)0); // Paramètres transférés
    }

    private static byte GetExchangeNature(string auditType) => auditType switch
    {
        "TIME" => 1,
        "MANIFEST" => 2,
        "DOWNLOAD" => 3,
        "CONFIRM" => 4,
        _ => 0
    };

    private static string ConvertToBase36(int value, int length)
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var result = new char[length];
        
        for (int i = length - 1; i >= 0; i--)
        {
            result[i] = chars[value % 36];
            value /= 36;
        }
        
        return new string(result);
    }
}