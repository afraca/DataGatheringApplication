using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;

namespace DataGatheringApplication.requests
{
    internal class PortfolioProjectsRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "orgs/{org_name}/projects.xml?api_key={api_key}&page={number}";
        private readonly OrganizationCollectionItem _organization;

        public PortfolioProjectsRequester(string key, OrganizationCollectionItem orgCollection, int page) : base(key)
        {
            var obj = new {api_key = ApiKey, number = page, org_name = orgCollection.UrlName};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);

            // Set organization name for when constructing PortfolioProjectsItem
            _organization = orgCollection;
        }

        public List<PortfolioProjectsItem> GetItemsOnPage()
        {
            var response = GetResponse();
            var xdocument = XDocument.Parse(response);
            //var projects = xdocument.Descendants("org");
            //var itemList = new List<OrganizationCollectionItem>(10);

            var pageItems = xdocument.Descendants("project").Select(project => new PortfolioProjectsItem
            {
                Name = project.Element("name").Value,
                Activity = project.Element("activity").Value,
                PrimaryLanguage = project.Element("primary_language").Value,
                OrganizationName = _organization.Name,
                OrganizationProjectCount = _organization.ProjectsCount
            }).ToList();

            return pageItems;
        }
    }
}