using Newtonsoft.Json;

namespace Reseter.Entities
{
    public class APICluster
    {
        #region Properties

        [JsonProperty("docket")]
        public string Docket { get; set; }

        #endregion
    }
}
