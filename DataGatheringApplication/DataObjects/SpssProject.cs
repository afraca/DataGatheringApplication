namespace DataGatheringApplication.DataObjects
{
    internal class SpssProject
    {
        #region Activity info

        public int BlanksAdded;
        public int BlanksRemoved;
        public int CodeAdded;
        public int CodeRemoved;
        public int CommentsAdded;
        public int CommentsRemoved;
        public int Commits;
        public int Contributors;
        public int MonthsAmount;

        #endregion

        #region Organization info

        public string OrganizationName;
        public int OrganizationProjectCount;
        // 1 if having affiliated contributors in organization
        public int ProjectManagement;
        // Organization info here
        // PortFolio: 1 = true, 0 = false
        public int PortFolioProject;

        #endregion

        #region Shared general info

        public int ProjectId;
        public string License;
        public string ProjectUrlName;
        public string PrimaryLanguage;

        #endregion
    }
}