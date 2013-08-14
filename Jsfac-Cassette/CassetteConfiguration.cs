using Cassette;
using Cassette.Scripts;

namespace Jsfac_Cassette
{
    /// <summary>
    /// Configures the Cassette asset bundles for the web application.
    /// </summary>
    public class CassetteBundleConfiguration : IConfiguration<BundleCollection>
    {
        public void Configure(BundleCollection bundles)
        {
            bundles.AddPerIndividualFile<ScriptBundle>("js/test");
            bundles.AddPerIndividualFile<ScriptBundle>("js/util");
            bundles.Add<ScriptBundle>("js/jsfac.js");
        }
    }
}