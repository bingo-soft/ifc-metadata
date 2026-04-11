using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Bingosoft.Net.IfcMetadata.FastStep.Mmf;

internal sealed class FastStepMmfSegmentManager : IDisposable
{
    private const uint SegmentMagic = 0x4D474553; // SEGM
    private const ushort SegmentVersion = 1;

    private readonly object _sync = new();
    private readonly string _rootDirectory;
    private readonly string _storePrefix;
    private readonly FastStepStoreKind _storeKind;
    private readonly long _segmentSize;
    private readonly Dictionary<uint, SegmentState> _writableSegments = [];

    private uint _currentSegmentId;
    private bool _disposed;

    internal FastStepMmfSegmentManager(
        string rootDirectory,
        string storePrefix,
        FastStepStoreKind storeKind,
        long segmentSize = 256L * 1024 * 1024)
    {
        if (segmentSize <= FastStepSegmentHeader.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentSize));
        }

        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _storePrefix = string.IsNullOrWhiteSpace(storePrefix) ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(storePrefix)) : storePrefix;
        _storeKind = storeKind;
        _segmentSize = segmentSize;

        Directory.CreateDirectory(_rootDirectory);
        _currentSegmentId = 0;
        _writableSegments[_currentSegmentId] = OpenSegment(_currentSegmentId, readOnly: false);
    }

    internal FastStepSegmentAddress Append(ReadOnlySpan<byte> payload)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            var current = _writableSegments[_currentSegmentId];
            var writeOffset = checked((long)current.Header.WriteOffset);
            if (writeOffset + payload.Length > _segmentSize)
            {
                _currentSegmentId++;
                current = OpenSegment(_currentSegmentId, readOnly: false);
                _writableSegments[_currentSegmentId] = current;
                writeOffset = checked((long)current.Header.WriteOffset);
            }

            current.Accessor.WriteArray(writeOffset, payload.ToArray(), 0, payload.Length);
            current.Header.WriteOffset = checked((ulong)(writeOffset + payload.Length));
            current.Header.RecordCount++;
            WriteHeader(current);

            return new FastStepSegmentAddress(current.SegmentId, (uint)writeOffset);
        }
    }

    internal FastStepSegmentAddress AppendRecord<TRecord>(in TRecord record)
        where TRecord : struct
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            var size = GetRecordSize<TRecord>();
            var current = _writableSegments[_currentSegmentId];
            var writeOffset = checked((long)current.Header.WriteOffset);

            if (writeOffset + size > _segmentSize)
            {
                _currentSegmentId++;
                current = OpenSegment(_currentSegmentId, readOnly: false);
                _writableSegments[_currentSegmentId] = current;
                writeOffset = checked((long)current.Header.WriteOffset);
            }

            var value = record;
            current.Accessor.Write(writeOffset, ref value);

            current.Header.WriteOffset = checked((ulong)(writeOffset + size));
            current.Header.RecordCount++;
            WriteHeader(current);

            return new FastStepSegmentAddress(current.SegmentId, (uint)writeOffset);
        }
    }

    internal bool TryReadRecord<TRecord>(FastStepSegmentAddress address, out TRecord record)
        where TRecord : struct
    {
        ThrowIfDisposed();

        using var segment = OpenSegment(address.SegmentId, readOnly: true);
        var size = GetRecordSize<TRecord>();
        if (checked((ulong)address.Offset + (ulong)size) > segment.Header.WriteOffset)
        {
            record = default;
            return false;
        }

        segment.Accessor.Read(address.Offset, out record);
        return true;
    }

    internal int ReadBytes(FastStepSegmentAddress address, Span<byte> destination)
    {
        ThrowIfDisposed();

        using var segment = OpenSegment(address.SegmentId, readOnly: true);
        var remaining = checked((long)segment.Header.WriteOffset - address.Offset);
        if (remaining <= 0)
        {
            return 0;
        }

        var toRead = (int)Math.Min(destination.Length, remaining);
        var buffer = new byte[toRead];
        segment.Accessor.ReadArray(address.Offset, buffer, 0, toRead);
        buffer.CopyTo(destination);
        return toRead;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var segment in _writableSegments.Values)
        {
            segment.Dispose();
        }

        _writableSegments.Clear();
        _disposed = true;
    }

    private SegmentState OpenSegment(uint segmentId, bool readOnly)
    {
        var path = BuildSegmentPath(segmentId);
        var exists = File.Exists(path);

        if (!exists && readOnly)
        {
            throw new FileNotFoundException("MMF segment was not found.", path);
        }

        var stream = new FileStream(
            path,
            exists ? FileMode.Open : FileMode.CreateNew,
            readOnly ? FileAccess.Read : FileAccess.ReadWrite,
            FileShare.Read);

        if (!exists)
        {
            stream.SetLength(_segmentSize);
        }

        var access = readOnly ? MemoryMappedFileAccess.Read : MemoryMappedFileAccess.ReadWrite;
        var mmf = MemoryMappedFile.CreateFromFile(stream, null, _segmentSize, access, HandleInheritability.None, leaveOpen: false);
        var accessor = mmf.CreateViewAccessor(0, _segmentSize, access);

        FastStepSegmentHeader header;
        if (exists)
        {
            accessor.Read(0, out header);
            ValidateHeader(header, segmentId);
        }
        else
        {
            header = CreateHeader(segmentId);
            accessor.Write(0, ref header);
            accessor.Flush();
        }

        return new SegmentState(segmentId, mmf, accessor, header);
    }

    private string BuildSegmentPath(uint segmentId)
    {
        return Path.Combine(_rootDirectory, $"{_storePrefix}_{segmentId:D6}.seg");
    }

    private FastStepSegmentHeader CreateHeader(uint segmentId)
    {
        return new FastStepSegmentHeader
        {
            Magic = SegmentMagic,
            Version = SegmentVersion,
            StoreKind = (ushort)_storeKind,
            SegmentId = segmentId,
            CreatedUnixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DataStartOffset = FastStepSegmentHeader.Size,
            WriteOffset = FastStepSegmentHeader.Size,
            RecordCount = 0,
            HeaderCrc32 = 0,
            Reserved0 = 0,
            Reserved1 = 0,
            Reserved2 = 0,
        };
    }

    private void ValidateHeader(FastStepSegmentHeader header, uint expectedSegmentId)
    {
        if (header.Magic != SegmentMagic)
        {
            throw new InvalidDataException("Invalid MMF segment magic.");
        }

        if (header.Version != SegmentVersion)
        {
            throw new InvalidDataException($"Unsupported MMF segment version '{header.Version}'.");
        }

        if (header.StoreKind != (ushort)_storeKind)
        {
            throw new InvalidDataException("MMF segment store kind mismatch.");
        }

        if (header.SegmentId != expectedSegmentId)
        {
            throw new InvalidDataException("MMF segment id mismatch.");
        }

        if (header.DataStartOffset < FastStepSegmentHeader.Size || header.WriteOffset < header.DataStartOffset || header.WriteOffset > (ulong)_segmentSize)
        {
            throw new InvalidDataException("MMF segment offsets are invalid.");
        }
    }

    private static void WriteHeader(SegmentState segment)
    {
        segment.Accessor.Write(0, ref segment.Header);
        segment.Accessor.Flush();
    }

    private static int GetRecordSize<TRecord>()
        where TRecord : struct
    {
        return System.Runtime.InteropServices.Marshal.SizeOf<TRecord>();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class SegmentState : IDisposable
    {
        internal SegmentState(uint segmentId, MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, FastStepSegmentHeader header)
        {
            SegmentId = segmentId;
            Mmf = mmf;
            Accessor = accessor;
            Header = header;
        }

        internal uint SegmentId { get; }

        internal MemoryMappedFile Mmf { get; }

        internal MemoryMappedViewAccessor Accessor { get; }

        internal FastStepSegmentHeader Header;

        public void Dispose()
        {
            Accessor.Dispose();
            Mmf.Dispose();
        }
    }
}
