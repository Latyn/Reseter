using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Threading;


namespace Reseter.RequestManager
{
    /// <summary>
    /// Create Http Request, using json, and read Http Response.
    /// </summary>
    public class Request
    {

        /// <summary>
        /// Url of http server wich request will be created to.
        /// </summary>
        public String URL { get; set; }

        /// <summary>
        /// HTTP Verb wich will be used. Eg. GET, POST, PUT, DELETE.
        /// </summary>
        public String Verb { get; set; }

        /// <summary>
        /// Request content, Json by default.
        /// </summary>
        public String Content
        {
            get { return "text/json"; }
        }

        /// <summary>
        /// User and Password for Basic Authentication
        /// </summary>
        public Credentials Credentials { get; set; }

        /// <summary>
        /// Query tries when server error
        /// </summary>
        public int QTries { get; set; }

        public HttpWebRequest HttpRequest { get; internal set; }
        public HttpWebResponse HttpResponse { get; internal set; }
        public CookieContainer CookieContainer = new CookieContainer();

        /// <summary>
        /// Constructor Overload that allows passing URL and the VERB to be used.
        /// </summary>
        /// <param name="url">URL which request will be created</param>
        /// <param name="verb">Http Verb that will be userd in this request</param>
        public Request(string url, string verb)
        {
            URL = url;
            Verb = verb;
            QTries = 0;
        }

        /// <summary>
        /// Default constructor overload without any paramter
        /// </summary>
        public Request()
        {
            Verb = "GET";
        }

        public object Execute<TT>(string url, object obj, string verb)
        {
            if (url != null)
                URL = url;

            if (verb != null)
                Verb = verb;

            HttpRequest = CreateRequest();

            WriteStream(obj);

            try
            {
                HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();
            }
            catch (WebException error)
            {
                HttpResponse = (HttpWebResponse)error.Response;
                return ReadResponseFromError(error);
            }

            return JsonConvert.DeserializeObject<TT>(ReadResponse());
        }

        public object Execute<TT>(string url)
        {
            if (url != null)
                URL = url;

            HttpRequest = CreateRequest();

            try
            {
                HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();
                return JsonConvert.DeserializeObject<TT>(ReadResponse());
            }
            catch (WebException error)
            {
                HttpResponse = (HttpWebResponse)error.Response;
                return ReadResponseFromError(error);
            }


        }

        public object Execute<TT>()
        {
            if (URL == null)
                throw new ArgumentException("URL cannot be null.");

            HttpRequest = CreateRequest();

            try
            {
                HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();
            }
            catch (WebException error)
            {
                HttpResponse = (HttpWebResponse)error.Response;
                return ReadResponseFromError(error);
            }

            return JsonConvert.DeserializeObject<TT>(ReadResponse());
        }

        public object Execute(string url, object obj, string verb)
        {
            if (url != null)
                URL = url;

            if (verb != null)
                Verb = verb;

            HttpRequest = CreateRequest();

            WriteStream(obj);

            try
            {
                HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();
            }
            catch (WebException error)
            {
                HttpResponse = (HttpWebResponse)error.Response;
                return ReadResponseFromError(error);
            }

            return ReadResponse();
        }

        public object Execute(string url)
        {
            if (url != null)
                URL = url;

            HttpRequest = CreateRequest();

            try
            {
                HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();
            }
            catch (WebException error)
            {
                HttpResponse = (HttpWebResponse)error.Response;
                return ReadResponseFromError(error);
            }

            return ReadResponse();
        }

        public string Execute()
        {
            if (URL == null)
            {
                throw new ArgumentException("URL cannot be null.");
            }

            //Create the Request
            HttpRequest = CreateRequest();

            try
            {
                HttpResponse = (HttpWebResponse) HttpRequest.GetResponse();

                if (HttpResponse == null)
                {
                    return null;
                }
            }
            catch (System.Net.WebException ex)
            {
                HttpResponse = (HttpWebResponse)ex.Response;

                // server errors
                switch (HttpResponse.StatusCode)
                {
                    case HttpStatusCode.NotFound: // 404
                        Console.WriteLine("Waiting .... Connection issues (400)");
                        Thread.Sleep(5000);
                        return HttpResponse.StatusCode.ToString();

                    case HttpStatusCode.InternalServerError: // 500
                        Console.WriteLine("Waiting .... Connection issues (500)");
                        Thread.Sleep(50000);
                        return HttpResponse.StatusCode.ToString();

                    default: Thread.Sleep(30000);
                        return ex.Message;
                }
            }

            // return the response body
            return ReadResponse();
        }

        internal HttpWebRequest CreateRequest()
        {
            var basicRequest = (HttpWebRequest)WebRequest.Create(URL);
            basicRequest.ContentType = Content;
            basicRequest.Method = Verb;
            basicRequest.CookieContainer = CookieContainer;

            if (Credentials != null)
                basicRequest.Headers.Add("Authorization", "Basic" + " " + EncodeCredentials(Credentials));
                //basicRequest.Headers.Add("Authorization", "Token" + " " + "aa0ea77bf371f14e3c28fbd7b01be78ed24a6e86");

            return basicRequest;
        }

        internal void WriteStream(object obj)
        {
            if (obj != null)
            {
                using (var streamWriter = new StreamWriter(HttpRequest.GetRequestStream()))
                {
                    if (obj is string)
                        streamWriter.Write(obj);
                    else
                        streamWriter.Write(JsonConvert.SerializeObject(obj));
                }
            }
        }

        internal String ReadResponse()
        {
            if (HttpResponse != null)
            {
                using (var streamReader = new StreamReader(HttpResponse.GetResponseStream()))
                {
                    return streamReader.ReadToEnd();
                }
            }

            return string.Empty;
        }

        internal String ReadResponseFromError(WebException error)
        {
            using (var streamReader = new StreamReader(error.Response.GetResponseStream()))
                return streamReader.ReadToEnd();
        }

        internal static string EncodeCredentials(Credentials credentials)
        {
            var strCredentials = string.Format("{0}:{1}", credentials.UserName, credentials.Password);
            var encodedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(strCredentials));

            return encodedCredentials;
        }

    }
}
