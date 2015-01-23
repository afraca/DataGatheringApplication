using System;
using System.IO;
using System.Net;

namespace DataGatheringApplication.requests
{
    internal abstract class OhlohApiRequester
    {
        protected const string BaseUrl = "https://www.openhub.net/";
        protected string ApiKey;
        protected WebRequest Request;

        protected OhlohApiRequester(string key)
        {
            ApiKey = key;
        }

        protected string GetResponse()
        {
            // Get the response.
            try
            {
                var response = Request.GetResponse();
                // Display the status.
                Console.WriteLine(((HttpWebResponse) response).StatusDescription);
                // Get the stream containing content returned by the server.
                var dataStream = response.GetResponseStream();
                // Open the stream using a StreamReader for easy access.
                var reader = new StreamReader(dataStream);
                // Read the content.
                var responseFromServer = reader.ReadToEnd();
                // Clean up the streams and the response.
                reader.Close();
                response.Close();

                return responseFromServer;
            }
            catch (WebException e)
            {
                Console.WriteLine(e);
                return "";
            }
        }

        protected Stream GetResponseStream()
        {
            var response = Request.GetResponse();
            // Display the status.
            Console.WriteLine(((HttpWebResponse) response).StatusDescription);
            // Get the stream containing content returned by the server.
            return response.GetResponseStream();
        }
    }
}