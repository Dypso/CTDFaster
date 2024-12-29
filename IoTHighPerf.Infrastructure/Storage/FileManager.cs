using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Tasks;
using IoTHighPerf.Core.Interfaces;
using Microsoft.Extensions.ObjectPool;

namespace IoTHighPerf.Infrastructure.Storage;

public class ByteArrayPooledObjectPolicy : IPooledObjectPolicy<byte[]>
{
    private readonly int _arraySize;

    public ByteArrayPooledObjectPolicy(int arraySize)
    {
        _arraySize = arraySize;
    }

    public byte[] Create()
    {
        return new byte[_arraySize];
    }

    public bool Return(byte[] obj)
    {
        // On peut réutiliser le tableau tel quel
        // Array.Clear(obj, 0, obj.Length); // Décommenter si vous voulez nettoyer le tableau
        return true;
    }
}

public sealed class FileManager : IFileManager
{
    private readonly ObjectPool<byte[]> _bufferPool;
    private readonly ConcurrentDictionary<string, byte[]> _hotCache;
    
    public FileManager()
    {
   	var policy = new ByteArrayPooledObjectPolicy(4096);
    	_bufferPool = new DefaultObjectPool<byte[]>(policy);

        //_bufferPool = ObjectPool.Create<byte[]>(new ByteArrayPooledObjectPolicy(4096));
        _hotCache = new ConcurrentDictionary<string, byte[]>();
    }

    public ValueTask<Memory<byte>> GetChunkAsync(string fileId, long offset, int size)
    {
        if (_hotCache.TryGetValue(fileId, out var data))
        {
            return new ValueTask<Memory<byte>>(new Memory<byte>(data, (int)offset, size));
        }

        var buffer = _bufferPool.Get();
        try
        {
            // Simuler le chargement des données (à implémenter avec le vrai stockage)
            return new ValueTask<Memory<byte>>(new Memory<byte>(buffer, 0, size));
        }
        finally
        {
            _bufferPool.Return(buffer);
        }
    }

    public ValueTask<bool> ValidateFileAsync(string fileId, string hash)
    {
        // Implémenter la validation du hash
        return new ValueTask<bool>(true);
    }

    public ValueTask UpdateDailyVersionsAsync()
    {
        // Mettre à jour les versions quotidiennes des fichiers
        return ValueTask.CompletedTask;
    }
}