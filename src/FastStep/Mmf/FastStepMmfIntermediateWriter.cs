using System;
using System.Text;

namespace Bingosoft.Net.IfcMetadata.FastStep.Mmf;

internal sealed class FastStepMmfIntermediateWriter : IDisposable
{
    private readonly FastStepMmfEntityStore _entityStore;
    private readonly FastStepMmfRelationStore _relationStore;
    private readonly FastStepMmfStringBlobStore _argumentStore;

    internal FastStepMmfIntermediateWriter(string directoryPath, long segmentSize = 256L * 1024 * 1024)
    {
        _entityStore = new FastStepMmfEntityStore(directoryPath, segmentSize);
        _relationStore = new FastStepMmfRelationStore(directoryPath, segmentSize);
        _argumentStore = new FastStepMmfStringBlobStore(directoryPath, segmentSize);
    }

    internal void WriteEntity(int entityId, int typeToken, string rawArguments, FastStepEntityFlags flags)
    {
        rawArguments ??= string.Empty;

        var utf8 = Encoding.UTF8.GetBytes(rawArguments);
        var stringEntry = _argumentStore.Append(utf8, ComputeFnv1a(utf8), FastStepStringFlags.Utf8);
        var argOffset = FastStepMmfAddressCodec.Pack(stringEntry.PayloadAddress);

        var record = new FastStepEntityRecord
        {
            EntityId = entityId,
            TypeToken = typeToken,
            ArgOffset = argOffset,
            ArgLength = stringEntry.ByteLength,
            FirstRelIndex = -1,
            RelCount = 0,
            Flags = (uint)flags,
            Reserved = 0,
        };

        _entityStore.Append(record);
    }

    internal void WriteRelation(int parentEntityId, int childEntityId, FastStepRelationKind relationKind, FastStepRelationFlags relationFlags = FastStepRelationFlags.None)
    {
        var record = new FastStepRelationRecord
        {
            ParentEntityId = parentEntityId,
            ChildEntityId = childEntityId,
            RelationKind = (ushort)relationKind,
            Flags = (ushort)relationFlags,
            NextRelIndex = -1,
        };

        _relationStore.Append(record);
    }

    public void Dispose()
    {
        _entityStore.Dispose();
        _relationStore.Dispose();
        _argumentStore.Dispose();
    }

    private static uint ComputeFnv1a(ReadOnlySpan<byte> data)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        for (var i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= prime;
        }

        return hash;
    }
}
