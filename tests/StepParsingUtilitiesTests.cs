using Bingosoft.Net.IfcMetadata.FastStep;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class StepParsingUtilitiesTests
{
    [Fact]
    public void ParseStepString_PlainQuoted_ReturnsInnerValue()
    {
        var value = StepParsingUtilities.ParseStepString("'Project A'");

        Assert.Equal("Project A", value);
    }

    [Fact]
    public void ParseStepString_DecodesEscapedSingleQuote()
    {
        var value = StepParsingUtilities.ParseStepString("'Project ''A'''");

        Assert.Equal("Project 'A'", value);
    }

    [Fact]
    public void ParseStepString_DecodesUtf16EscapeBlock()
    {
        var value = StepParsingUtilities.ParseStepString("'\\X2\\00410042\\X0\\'");

        Assert.Equal("AB", value);
    }

    [Fact]
    public void ParseStepString_DecodesSingleByteEscape()
    {
        var value = StepParsingUtilities.ParseStepString("'A\\X\\42\\C'");

        Assert.Equal("ABC", value);
    }

    [Fact]
    public void ParseStepString_InvalidUtf16Escape_KeepsHexPayloadWithoutMarkers()
    {
        var value = StepParsingUtilities.ParseStepString("'\\X2\\00ZZ\\X0\\'");

        Assert.Equal("00ZZ", value);
    }

    [Fact]
    public void ParseStepReferenceList_ParsesTopLevelReferencesOnce()
    {
        var values = StepParsingUtilities.ParseStepReferenceList("(#1,#2,#3)");

        Assert.Equal(new[] { 1, 2, 3 }, values);
    }
}
