using System;

namespace DataGatheringApplication.DataObjects.Api
{
    public class OrganizationCollectionItem
    {
        public int AffiliatedCommitters;
        public string Homepage;
        public string Name;
        public int ProjectsCount;
        public string Type;
        public string UrlName;
        public string XmlUrl;

        public OrganizationCollectionItem(string name, string xmlUrl, DateTime createdAt, DateTime updatedAt,
            string homepage, string urlName, string type, int projectsCount, int affiliatedCommitters)
        {
            Name = name;
            XmlUrl = xmlUrl;
            Homepage = homepage;
            UrlName = urlName;
            Type = type;
            ProjectsCount = projectsCount;
            AffiliatedCommitters = affiliatedCommitters;
        }

        public OrganizationCollectionItem()
        {
        }
    }
}