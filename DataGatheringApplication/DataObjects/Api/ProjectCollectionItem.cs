using System;

namespace DataGatheringApplication.DataObjects.Api
{
    internal class ProjectCollectionItem
    {
        public ProjectCollectionItem(int id, string name, string xmlUrl, DateTime createdAt, DateTime updatedAt,
            string urlName, int analysisId)
        {
            Id = id;
            Name = name;
            XmlUrl = xmlUrl;
            UrlName = urlName;
            AnalysisId = analysisId;
        }

        public ProjectCollectionItem()
        {
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string XmlUrl { get; set; }
        public string UrlName { get; set; }
        public int AnalysisId { get; set; }
    }
}