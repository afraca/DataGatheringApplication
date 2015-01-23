using System;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;
using DataGatheringApplication.requests;

namespace DataGatheringApplication.Requests
{
    internal class ActivityFactRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "projects/{project_id}/analyses/latest/activity_facts.xml?api_key={api_key}";
        private readonly string _projectUrlName;

        public ActivityFactRequester(string key, string projectUrlName) : base(key)
        {
            var obj = new {api_key = ApiKey, project_id = projectUrlName};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
            // The id is not an integer in this case
            _projectUrlName = projectUrlName;
        }

        public ActivityFact GetLatestActivityFact()
        {
            var xdocument = XDocument.Load(GetResponseStream());

            //var lastXmlItem = xdocument.Descendants("activity_fact").Last();
            // It has been determined the actual last month will report 0's way way way more
            // times than the month before, some flaw in the OpenHub API, so we actually take
            // the second to last item.
            var lastXmlItem = xdocument.Descendants("activity_fact").Reverse().Skip(1).First();

            return new ActivityFact
            {
                ProjectUrlName = _projectUrlName,
                MonthsAmount =
                    Convert.ToInt32(xdocument.Descendants("response").First().Element("items_returned").Value),
                CodeAdded = Convert.ToInt32(lastXmlItem.Element("code_added").Value),
                CodeRemoved = Convert.ToInt32(lastXmlItem.Element("code_removed").Value),
                CommentsAdded = Convert.ToInt32(lastXmlItem.Element("comments_added").Value),
                CommentsRemoved = Convert.ToInt32(lastXmlItem.Element("comments_removed").Value),
                BlanksAdded = Convert.ToInt32(lastXmlItem.Element("blanks_added").Value),
                BlanksRemoved = Convert.ToInt32(lastXmlItem.Element("blanks_removed").Value),
                Commits = Convert.ToInt32(lastXmlItem.Element("commits").Value),
                Contributors = Convert.ToInt32(lastXmlItem.Element("contributors").Value)
            };
        }
    }
}