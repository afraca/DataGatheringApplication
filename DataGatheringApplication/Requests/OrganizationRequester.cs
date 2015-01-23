using System.Net;

namespace DataGatheringApplication.requests
{
    internal class OrganizationRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "orgs/{org_url_name}.xml?api_key={api_key}&view={view_option}";

        public OrganizationRequester(string orgUrlName, string apiKey, string viewOption = "portfolio_projects")
            : base(apiKey)
        {
            var obj = new {org_url_name = orgUrlName, view_option = viewOption, api_key = ApiKey};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
        }
    }
}