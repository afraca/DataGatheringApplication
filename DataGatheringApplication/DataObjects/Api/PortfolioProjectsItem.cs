namespace DataGatheringApplication.DataObjects.Api
{
    public class PortfolioProjectsItem
    {
        public string Activity;
        public string Name;
        public string OrganizationName;
        public int OrganizationProjectCount;
        public string PrimaryLanguage;

        public PortfolioProjectsItem(string name, string activity, string primaryLanguage, string organizationName)
        {
            Name = name;
            Activity = activity;
            PrimaryLanguage = primaryLanguage;
            OrganizationName = organizationName;
        }

        public PortfolioProjectsItem()
        {
        }
    }
}