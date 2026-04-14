using System;
using System.Runtime.InteropServices;

namespace Bingosoft.Net.IfcMetadata.FastStep.Mmf;

internal enum FastStepStoreKind : ushort
{
    Entity = 1,
    String = 2,
    Relation = 3,
    Object = 4,
    Index = 5,
}

[Flags]
internal enum FastStepEntityFlags : uint
{
    None = 0,
    ParsedOk = 1u << 0,
    HasStringArgs = 1u << 1,
    HasRelations = 1u << 2,
    Emitted = 1u << 3,
    Invalid = 1u << 4,
}

[Flags]
internal enum FastStepRelationFlags : ushort
{
    None = 0,
    Optional = 1 << 0,
    Inverse = 1 << 1,
    Aggregated = 1 << 2,
}

internal enum FastStepRelationKind : ushort
{
    Unknown = 0,
    Contains = 1,
    References = 2,
    TypeOf = 3,
    PropertySet = 4,
    Material = 5,
    SpatialParent = 6,
    ArgLink = 100,
}

[Flags]
internal enum FastStepObjectFlags : uint
{
    None = 0,
    ReadyToEmit = 1u << 0,
    HasPayload = 1u << 1,
    Ordered = 1u << 2,
    Emitted = 1u << 3,
    Invalid = 1u << 4,
}

[Flags]
internal enum FastStepStringFlags : uint
{
    None = 0,
    Utf8 = 1u << 0,
    Escaped = 1u << 1,
    Interned = 1u << 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FastStepSegmentHeader
{
    internal const int Size = 64;

    internal uint Magic;
    internal ushort Version;
    internal ushort StoreKind;
    internal uint SegmentId;
    internal ulong CreatedUnixMs;
    internal ulong DataStartOffset;
    internal ulong WriteOffset;
    internal ulong RecordCount;
    internal uint HeaderCrc32;
    internal uint Reserved0;
    internal ulong Reserved1;
    internal uint Reserved2;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FastStepEntityRecord
{
    internal int EntityId;
    internal int TypeToken;
    internal long ArgOffset;
    internal int ArgLength;
    internal int FirstRelIndex;
    internal int RelCount;
    internal uint Flags;
    internal uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FastStepRelationRecord
{
    internal int ParentEntityId;
    internal int ChildEntityId;
    internal ushort RelationKind;
    internal ushort Flags;
    internal int NextRelIndex;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FastStepStringEntryHeader
{
    internal uint ByteLength;
    internal uint Hash32;
    internal uint Flags;
    internal uint Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct FastStepObjectRecord
{
    internal int EntityId;
    internal int TypeToken;
    internal uint PayloadSegmentId;
    internal uint PayloadOffset;
    internal int PayloadLength;
    internal int OutputOrder;
    internal uint Flags;
    internal uint Reserved;
}

internal readonly record struct FastStepSegmentAddress(uint SegmentId, uint Offset);

internal readonly record struct FastStepStringEntryRef(
    FastStepSegmentAddress HeaderAddress,
    FastStepSegmentAddress PayloadAddress,
    int ByteLength);

internal static class FastStepMmfAddressCodec
{
    internal static long Pack(FastStepSegmentAddress address)
    {
        return ((long)address.SegmentId << 32) | address.Offset;
    }

    internal static FastStepSegmentAddress Unpack(long packed)
    {
        var segmentId = unchecked((uint)(packed >> 32));
        var offset = unchecked((uint)packed);
        return new FastStepSegmentAddress(segmentId, offset);
    }
}
