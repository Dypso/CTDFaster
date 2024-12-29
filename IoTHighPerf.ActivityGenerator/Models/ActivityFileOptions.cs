namespace IoTHighPerf.ActivityGenerator.Models;

public class ActivityFileOptions
{
    public const string DefaultCebConcentrateur = "CTD_API-----------"; // 19 caractères
    public const int MaxBatchSize = 10000;
    public const int BatchTimeoutSeconds = 60;
    public const string TopicName = "audit";

    public string OutputPath { get; set; } = "activities";
    public string CounterFilePath { get; set; } = "activities/counter.dat";
    public string TempPath { get; set; } = "activities/temp";
    public string CebConcentrateur { get; set; } = DefaultCebConcentrateur;
}