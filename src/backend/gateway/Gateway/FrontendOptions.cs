namespace Sellevate.Gateway;

/// <summary>
/// Browser origins the gateway is allowed to answer, bound from the <c>Frontend</c> config
/// section. <see cref="Url"/> is a <em>comma-separated allow-list</em>, not a single address:
/// deployments list the production origin alongside <c>http://localhost:3000</c> so one image
/// serves both. The property default is the value shipped in <c>appsettings.json</c>, so a
/// deployment that forgets <c>Frontend__Url</c> still allows local development rather than
/// silently allowing nothing.
/// </summary>
internal sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string Url { get; set; } = "http://localhost:3000";
}
