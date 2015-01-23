using System;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;
using DataGatheringApplication.requests;

namespace DataGatheringApplication.Requests
{
    internal class SizeFactRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "projects/{project_id}/analyses/latest/size_facts.xml?api_key={api_key}";
        private readonly string _projectUrlName;

        public SizeFactRequester(string key, string projectUrlName)
            : base(key)
        {
            var obj = new {api_key = ApiKey, project_id = projectUrlName};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
            // The id is not an integer in this case
            _projectUrlName = projectUrlName;
        }

        public SizeFact GetLatestSizeFact()
        {
            var xdocument = XDocument.Load(GetResponseStream());

            //var lastXmlItem = xdocument.Descendants("activity_fact").Last();
            // It has been determined the actual last month will report 0's way way way more
            // times than the month before, some flaw in the OpenHub API, so we actually take
            // the second to last item.
            var lastXmlItem = xdocument.Descendants("size_fact").Reverse().Skip(1).First();

            return new SizeFact
            {
                MonthsAmount =
                    Convert.ToInt32(xdocument.Descendants("response").First().Element("items_returned").Value),
                Code = Convert.ToInt32(lastXmlItem.Element("code").Value),
                Comments = Convert.ToInt32(lastXmlItem.Element("comments").Value),
                Blanks = Convert.ToInt32(lastXmlItem.Element("blanks").Value),
                Commits = Convert.ToInt32(lastXmlItem.Element("commits").Value),
                ManMonths = Convert.ToInt32(lastXmlItem.Element("man_months").Value),
                ProjectUrlName = _projectUrlName
            };
        }
    }
}