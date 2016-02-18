using Newtonsoft.Json;

namespace Reseter.RequestManager
{
    public class Court
    {
        public Court(string _courtid , string _name) {
            CourtId = _courtid;
            Name = _name;
        }

        #region Properties

        [JsonProperty("full_name")]
        public string Name { get; set; }

        [JsonProperty("id")]
        public string CourtId { get; set; }

        #endregion

        #region Methods

        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, CourtId);
        }

        #endregion
    }
}
