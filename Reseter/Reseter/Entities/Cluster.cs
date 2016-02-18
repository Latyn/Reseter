using Newtonsoft.Json;

namespace Reseter.RequestManager
{
    public class APICluster
    {
        #region Properties

        [JsonProperty("docket")]
        public string Docket { get; set; }

        #endregion
    }
}
