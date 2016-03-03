using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
//using EasyHttp.Http;
using Reseter.RequestManager;
using Newtonsoft.Json;
using System.Linq;
using Reseter.Utilities;
using System.Net.Http;

namespace Reseter.Courts_Reset
{
    public class APIReset
    {
        private SqlConnection m_db;
        private string BulkDataPath;
        private string CourtListenerUrl = "https://www.courtlistener.com";
        private StateDictionary StatesDictionary;
        public Dictionary<string, string> CourtDictionary = new Dictionary<string, string>();
        public int MenuOption;

        public APIReset()
        {
            // Initialize single db connection
            m_db = new SqlConnection(ConfigurationManager.ConnectionStrings["CaseLawContext"].ConnectionString);

            // Initialize the bulk data path
            BulkDataPath = ConfigurationManager.AppSettings["BulkDataPath"];
            MenuOption = int.Parse(ConfigurationManager.AppSettings["MenuOption"]);

            // Initialize the dictionary
            StatesDictionary = new StateDictionary();

            // Open the db connection
            Console.WriteLine("Opening DB");
            m_db.Open();
        }
        public void Menu()
        {
            switch (MenuOption)
            {
                case 1: Process();
                    break;
                case 2: CheckDockets();
                    break;
            }
        }

        private void CheckDockets()
        {
            DateTime start = DateTime.Now;
            var count = 0;

            Console.WriteLine("Fetching OpinionDocuments");
            foreach (var Document in GetOpinionDocuments())
            {
                ApiDocument TempDocument = new ApiDocument();
                count = count + 1;

                Console.WriteLine("From " + start.ToString("HH:mm:ss") + " to " + DateTime.Now.ToString("HH:mm:ss"));
               
                //Console.WriteLine("Last : " + Document.Docket);

                //Get soruce url to extract the number
                var SourceNumber = Document.SourceFile.TrimEnd('/').Split('/').ToList().Last();
                Console.WriteLine("Opinion : " + SourceNumber);
                TempDocument = GetFromBulk(SourceNumber);

                if (TempDocument != null)
                {
                    Console.WriteLine("Checking from Bulk " + count);
                    if (TempDocument.Citation.Docket != "" & TempDocument.Citation.Docket != null)
                    {
                        //Console.WriteLine("TempDocument.Citation.Docket" + TempDocument.Citation.Docket);
                        if (!UpdateDocket(TempDocument.Citation.Docket, Document.SourceFile)) {
                            Console.WriteLine("Failed");
                            Log(Document.Id.ToString());
                        }

                        continue;

                        //else
                        //{
                        //    Console.WriteLine("New : " + TempDocument.Citation.Docket);
                        //}
                    }
                    else
                    {
                        if (TempDocument.Docket != "" & TempDocument.Docket != null)
                        {
                            //Console.WriteLine("TempDocument.Docket" + TempDocument.Citation.Docket);
                            //Console.WriteLine(TempDocument.Docket);

                            if (!UpdateDocket(TempDocument.Docket, Document.SourceFile)) {
                                Console.WriteLine("Failed");
                                Log(Document.Id.ToString());
                            }

                            continue;

                            //else
                            //{
                            //    Console.WriteLine("New : " + TempDocument.Docket);
                            //}
                        }
                        else
                        {
                            ApiDocket newDocket = new ApiDocket();

                            Console.WriteLine("Checking from API");
                            newDocket = checkDocketfromAPI(SourceNumber);

                            if (newDocket.Docket != null)
                            {
                                if (!UpdateDocket(newDocket.Docket, Document.SourceFile))
                                {

                                    Log(Document.Id.ToString());
                                }

                                continue;

                                //else
                                //{
                                //    Console.WriteLine("New : " + newDocket.Docket);
                                //}
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No Bulk Data " + count);
                    continue;
                }
            }
        }

        private ApiDocket checkDocketfromAPI(string docketUrl)
        {
            Console.WriteLine("API Document");
            ApiDocument temp = new ApiDocument();
            var http = new HttpClient();
            var tempApiCluster = new APICluster();

            // Configure the client
            Request request = new Request();
            Credentials credentials = new Credentials();
            credentials.UserName = "peidelman";
            credentials.Password = "Dyn4m1c5!";
            request.Credentials = credentials;


            var query = string.Format("{0}/{1}/{2}", CourtListenerUrl, "api/rest/v3/clusters/", docketUrl + "/?format=json");
            // Retrieves the response from CourtListener API
            request.URL = query;

            var data = request.Execute();

            // Verify if the response is good
            if (request.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            tempApiCluster = JsonConvert.DeserializeObject<APICluster>(data);

            request.URL = tempApiCluster.Docket;

            // Verify if second the response is good
            if (request.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            data = request.Execute();

            // var responseCourt = http.Get(tempApiDocument.Docket);
            // return deserialized court
            return JsonConvert.DeserializeObject<ApiDocket>(data);
        }

        private bool UpdateDocket(string docket, string sourcefile)
        {
            var result = false;

            using (var cmd = m_db.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = @"update OpinionDocuments
                                    set DocketNumber = @value,
                                    DataFixed = 'True'
                                    where SourceFile = @sourcefile";
                if (docket == null)
                {
                    cmd.Parameters.Add("value", SqlDbType.VarChar).Value = DBNull.Value;
                }
                else
                {
                    cmd.Parameters.Add("value", SqlDbType.VarChar).Value = docket;
                }

                cmd.Parameters.Add("sourcefile", SqlDbType.VarChar).Value = sourcefile;

                if (cmd.ExecuteNonQuery() > 0)
                {
                    result = true;
                }
            }

            return result;
        }

        public void Process()
        {
            try
            {
                var CourtDictionary = new Dictionary<string, string>();
                var count = 0;
                var ApiCount = 0;
                var BulkCount = 0;
                DateTime start = DateTime.Now;

                Console.WriteLine("Fetching OpinionDocuments");
                foreach (var Document in GetOpinionDocuments())
                {
                    ApiDocument TempDocument = new ApiDocument();

                    count = count + 1;

                    Console.WriteLine("From " + start.ToString("HH:mm:ss") + " to " + DateTime.Now.ToString("HH:mm:ss"));

                    Console.WriteLine("Processing Opinion Document: " + Document.Id.ToString() + ", " + Document.SourceFile);

                    var SourceNumber = Document.SourceFile.TrimEnd('/').Split('/').ToList().Last();

                    // Get the file from the bulk data that matches the Document Id

                    // v2 json format case has document type null, getting docket from docket url
                    TempDocument = GetFromBulk(SourceNumber);

                    if (TempDocument == null)
                    {
                        ApiCount = ApiCount + 1;
                        TempDocument = GetFromAPI(SourceNumber);
                        if (TempDocument.caseName != "" & TempDocument.caseName != null )
                        {
                            Console.WriteLine(TempDocument.caseName);
                            TempDocument.Citation.caseName = TempDocument.caseName;
                        }
                        else
                        {
                            if (TempDocument.caseNameFull != "" & TempDocument.caseNameFull != null)
                            {
                                Console.WriteLine(TempDocument.caseNameFull);
                                TempDocument.Citation.caseName = TempDocument.caseNameFull;
                            }
                        }

                        // if it is not gotten from bulk or API set Datafixed as null  and go to next item
                        if (TempDocument == null)
                        {
                            if (setDataFixed(Document.SourceFile))
                            {
                                Console.WriteLine("No Data");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        BulkCount = BulkCount + 1;
                    }

                    CheckCourts(TempDocument, Document, SourceNumber);
                    Console.WriteLine("Total= " + count + " Checked from API " + ApiCount + " Bulk " + BulkCount);
                }
            }
            catch (Exception)
            {

                System.Environment.Exit(1);
            }
                

        }
        private bool setDataFixed(string SourceFile)
        {
            var result = false;

            using (var cmd = m_db.CreateCommand())
            {
                cmd.CommandTimeout = 0;
                cmd.CommandText = @"update OpinionDocuments
                                    set DataFixed = @value
                                    where SourceFile = @sourcefile";

                cmd.Parameters.Add("value", SqlDbType.VarChar).Value = DBNull.Value;
                cmd.Parameters.Add("sourcefile", SqlDbType.VarChar).Value = SourceFile;

                if (cmd.ExecuteNonQuery() > 0)
                {
                    result = true;
                }
            }

            return result;
        }
        private ApiDocument GetFromBulk(string SourceNumber)
        {
            ApiDocument TempDocument = new ApiDocument();

            TempDocument = GetDocumentFromBulkData(SourceNumber);

            if (TempDocument != null)
            {
                //var TempDocket = string.Empty;

                //if (TempDocument.Docket == string.Empty)
                //{
                //    TempDocket = TempDocument.DocketUrl.TrimEnd('/').Split('/').ToList().Last();
                //    TempDocument.Docket = TempDocket;

                //}
                if (TempDocument.Citation.Docket != null)
                {
                    TempDocument.Docket = TempDocument.Citation.Docket;
                }

            }

            return TempDocument;
        }

        private ApiDocument GetFromAPI(string SourceNumber)
        {
            ApiDocument TempDocument = new ApiDocument();

            // Get the document from Court Listener that matches the Document Source File
            TempDocument = GetDocumentFromAPI(SourceNumber);

            if (TempDocument != null)
            {
                //Value came from a diferent hierarchy so we get it and then stored at the same value than bulk data has
                TempDocument.Citation.caseName = TempDocument.caseName;
                Console.WriteLine(TempDocument.Citation.caseName);
            }

            return TempDocument;
        }

        private void CheckCourts(ApiDocument TempDocument, OpinionDocument Document, string SourceNumber)
        {
            Court TempCourt= new Court("","");

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
                    Console.WriteLine("Contains Court "+ CourtId);

                    TempCourt.CourtId = CourtId;
                    TempCourt.Name = (CourtDictionary[CourtId]);

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

                    TempCourt = GetCourtFromAPI(CourtId);
                    TempCourt.CourtId = CourtId;

                    

                    if (TempCourt != null)
                    {
                        CourtDictionary.Add(TempCourt.CourtId, TempCourt.Name);
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
            Console.WriteLine(TempDocument.Citation.caseName);
            // Verify if the state exists on the state dictionary
            if (StatesDictionary.GetDictionary().ContainsKey(CourtId))
            {

                // Fetch the state from the state dictionary
                string state = StatesDictionary.GetDictionary()[CourtId];

                Console.WriteLine(TempDocument.Citation.caseName);
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
                cmd.CommandText = @"select o.OpinionDocumentId, o.SourceFile, o.DocketNumber
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
                        string Docket = reader["DocketNumber"].ToString();
                        yield return new OpinionDocument(Id, SourceFile, Docket);
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
                return null;
            }
        }

        /// <summary>
        /// Retrieves the court data from CourtListener
        /// </summary>
        private Court GetCourtFromAPI(string CourtUrl)
        {
            Console.WriteLine("API Court "+ CourtUrl);
            Request request = new Request();
            Credentials credentials = new Credentials();
            credentials.UserName = "peidelman";
            credentials.Password = "Dyn4m1c5!";
            request.Credentials = credentials;


            var fullUrl = string.Format("{0}/{1}/{2}", CourtListenerUrl, "api/rest/v3/courts", CourtUrl + "/?format=json");

            // Retrieves the response from CourtListener API
            request.URL = fullUrl;

            var data = request.Execute();

            // Verify if the response is good
            if (request.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            // return deserialized court
            return JsonConvert.DeserializeObject<Court>(data);
        }

        /// <summary>
        /// Retrieves the document data from CourtListner
        /// </summary>
        private ApiDocument GetDocumentFromAPI(string DocumentUrl)
        {
            Console.WriteLine("API Document");
            ApiDocument temp = new ApiDocument();
            var http = new HttpClient();
            var tempApiCluster = new APICluster();

            // Configure the client
            Request request = new Request();
            Credentials credentials = new Credentials();
            credentials.UserName = "peidelman";
            credentials.Password = "Dyn4m1c5!";
            request.Credentials = credentials;


            var query = string.Format("{0}/{1}/{2}", CourtListenerUrl, "api/rest/v3/clusters", DocumentUrl + "/?format=json");
            // Retrieves the response from CourtListener API
            request.URL = query;

            var data = request.Execute();

            // Verify if the response is good
            if (request.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            tempApiCluster = JsonConvert.DeserializeObject<APICluster>(data);

            request.URL = tempApiCluster.Docket;

            // Verify if second the response is good
            if (request.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //throw new Exception("Failed to fetch court data from api");
                return null;
            }

            data = request.Execute();

            // var responseCourt = http.Get(tempApiDocument.Docket);
            // return deserialized court
            return JsonConvert.DeserializeObject<ApiDocument>(data);
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
