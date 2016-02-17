using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using EasyHttp.Http;
using Reseter.Entities;
using Newtonsoft.Json;
using System.Linq;
using Reseter.Utilities;

namespace Reseter.Courts_Reset
{
    public class APIReset
    {
        private SqlConnection m_db;
        private string BulkDataPath;
        private string CourtListenerUrl = "https://www.courtlistener.com";
        private StateDictionary StatesDictionary;

        public APIReset()
        {
            // Initialize single db connection
            m_db = new SqlConnection(ConfigurationManager.ConnectionStrings["CaseLawContext"].ConnectionString);

            // Initialize the bulk data path
            BulkDataPath = ConfigurationManager.AppSettings["BulkDataPath"];

            // Initialize the dictionary
            StatesDictionary = new StateDictionary();

            // Open the db connection
            Console.WriteLine("Opening DB");
            m_db.Open();
        }

        public void Process()
        {
            try
            {
                var CourtDictionary = new Dictionary<string, string>();
                var count = 0;
                DateTime start = DateTime.Now;

                Console.WriteLine("Fetching OpinionDocuments");
                foreach (var Document in GetOpinionDocuments())
                {
                    ApiDocument TempDocument = new ApiDocument();

                    count = count + 1;

                    Console.WriteLine("Checked documents= " + count + " from " + start.ToString("HH:mm:ss") + " to " + DateTime.Now.ToString("HH:mm:ss"));

                    Console.WriteLine("Processing Opinion Document: " + Document.Id.ToString() + ", " + Document.SourceFile);

                    var SourceNumber = Document.SourceFile.TrimEnd('/').Split('/').ToList().Last();

                    // Get the file from the bulk data that matches the Document Id

                    // v2 json format case has document type null, getting docket from docket url
                    TempDocument = GetFromBulk(SourceNumber);

                    if (TempDocument == null)
                    {
                        TempDocument = GetFromAPI(SourceNumber);
                    }
                    else
                    {
                        if (TempDocument.CourtUrl == String.Empty || TempDocument.CourtUrl == null)
                        {

                            TempDocument = GetFromAPI(SourceNumber);
                        }
                    }


                    CheckCourts(TempDocument, Document, SourceNumber);
                }
            }
            catch (Exception)
            {
                System.Environment.Exit(1);
            }

        }

        private ApiDocument GetFromBulk(string SourceNumber)
        {
            ApiDocument TempDocument = new ApiDocument();

            TempDocument = GetDocumentFromBulkData(SourceNumber);

            if (TempDocument != null)
            {
                var TempDocket = string.Empty;

                if (TempDocument.Docket == string.Empty)
                {
                    TempDocket = TempDocument.DocketUrl.TrimEnd('/').Split('/').ToList().Last();
                    TempDocument.Docket = TempDocket;

                }
                if (TempDocument.Citation.Docket != null)
                {
                    TempDocument.Docket = TempDocument.Citation.Docket;
                }

                TempDocument.caseName = TempDocument.Citation.caseName;
            }

            return TempDocument;
        }

        private ApiDocument GetFromAPI(string SourceNumber)
        {
            ApiDocument TempDocument = new ApiDocument();

            // Get the document from Court Listener that matches the Document Source File
            TempDocument = GetDocumentFromAPI(SourceNumber);
            //Value came from a diferent hierarchy so we get it and then stored at the same value than bulk data has
            TempDocument.Citation.caseName = TempDocument.caseName;

            return TempDocument;
        }

        private void CheckCourts(ApiDocument TempDocument, OpinionDocument Document, string SourceNumber)
        {
            var CourtDictionary = new Dictionary<string, string>();

            if (TempDocument != null)
            {

                string CourtId = TempDocument.CourtUrl.Replace("/?format=json", "").TrimEnd('/').Split('/').ToList().Last();

                //Adding sourcefile from bulk
                //if (TempDocument.Citation != null)
                //{
                //    Document.SourceFile = TempDocument.Citation.sourceFile[0];
                //}

                // Verify if the court exists on the cache CourtDictionary
                if (CourtDictionary.ContainsKey(CourtId))
                {
                    var TempCourt = new Court(CourtId, CourtDictionary[CourtId]);

                    if (TempCourt != null)
                    {
                        CheckCourt(TempCourt, TempDocument, Document, SourceNumber, CourtId);
                    }
                    else
                    {
                        Console.WriteLine("The court with the Code URL: " + TempDocument.CourtUrl + " couldn't be found.");
                    }
                }
                else
                {
                    // Get the Court of a Document from Court Listener

                    var TempCourt = GetCourtFromAPI(CourtId);
                    TempCourt.CourtId = CourtId;

                    if (TempCourt != null)
                    {
                        CheckCourt(TempCourt, TempDocument, Document, SourceNumber, CourtId);
                    }
                    else
                    {
                        Console.WriteLine("The court with the Code URL: " + TempDocument.CourtUrl + " couldn't be found.");
                    }
                }
            }
            else
            {
                Console.WriteLine("The document with the Source File: " + Document.SourceFile + " couldn't be found.");
            }
        }

        private void CheckCourt(Court TempCourt, ApiDocument TempDocument, OpinionDocument Document, string SourceNumber, string CourtId)
        {
            Document.SourceFile = "/api/rest/v3/opinions/" + SourceNumber + "/";

            // Verify if the state exists on the state dictionary
            if (StatesDictionary.GetDictionary().ContainsKey(CourtId))
            {

                // Fetch the state from the state dictionary
                string state = StatesDictionary.GetDictionary()[CourtId];

                // Update the court and state of the Opiniond Document
                if (UpdateDocumentValues(Document.Id, Document.SourceFile, TempCourt.Name, TempDocument.Citation.caseName, state, TempDocument.Docket))
                {
                    Log(Document.Id.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the source file from documents in our database that came from CourtListener
        /// </summary>
        private IEnumerable<OpinionDocument> GetOpinionDocuments()
        {
            Console.WriteLine("Getting opinions");

            using (var cmd = m_db.CreateCommand())
            {
                Console.WriteLine(m_db.State);
                cmd.CommandTimeout = 0;
                cmd.CommandText = @"select o.OpinionDocumentId, o.SourceFile
                                    from OpinionDocuments as o
                                    where o.SourceFile like '%api%'
                                    and o.DataFixed = 0";

                using (var reader = cmd.ExecuteReader())
                {
                    Console.WriteLine(reader.HasRows);

                    while (reader.Read())
                    {
                        int Id = Convert.ToInt32(reader["OpinionDocumentId"]);
                        string SourceFile = reader["SourceFile"].ToString();
                        yield return new OpinionDocument(Id, SourceFile);
                    }
                }
            }
        }

        /// <summary>
        /// Gets a json file from the bulk data that matches a given id
        /// </summary>
        private ApiDocument GetDocumentFromBulkData(string FileId)
        {
            string FilePath = BulkDataPath + "\\" + FileId + ".json";
            if (File.Exists(FilePath))
            {
                return JsonConvert.DeserializeObject<ApiDocument>(File.ReadAllText(FilePath));
            }
            else
            {
                Console.WriteLine("Getting from Bulk");
                return null;
            }
        }

        /// <summary>
        /// Retrieves the court data from CourtListener
        /// </summary>
        private Court GetCourtFromAPI(string CourtUrl)
        {
            var http = new HttpClient();

            // Configure the client
            http.Request.Accept = HttpContentTypes.ApplicationJson;
            http.Request.ForceBasicAuth = true;
            http.Request.SetBasicAuthentication("peidelman", "Dyn4m1c5!");
            var fullUrl = string.Format("{0}/{1}/{2}", CourtListenerUrl, "api/rest/v3/courts", CourtUrl + "/");
            //fullUrl = fullUrl.Replace("//", "/");

            // Retrieves the response from CourtListener API
            var response = http.Get(fullUrl);

            // Verify if the response is good
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            // return deserialized court
            return JsonConvert.DeserializeObject<Court>(response.RawText);
        }

        /// <summary>
        /// Retrieves the document data from CourtListner
        /// </summary>
        private ApiDocument GetDocumentFromAPI(string DocumentUrl)
        {
            ApiDocument temp = new ApiDocument();
            var http = new HttpClient();
            var tempApiCluster = new APICluster();
            // Configure the client
            http.Request.Accept = HttpContentTypes.ApplicationJson;
            http.Request.ForceBasicAuth = true;
            http.Request.SetBasicAuthentication("peidelman", "Dyn4m1c5!");
            var query = string.Format("{0}/{1}/{2}", CourtListenerUrl, "api/rest/v3/clusters", DocumentUrl + "/?format=json");
            // Retrieves the response from CourtListener API
            var response = http.Get(query);

            // Verify if the response is good
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            tempApiCluster = JsonConvert.DeserializeObject<APICluster>(response.RawText);

            var responseCourt = http.Get(tempApiCluster.Docket);

            // Verify if second the response is good
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            // var responseCourt = http.Get(tempApiDocument.Docket);
            // return deserialized court
            return JsonConvert.DeserializeObject<ApiDocument>(responseCourt.RawText);
        }

        /// <summary>
        /// Updates the court and state of a given document
        /// </summary>
        private bool UpdateDocumentCourt(int Id, string SourceFile, string Court)
        {
            var result = false;

            using (var cmd = m_db.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = @"update OpinionDocuments
                                    set Court = @court,
                                    DataFixed = 'True',
                                    SourceFile = @sourcefile
                                    where OpinionDocumentId = @Id";

                cmd.Parameters.Add("court", SqlDbType.VarChar).Value = Court;
                cmd.Parameters.Add("sourcefile", SqlDbType.VarChar).Value = SourceFile;
                cmd.Parameters.Add("Id", SqlDbType.Int).Value = Id;

                if (cmd.ExecuteNonQuery() > 0)
                {
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Updates the court and state of a given document
        /// </summary>
        private bool UpdateDocumentValues(int Id, string SourceFile, string Court, string Title, string State, string DocketNumber)
        {
            var result = false;

            using (var cmd = m_db.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = @"update OpinionDocuments
                                    set Court = @court,
	                                State = @state,
                                    DocketNumber= @docketnumber,
                                    DisplayTitle= @displayTitle,
                                    DataFixed = 'True',
                                    SourceFile = @sourcefile
                                    where OpinionDocumentId = @Id";

                cmd.Parameters.Add("court", SqlDbType.VarChar).Value = Court;
                cmd.Parameters.Add("state", SqlDbType.VarChar).Value = State;
                cmd.Parameters.Add("sourcefile", SqlDbType.VarChar).Value = SourceFile;
                cmd.Parameters.Add("displayTitle", SqlDbType.VarChar).Value = Title;
                cmd.Parameters.Add("docketnumber", SqlDbType.VarChar).Value = DocketNumber;
                cmd.Parameters.Add("Id", SqlDbType.Int).Value = Id;

                if (cmd.ExecuteNonQuery() > 0)
                {
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Log text into a text file
        /// </summary>
        public void Log(string text)
        {
            StreamWriter log;

            if (!File.Exists("UpdatedOpinionDocumentsIds.txt"))
            {
                log = new StreamWriter("UpdatedOpinionDocumentsIds.txt");
            }
            else
            {
                log = File.AppendText("UpdatedOpinionDocumentsIds.txt");
            }

            log.WriteLine(text);

            log.Close();
        }

    }
}
