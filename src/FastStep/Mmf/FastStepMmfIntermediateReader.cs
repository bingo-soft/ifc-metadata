using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Bingosoft.Net.IfcMetadata.FastStep.Mmf;

internal sealed class FastStepMmfIntermediateReader : IDisposable
{
    private readonly string _directoryPath;
    private readonly Dictionary<int, FastStepEntityRecord> _entityById = [];
    private readonly Dictionary<uint, StringSegmentAccessor> _stringSegments = [];

    private bool _disposed;

    internal FastStepMmfIntermediateReader(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(directoryPath));
        }

        _directoryPath = directoryPath;
        LoadEntityIndex();
    }

    internal string DirectoryPath => _directoryPath;

    internal bool TryGetEntityRecord(int entityId, out FastStepEntityRecord record)
    {
        ThrowIfDisposed();
        return _entityById.TryGetValue(entityId, out record);
    }

    internal IEnumerable<FastStepObjectRecord> EnumerateObjectRecords()
    {
        ThrowIfDisposed();

        var files = Directory.GetFiles(_directoryPath, "object_*.seg", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);

        var recordSize = Marshal.SizeOf<FastStepObjectRecord>();
        for (var i = 0; i < files.Length; i++)
        {
            using var stream = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var mmf = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            accessor.Read(0, out FastStepSegmentHeader header);
            ValidateHeader(header, FastStepStoreKind.Object);

            var offset = checked((long)header.DataStartOffset);
            var end = checked((long)header.WriteOffset);
            while (offset + recordSize <= end)
            {
                accessor.Read(offset, out FastStepObjectRecord record);
                yield return record;
                offset += recordSize;
            }
        }
    }

    internal bool TryReadRawArguments(int entityId, out string rawArguments)
    {
        ThrowIfDisposed();

        if (!_entityById.TryGetValue(entityId, out var record))
        {
            rawArguments = null;
            return false;
        }

        var payloadAddress = FastStepMmfAddressCodec.Unpack(record.ArgOffset);
        if (record.ArgLength <= 0)
        {
            rawArguments = string.Empty;
            return true;
        }

        var bytes = ReadStringPayload(payloadAddress, record.ArgLength);
        rawArguments = Encoding.UTF8.GetString(bytes);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var accessor in _stringSegments.Values)
        {
            accessor.Dispose();
        }

        _stringSegments.Clear();
        _entityById.Clear();
        _disposed = true;
    }

    private void LoadEntityIndex()
    {
        var files = Directory.GetFiles(_directoryPath, "entity_*.seg", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);

        var recordSize = Marshal.SizeOf<FastStepEntityRecord>();
        for (var i = 0; i < files.Length; i++)
        {
            LoadEntitySegment(files[i], recordSize);
        }
    }

    private void LoadEntitySegment(string path, int recordSize)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var mmf = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
        using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        accessor.Read(0, out FastStepSegmentHeader header);
        ValidateHeader(header, FastStepStoreKind.Entity);

        var offset = checked((long)header.DataStartOffset);
        var end = checked((long)header.WriteOffset);
        while (offset + recordSize <= end)
        {
            accessor.Read(offset, out FastStepEntityRecord record);
            _entityById[record.EntityId] = record;
            offset += recordSize;
        }
    }

    private byte[] ReadStringPayload(FastStepSegmentAddress address, int byteLength)
    {
        if (!_stringSegments.TryGetValue(address.SegmentId, out var accessor))
        {
            accessor = OpenStringSegment(address.SegmentId);
            _stringSegments[address.SegmentId] = accessor;
        }

        var remaining = checked((long)accessor.Header.WriteOffset - address.Offset);
        if (byteLength > remaining)
        {
            throw new InvalidDataException("String payload exceeds written segment range.");
        }

        var buffer = new byte[byteLength];
        accessor.Accessor.ReadArray(address.Offset, buffer, 0, byteLength);
        return buffer;
    }

    private StringSegmentAccessor OpenStringSegment(uint segmentId)
    {
        var path = Path.Combine(_directoryPath, $"string_{segmentId:D6}.seg");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("String MMF segment was not found.", path);
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var mmf = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: false);
        var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        accessor.Read(0, out FastStepSegmentHeader header);
        ValidateHeader(header, FastStepStoreKind.String);

        return new StringSegmentAccessor(mmf, accessor, header);
    }

    private static void ValidateHeader(FastStepSegmentHeader header, FastStepStoreKind expectedStoreKind)
    {
        const uint segmentMagic = 0x4D474553;
        const ushort segmentVersion = 1;

        if (header.Magic != segmentMagic)
        {
            throw new InvalidDataException("Invalid MMF segment magic.");
        }

        if (header.Version != segmentVersion)
        {
            throw new InvalidDataException($"Unsupported MMF segment version '{header.Version}'.");
        }

        if (header.StoreKind != (ushort)expectedStoreKind)
        {
            throw new InvalidDataException("MMF segment store kind mismatch.");
        }

        if (header.WriteOffset < header.DataStartOffset)
        {
            throw new InvalidDataException("MMF segment offsets are invalid.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class StringSegmentAccessor : IDisposable
    {
        internal StringSegmentAccessor(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, FastStepSegmentHeader header)
        {
            Mmf = mmf;
            Accessor = accessor;
            Header = header;
        }

        internal MemoryMappedFile Mmf { get; }

        internal MemoryMappedViewAccessor Accessor { get; }

        internal FastStepSegmentHeader Header { get; }

        public void Dispose()
        {
            Accessor.Dispose();
            Mmf.Dispose();
        }
    }
}
