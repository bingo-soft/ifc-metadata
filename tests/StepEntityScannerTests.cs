using System.IO;

using Bingosoft.Net.IfcMetadata.FastStep;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class StepEntityScannerTests
{
    private static int[] GetChildren(FastStepIndexes indexes, FastStepAdjacency adjacency, int parentEntityId)
    {
        var parentSlot = indexes.GetSlotOrMissing(parentEntityId);
        if (parentSlot < 0)
        {
            return [];
        }

        var start = adjacency.Offsets[parentSlot];
        var end = adjacency.Offsets[parentSlot + 1];
        var children = new int[end - start];

        for (var i = start; i < end; i++)
        {
            var childSlot = adjacency.Edges[i];
            children[i - start] = indexes.GetEntityIdBySlot(childSlot);
        }

        return children;
    }

    [Fact]
    public void Scan_IndexesKnownEntities_AndRelations()
    {
        const string step = """
        ISO-10303-21;
        DATA;
        #10=IFCPROJECT('project-guid',$, 'Project Name',$,$,$,$,$,$);
        #11=IFCSITE('site-guid',$,'Site',$,$,$,$,$,$,$,$,$,$,$);
        #12=IFCBUILDING('building-guid',$,'Building',$,$,$,$,$,$,$,$,$);
        #13=IFCBUILDINGSTOREY('storey-guid',$,'Storey',$,$,$,$,$,$,$);
        #20=IFCPROPERTYSET('pset-guid', $, 'Pset', $, ());
        #200=IFCMATERIAL('Concrete',$,$);
        #300=IFCWALLTYPE('wall-type-guid',$,'WallType',$,$,$,$,$,$,.NOTDEFINED.);
        #30=IFCRELAGGREGATES('rel-guid',$,$,$,#10,(#11,#12));
        #40=IFCRELCONTAINEDINSPATIALSTRUCTURE('rel2-guid',$,$,$,(#12,#13),#11);
        #50=IFCRELDEFINESBYPROPERTIES('rel3-guid',$,$,$,(#12),#20);
        #60=IFCRELASSOCIATESMATERIAL('rel4-guid',$,$,$,(#12),#200);
        #70=IFCRELDEFINESBYTYPE('rel5-guid',$,$,$,(#12),#300);
        ENDSEC;
        END-ISO-10303-21;
        """;

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        Assert.Equal(12, indexes.EntityCount);

        Assert.True(indexes.Project.HasValue);
        Assert.Equal("project-guid", indexes.Project.Value.GlobalId);
        Assert.Equal("Project Name", indexes.Project.Value.Name);

        Assert.Equal("pset-guid", indexes.PropertySetGlobalIds[20]);

        Assert.Equal(new[] { 11, 12 }, GetChildren(indexes, indexes.DecompositionAdjacency, 10));
        Assert.Equal(new[] { 12, 13 }, GetChildren(indexes, indexes.ContainmentAdjacency, 11));

        Assert.Equal(new[] { 20 }, GetChildren(indexes, indexes.DefinesByPropertiesAdjacency, 12));
        Assert.Equal(new[] { 200 }, GetChildren(indexes, indexes.AssociatesMaterialAdjacency, 12));
        Assert.Equal(new[] { 300 }, GetChildren(indexes, indexes.DefinesByTypeAdjacency, 12));

        Assert.Equal(indexes.EntityCount + 1, indexes.DecompositionAdjacency.Offsets.Length);
    }

    [Fact]
    public void Scan_IndexesEntityOffsets_AndArgumentRanges()
    {
        const string step = "prefix #10=IFCPROJECT('guid',$,'Project Name',$,$,$,$,$,$); suffix";

        using var reader = new StringReader(step);
        _ = StepEntityScanner.Scan(reader, new FastStepScanOptions(CaptureDiagnostics: true), out var diagnostics);

        Assert.NotNull(diagnostics);

        var range = diagnostics.EntityRanges[10];
        var rawArguments = diagnostics.EntityRawArguments[10];
        var statementStart = step.IndexOf("#10=IFCPROJECT", System.StringComparison.Ordinal);
        var statementEnd = step.IndexOf(';', statementStart) + 1;
        var argsStart = step.IndexOf('(', statementStart) + 1;
        var argsEnd = argsStart + rawArguments.Length;

        Assert.Equal(statementStart, range.StatementStartOffset);
        Assert.Equal(statementEnd, range.StatementEndOffset);
        Assert.Equal(argsStart, range.ArgumentsStartOffset);
        Assert.Equal(argsEnd, range.ArgumentsEndOffset);

        var argsSlice = step[range.ArgumentsStartOffset..range.ArgumentsEndOffset];
        Assert.Equal(rawArguments, argsSlice);
    }

    [Fact]
    public void Scan_PoolsRepeatedStrings()
    {
        const string step = """
        #10=IFCPROJECT('project-guid',$,'Shared Name',$,$,$,$,$,$);
        #11=IFCSITE('site-guid',$,'Shared Name',$,$,$,$,$,$,$,$,$,$,$);
        #12=IFCWALL('wall-1',$,'Wall A',$,$,$,$,$);
        #13=IFCWALL('wall-2',$,'Wall B',$,$,$,$,$);
        """;

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        Assert.True(object.ReferenceEquals(indexes.GetName(10), indexes.GetName(11)));
        Assert.True(object.ReferenceEquals(indexes.GetNormalizedTypeName(12), indexes.GetNormalizedTypeName(13)));
    }

    [Fact]
    public void ScanWithHeader_ReadsHeaderAndEntities_FromSingleReader()
    {
        const string step = """
        ISO-10303-21;
        HEADER;
        FILE_NAME('fixture.ifc','2024-01-01T00:00:00',('author'),('org'),'app','system','auth');
        FILE_SCHEMA(('IFC4'));
        ENDSEC;
        DATA;
        #10=IFCPROJECT('project-guid',$,'Project Name',$,$,$,$,$,$);
        ENDSEC;
        END-ISO-10303-21;
        """;

        using var reader = new StringReader(step);
        var scanResult = StepEntityScanner.ScanWithHeader(reader);

        Assert.Equal("IFC4", scanResult.Header.Schema);
        Assert.Equal("2024-01-01T00:00:00", scanResult.Header.CreatedAt);
        Assert.Equal("author", scanResult.Header.Author);
        Assert.True(scanResult.Indexes.Project.HasValue);
        Assert.Equal("project-guid", scanResult.Indexes.Project.Value.GlobalId);
    }

    [Fact]
    public void Scan_HandlesEscapedQuotes_InStepStrings()
    {
        const string step = "#10=IFCPROJECT('guid',$,'Project ''A''',$,$,$,$,$,$);";

        using var reader = new StringReader(step);
        var indexes = StepEntityScanner.Scan(reader);

        Assert.True(indexes.Project.HasValue);
        Assert.Equal("Project 'A'", indexes.Project.Value.Name);
    }
}


