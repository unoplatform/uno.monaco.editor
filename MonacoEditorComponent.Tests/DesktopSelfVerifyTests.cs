using System.Text.Json;

using Xunit;

namespace MonacoEditorComponent.Tests;

/// <summary>
/// Runtime coverage for the desktop target on native web views that cannot be driven over CDP:
/// WebKitGTK (Linux) and WKWebView (macOS). Assertions read the markers the app's own
/// self-verification scenario writes to stdout -- see <see cref="DesktopSelfVerifyFixture"/>.
///
/// <para><b>Tagged <c>Category=DesktopSelfVerify</c></b> so CI can target it: it is the whole
/// payload of the Linux desktop job, an extra step on macOS, and excluded on Windows where the
/// far more detailed <see cref="DesktopIntegrationTests"/> CDP suite already runs.</para>
///
/// <para><b>Requires a display.</b> On Linux CI this runs under <c>xvfb-run</c>; there is no
/// headless mode for the native web view.</para>
/// </summary>
[Trait("Category", "DesktopSelfVerify")]
[Collection("DesktopSelfVerify")]
public sealed class DesktopSelfVerifyTests
{
    private readonly DesktopSelfVerifyFixture _fixture;

    public DesktopSelfVerifyTests(DesktopSelfVerifyFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The app-side verdict. It already requires every probe to be healthy, so a failure here
    /// means something is broken; the probe tests below say what.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopSelfVerify")]
    public void SelfVerify_ReportsPass()
    {
        Assert.Equal("PASS", _fixture.Result);
    }

    /// <summary>
    /// Monaco actually rendered: an editor and model exist, the DOM node is connected, and the
    /// editor has real pixel dimensions. Non-zero size is the assertion that separates "the app
    /// launched" from "the editor is usable" -- a mis-sized native web view still reports an
    /// editor instance while showing nothing.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopSelfVerify")]
    public void SelfVerify_RuntimeProbesHealthy()
    {
        // One probe per lifecycle stage: initial tab, second tab, back to the first.
        Assert.Equal(3, _fixture.RuntimeProbes.Count);

        foreach (var payload in _fixture.RuntimeProbes)
        {
            var probe = Parse(payload);
            var stage = GetString(probe, "stage");

            Assert.True(probe.GetProperty("hasEditor").GetBoolean(), $"No editor at stage '{stage}': {payload}");
            Assert.True(probe.GetProperty("hasModel").GetBoolean(), $"No model at stage '{stage}': {payload}");
            Assert.True(probe.GetProperty("isConnected").GetBoolean(), $"DOM node not connected at stage '{stage}': {payload}");
            Assert.True(probe.GetProperty("width").GetDouble() > 0, $"Editor width is not positive at stage '{stage}': {payload}");
            Assert.True(probe.GetProperty("height").GetDouble() > 0, $"Editor height is not positive at stage '{stage}': {payload}");
        }
    }

    /// <summary>
    /// The bridge carries real feature traffic: <c>AddActionAsync</c> registered a custom action
    /// in Monaco, and a hover request round-tripped JS to C# and back with content.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopSelfVerify")]
    public void SelfVerify_FeatureProbesHealthy()
    {
        Assert.Equal(3, _fixture.FeatureProbes.Count);

        foreach (var payload in _fixture.FeatureProbes)
        {
            var probe = Parse(payload);
            var stage = GetString(probe, "stage");

            Assert.True(
                probe.GetProperty("hasTestAction").GetBoolean(),
                $"Custom action was not registered at stage '{stage}': {payload}");

            var hover = GetString(probe, "hoverProbeResult");
            Assert.False(string.IsNullOrWhiteSpace(hover), $"Hover provider returned nothing at stage '{stage}': {payload}");
            Assert.NotEqual("__null__", hover);
            Assert.DoesNotContain("__error__:", hover, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The JSON-RPC handshake completed in both directions. This is the transport-level
    /// counterpart to the probes: it fails distinctly when the bridge never came up, rather
    /// than presenting as a generic editor timeout.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopSelfVerify")]
    public void SelfVerify_BridgeHandshakeLogged()
    {
        Assert.Contains(_fixture.LogLines, l => l.Contains("bridge/ready received", StringComparison.Ordinal));
        Assert.Contains(_fixture.LogLines, l => l.Contains("editor/ready received", StringComparison.Ordinal));
    }

    /// <summary>
    /// The Linux WebKitGTK pre-flight probe did not reject a machine that does have the runtime.
    /// A false positive there aborts before the web view is ever created, which would otherwise
    /// surface only as an unexplained readiness timeout.
    /// </summary>
    [Fact]
    [Trait("Category", "DesktopSelfVerify")]
    public void SelfVerify_NoWebKitGtkProbeFailure()
    {
        Assert.DoesNotContain(
            _fixture.LogLines,
            l => l.Contains("WebKitGTK runtime library not found", StringComparison.Ordinal));
    }

    private static JsonElement Parse(string payload)
    {
        Assert.False(string.IsNullOrWhiteSpace(payload), "Probe payload was empty.");
        using var document = JsonDocument.Parse(payload);
        // Clone so the element outlives the JsonDocument.
        return document.RootElement.Clone();
    }

    private static string GetString(JsonElement probe, string propertyName) =>
        probe.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

/// <summary>
/// xUnit collection definition for desktop self-verification, sharing a single
/// <see cref="DesktopSelfVerifyFixture"/> -- and therefore a single app launch -- across
/// all tests in the collection.
/// </summary>
[CollectionDefinition("DesktopSelfVerify")]
public sealed class DesktopSelfVerifyCollection : ICollectionFixture<DesktopSelfVerifyFixture>
{
}
