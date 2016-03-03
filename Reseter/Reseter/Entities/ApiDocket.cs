using Newtonsoft.Json;

namespace Reseter.RequestManager
{
    public class ApiDocket
    {

        #region Properties

        [JsonProperty("docket_number")]
        public string Docket { get; set; } = string.Empty;

            #endregion

    }
}
