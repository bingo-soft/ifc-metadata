using System;
using System.Runtime.InteropServices;

namespace Bingosoft.Net.IfcMetadata.FastStep.Mmf;

internal sealed class FastStepMmfEntityStore : IDisposable
{
    private readonly FastStepMmfSegmentManager _segments;

    internal FastStepMmfEntityStore(string directoryPath, long segmentSize = 256L * 1024 * 1024)
    {
        _segments = new FastStepMmfSegmentManager(directoryPath, "entity", FastStepStoreKind.Entity, segmentSize);
    }

    internal FastStepSegmentAddress Append(in FastStepEntityRecord record)
    {
        return _segments.AppendRecord(record);
    }

    internal bool TryRead(FastStepSegmentAddress address, out FastStepEntityRecord record)
    {
        return _segments.TryReadRecord(address, out record);
    }

    public void Dispose()
    {
        _segments.Dispose();
    }
}

internal sealed class FastStepMmfRelationStore : IDisposable
{
    private readonly FastStepMmfSegmentManager _segments;

    internal FastStepMmfRelationStore(string directoryPath, long segmentSize = 256L * 1024 * 1024)
    {
        _segments = new FastStepMmfSegmentManager(directoryPath, "relation", FastStepStoreKind.Relation, segmentSize);
    }

    internal FastStepSegmentAddress Append(in FastStepRelationRecord record)
    {
        return _segments.AppendRecord(record);
    }

    internal bool TryRead(FastStepSegmentAddress address, out FastStepRelationRecord record)
    {
        return _segments.TryReadRecord(address, out record);
    }

    public void Dispose()
    {
        _segments.Dispose();
    }
}

internal sealed class FastStepMmfObjectStore : IDisposable
{
    private readonly FastStepMmfSegmentManager _segments;

    internal FastStepMmfObjectStore(string directoryPath, long segmentSize = 256L * 1024 * 1024)
    {
        _segments = new FastStepMmfSegmentManager(directoryPath, "object", FastStepStoreKind.Object, segmentSize);
    }

    internal FastStepSegmentAddress Append(in FastStepObjectRecord record)
    {
        return _segments.AppendRecord(record);
    }

    internal bool TryRead(FastStepSegmentAddress address, out FastStepObjectRecord record)
    {
        return _segments.TryReadRecord(address, out record);
    }

    public void Dispose()
    {
        _segments.Dispose();
    }
}

internal sealed class FastStepMmfStringBlobStore : IDisposable
{
    private readonly FastStepMmfSegmentManager _segments;

    internal FastStepMmfStringBlobStore(string directoryPath, long segmentSize = 256L * 1024 * 1024)
    {
        _segments = new FastStepMmfSegmentManager(directoryPath, "string", FastStepStoreKind.String, segmentSize);
    }

    internal FastStepStringEntryRef Append(ReadOnlySpan<byte> utf8Payload, uint hash32, FastStepStringFlags flags = FastStepStringFlags.Utf8)
    {
        var header = new FastStepStringEntryHeader
        {
            ByteLength = (uint)utf8Payload.Length,
            Hash32 = hash32,
            Flags = (uint)flags,
            Reserved = 0,
        };

        var headerSize = Marshal.SizeOf<FastStepStringEntryHeader>();
        var entry = new byte[headerSize + utf8Payload.Length];
        MemoryMarshal.Write(entry.AsSpan(0, headerSize), in header);
        utf8Payload.CopyTo(entry.AsSpan(headerSize));

        var headerAddress = _segments.Append(entry);
        var payloadAddress = new FastStepSegmentAddress(headerAddress.SegmentId, checked((uint)(headerAddress.Offset + headerSize)));
        return new FastStepStringEntryRef(headerAddress, payloadAddress, utf8Payload.Length);
    }

    internal bool TryReadHeader(FastStepSegmentAddress address, out FastStepStringEntryHeader header)
    {
        return _segments.TryReadRecord(address, out header);
    }

    internal int ReadPayload(FastStepSegmentAddress payloadAddress, Span<byte> destination)
    {
        return _segments.ReadBytes(payloadAddress, destination);
    }

    public void Dispose()
    {
        _segments.Dispose();
    }
}
