using System.Net;

namespace DataGatheringApplication.requests
{
    internal class AnalysisRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "projects/{project_id}/analyses/latest/activity_facts.xml";

        public AnalysisRequester(string key, int projectId) : base(key)
        {
            var obj = new {api_key = ApiKey, project_id = projectId};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
        }
    }
}