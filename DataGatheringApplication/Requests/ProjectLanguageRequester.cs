using System;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;
using DataGatheringApplication.requests;

namespace DataGatheringApplication.Requests
{
    internal class ProjectLanguageRequester : OhlohApiRequester
    {
        // For easy acces to portfolio license combo retrieval
        public int Id;

        public ProjectLanguageRequester(string key, string xmlUrl) : base(key)
        {
            // In goes full xml url, append api key
            var callUrl = xmlUrl + "?api_key=" + ApiKey;
            Request = WebRequest.Create(callUrl);
        }

        public LonerProjectLanguageCombo GetCombo()
        {
            var xdocument = XDocument.Load(GetResponseStream());
            var project = xdocument.Descendants("project").First();
            if (project == null)
            {
                throw new FormatException();
            }

            try
            {
                return new LonerProjectLanguageCombo
                {
                    Id = Convert.ToInt32(project.Element("id").Value),
                    LanguageId = Convert.ToInt32(project.Element("analysis").Element("main_language_id").Value)
                };
            }
            catch (NullReferenceException e)
            {
                throw new FormatException();
            }
        }

        public PortfolioLicenseCombo GetPortfolioLicenseCombo()
        {
            var xdocument = XDocument.Load(GetResponseStream());

            return new PortfolioLicenseCombo
            {
                License = xdocument.Descendants("license").First().Element("name").Value,
                ProjectId = Id
            };
        }
    }
}