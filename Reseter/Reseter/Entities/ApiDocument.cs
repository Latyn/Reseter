using Newtonsoft.Json;
using System.Collections.Generic;

namespace Reseter.RequestManager
{
    public class ApiDocument
    {
        #region Properties

        [JsonProperty("court")]
        public string CourtUrl { get; set; }


        [JsonProperty("citation")]
        public ApiDocumentCitation Citation { get; set; } = new ApiDocumentCitation("","");

        [JsonProperty("docket_number")]
        public string Docket { get; set; } = string.Empty;

        //API parameter
        [JsonProperty("case_name")]
        public string caseName { get; set; } = string.Empty;

        [JsonProperty("docket")]
        public string DocketUrl { get; set; } = string.Empty;

        
        [JsonProperty("case_name_full")]
        public string caseNameFull { get; set; } = string.Empty;

        #endregion
    }

    public class ApiDocumentCitation
    {

        #region Properties

        [JsonProperty("document_uris")]
        public List<string> sourceFile { get; set; }

        [JsonProperty("case_name")]
        public string caseName { get; set; }

        [JsonProperty("docket_number")]
        public string Docket { get; set; }

        public ApiDocumentCitation(string caseName, string Docket)
        {
            this.caseName = caseName;
            this.Docket = Docket;
        }

        #endregion

    }

}
