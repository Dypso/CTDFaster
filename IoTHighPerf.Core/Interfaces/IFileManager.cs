using System.Threading.Tasks;
using System.IO.Pipelines;

namespace IoTHighPerf.Core.Interfaces;

public interface IFileManager
{
    ValueTask<Memory<byte>> GetChunkAsync(string fileId, long offset, int size);
    ValueTask<bool> ValidateFileAsync(string fileId, string hash);
    ValueTask UpdateDailyVersionsAsync();
}