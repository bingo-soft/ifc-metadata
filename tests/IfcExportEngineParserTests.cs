using Bingosoft.Net.IfcMetadata;

using Xunit;

namespace IfcMetadata.Tests;

public sealed class IfcExportEngineParserTests
{
    [Theory]
    [InlineData("xbim", "Xbim")]
    [InlineData("XBIM", "Xbim")]
    [InlineData("fast-step", "FastStep")]
    [InlineData("faststep", "FastStep")]
    public void TryParse_RecognizesSupportedEngineNames(string value, string expectedEngineName)
    {
        var parsed = IfcExportEngineParser.TryParse(value, out var engine);

        Assert.True(parsed);
        Assert.Equal(expectedEngineName, engine.ToString());
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForUnknownEngineName()
    {
        var parsed = IfcExportEngineParser.TryParse("other", out var engine);

        Assert.False(parsed);
        Assert.Equal(IfcExportEngine.Xbim, engine);
    }
}
