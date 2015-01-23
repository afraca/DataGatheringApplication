using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;

namespace DataGatheringApplication.requests
{
    internal class OrganizationCollectionRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "orgs.xml?api_key={api_key}&page={current_page}";

        public OrganizationCollectionRequester(string key, int page) : base(key)
        {
            var obj = new {api_key = ApiKey, current_page = page};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
        }

        public List<OrganizationCollectionItem> GetItemsOnPage()
        {
            var response = GetResponse();
            var xdocument = XDocument.Parse(response);
            //var projects = xdocument.Descendants("org");
            //var itemList = new List<OrganizationCollectionItem>(10);

            var pageItems = xdocument.Descendants("org").Select(project => new OrganizationCollectionItem
            {
                Name = project.Element("name").Value,
                XmlUrl = project.Element("url").Value,
                Homepage = project.Element("homepage_url").Value,
                UrlName = project.Element("url_name").Value,
                Type = project.Element("type").Value,
                ProjectsCount = Convert.ToInt32(project.Element("projects_count").Value),
                AffiliatedCommitters = Convert.ToInt32(project.Element("affiliated_committers").Value)
            }).ToList();

            return pageItems;
        }
    }
}