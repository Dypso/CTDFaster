using System.Threading.Tasks;

namespace IoTHighPerf.Core.Interfaces;

public interface IAuditLogger
{
    ValueTask LogManifestRequestAsync(string deviceId);
    ValueTask LogDownloadAsync(string deviceId, string fileId, long offset, int size);
    ValueTask LogConfirmationAsync(string deviceId, string fileId);

    ValueTask LogTimeSyncAsync(string deviceId);

}