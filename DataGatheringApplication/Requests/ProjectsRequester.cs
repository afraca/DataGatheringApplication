using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;
using DataGatheringApplication.requests;

namespace DataGatheringApplication.Requests
{
    internal class ProjectsRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "projects.xml?api_key={api_key}";
        //"orgs/{org_url_name}.xml?api_key={api_key}&view={view_option}";

        public ProjectsRequester(string key, string searchquery) : base(key)
        {
            // This is the requester for specific queries, we manually append it
            _callUrl = _callUrl + "&query={query}";
            var obj = new {api_key = ApiKey, query = searchquery};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
        }

        public ProjectsRequester(string key, int callPage) : base(key)
        {
            // This constructor makes a requester for a specific page
            // (thankfully, no combination with other constructor required)

            _callUrl = _callUrl + "&page={page}";
            var obj = new {api_key = ApiKey, page = callPage};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
        }

        public ProjectCollectionItem GetItemByName(string name)
        {
            //var response = GetResponse();
            //var xdocument = XDocument.Parse(response);
            var xdocument = XDocument.Load(GetResponseStream());

            return
                xdocument.Descendants("project")
                    .Where(project => project.Element("name").Value == name)
                    .Select(project => new ProjectCollectionItem
                    {
                        Id = Convert.ToInt32(project.Element("id").Value),
                        AnalysisId = Convert.ToInt32(project.Element("analysis_id").Value),
                        Name = project.Element("name").Value,
                        UrlName = project.Element("url_name").Value,
                        XmlUrl = project.Element("url").Value
                    }).First();
        }

        public IEnumerable<LonerProject> GetLonerProjectsOnPage()
        {
            var xdocument = XDocument.Load(GetResponseStream());

            var items = new List<LonerProject>();

            foreach (var project in xdocument.Descendants("project"))
            {
                try
                {
                    var obj = new LonerProject
                    {
                        Id = Convert.ToInt32(project.Element("id").Value),
                        Name = project.Element("name").Value,
                        UrlName = project.Element("url_name").Value,
                        XmlUrl = project.Element("url").Value,
                        AnalysisId = Convert.ToInt32(project.Element("analysis_id").Value),
                        License =
                            project.Element("licenses").Descendants("license").Any()
                                ? project.Element("licenses").Descendants("license").First().Element("name").Value
                                : "",
                        ActivityIndex =
                            Convert.ToInt32(project.Element("project_activity_index").Element("value").Value)
                    };


                    items.Add(obj);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, couldnt create project");
                }
            }

            return items;

            // Take first license. Proven to work ok for most projects
        }
    }
}