using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml;
using CsvFiles;
using DataGatheringApplication.DataObjects;
using DataGatheringApplication.DataObjects.Api;
using DataGatheringApplication.requests;
using DataGatheringApplication.Requests;
using MoreLinq;

namespace DataGatheringApplication
{
    internal class Program
    {
        public const string DataLocation = "D:/Owncloud/Documents/Studie/Bachelorthesis/Data/";
        public const int OrganizationCollectionPages = 81;
        private static KeyManager _keyManager;

        public static void Main(string[] args)
        {
            // See readme.md for remarks. 

            _keyManager = new KeyManager();

            //GetOrganizationCollectionItems();
            //GetPortfolioProjectItems();
            //GetProjectCollectionItems();
            //GetLonerProjects();
            //GetActivityFacts();
            //GetOldLonerActivityFacts();
            //GetLanguages();
            //GetLonerLanguageCombos();
            //GetPortfolioLicenses();
            //ComposeProjectInformation();


            //GetSizeFacts();
        }

        public static void GetOrganizationCollectionItems()
        {
            // On writing, .csv will automatically be appended.

            #region write to local file

            // By manual lookup the limit of 1000 items has been found. 
            // In reality, at the time of writing it will be about 807.
            var items = new List<OrganizationCollectionItem>(1000);

            for (var page = 1; page <= OrganizationCollectionPages; page++)
            {
                var key = _keyManager.GetApiKey();
                var requester = new OrganizationCollectionRequester(key, page);
                var pageItems = requester.GetItemsOnPage();
                items.AddRange(pageItems);
            }

            //write the whole result to a csv file
            items.ToCsv(DataLocation + OrganizationCollectionFilename);

            #endregion

            /*var test = CsvFile.Read<OrganizationCollectionItem>(DataLocation + filename + ".csv");
            foreach (var item in test)
            {
                Console.WriteLine(item.UrlName);
            }*/
            Console.ReadLine();
        }

        public static void GetPortfolioProjectItems()
        {
            var items = new List<PortfolioProjectsItem>(2500);

            #region write to local file

            // get the organizations. 
            var organizations =
                CsvFile.Read<OrganizationCollectionItem>(DataLocation + OrganizationCollectionFilename + ".csv");

            foreach (var organization in organizations)
            {
                var key = _keyManager.GetApiKey();
                // there are 20 results per page, so we divide the total amount of projects for an organization
                // by 20 to get the required amount of pages
                var pages = (int) Math.Ceiling(organization.ProjectsCount/20.0);
                // Watch out for the off by one. 341 items needs a page 18 for example.
                for (var page = 1; page <= pages; page++)
                {
                    var requester = new PortfolioProjectsRequester(key, organization, page);
                    items.AddRange(requester.GetItemsOnPage());
                }
                //var requester = new PortfolioProjectsRequester(key, organizationName, )
            }

            items.ToCsv(DataLocation + PortfolioProjectsFilename);

            #endregion
        }

        public static void GetProjectCollectionItems()
        {
            // This method is for getting more info on portfolio projects
            // By default, the API doesn't provide us with a lot in the PortfolioProjectItems
            // So for each project we do a separate query to the API. 

            // The input is the ~2100 portfolio project items downloaded before. These were all
            // the projects belonging to an organization.
            var items = new List<ProjectCollectionItem>(2200);
            //var items = new ConcurrentBag<ProjectCollectionItem>();


            var projects = CsvFile.Read<PortfolioProjectsItem>(DataLocation + PortfolioProjectsFilename + ".csv");
            // As we're dealing with API limit, we select a subset of the whole collection.
            // As our daily limit is 4000, and we need several runs for other calls as well, we
            // do 800
            const int subsetCount = 800;
            var rnd = new Random();
            projects = projects.OrderBy(x => rnd.Next()).Take(subsetCount);

            foreach (var project in projects)
            {
                var key = _keyManager.GetApiKey();

                var requester = new ProjectsRequester(key, project.Name);

                try
                {
                    var item = requester.GetItemByName(project.Name);

                    items.Add(item);
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }

            items.ToCsv(DataLocation + ProjectCollectionFilename);
        }

        public static void GetLonerProjects()
        {
            // Loner = non-portfolio
            var items = new List<LonerProject>(1200);

            // We generate 115 random pagenumbers
            // (We want about 800 projects, about 30% will generate
            // some xml parse error, so that's 81 pages / 0.7 == 115 pages)
            // There are lots of complicated correct ways to do
            // this, but with our odds we'll be fine

            // Setting up
            var rnd = new Random();

            const int requiredPages = 140;
            // Visit the projects listing xml page
            // Math.ceil(available/10) == maxPage
            const int maxPage = 66797;
            var randomPages = new List<int>(requiredPages);

            while (randomPages.Count < requiredPages)
            {
                // upper value excluded
                var randNum = rnd.Next(1, maxPage + 1);
                if (!randomPages.Contains(randNum))
                {
                    randomPages.Add(randNum);
                }
            }

            foreach (var page in randomPages)
            {
                var key = _keyManager.GetApiKey();

                var requester = new ProjectsRequester(key, page);

                items.AddRange(requester.GetLonerProjectsOnPage());
            }

            // Filter for projects already in portfolio projects
            var portfolioprojectIds =
                CsvFile.Read<ProjectCollectionItem>(DataLocation + ProjectCollectionFilename + ".csv").ToList();
            var idlist = portfolioprojectIds.Select(project => project.Id);

            items = items.Where(lonerProject => !idlist.Contains(lonerProject.Id)).ToList();


            items.ToCsv(DataLocation + LonerProjectsFilename);
        }

        public static void GetActivityFacts()
        {
            // Set up the collection holding our result, all the activityfacts
            // About 2000 is to be expected, as dictated by previous results
            var items = new List<ActivityFact>(2000);

            // first load the file which holds the basic project information for
            // portfolio projects
            var portfolioProjects =
                CsvFile.Read<ProjectCollectionItem>(DataLocation + ProjectCollectionFilename + ".csv");

            // Gather activityfacts for those projects
            foreach (var projectCollectionItem in portfolioProjects)
            {
                var key = _keyManager.GetApiKey();

                var requester = new ActivityFactRequester(key, projectCollectionItem.UrlName);

                try
                {
                    items.Add(requester.GetLatestActivityFact());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }


            // then load the file which holds the basic project information for
            // non-portfolio projects
            var lonerProjects = CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + ".csv");

            // Gather activityfacts for those projects
            foreach (var lonerProject in lonerProjects)
            {
                var key = _keyManager.GetApiKey();

                var requester = new ActivityFactRequester(key, lonerProject.UrlName);

                try
                {
                    items.Add(requester.GetLatestActivityFact());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }


            // Write results to file       
            items.ToCsv(DataLocation + ActivityFactsFilename);
        }

        public static void GetOldLonerActivityFacts()
        {
            // Set up the collection holding our result, all the activityfacts
            // About 2000 is to be expected, as dictated by previous results
            var items = new List<ActivityFact>(890);

            // then load the file which holds the basic project information for
            // non-portfolio projects, NOTE! the old set!
            var lonerProjects = CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + "_old_set.csv");

            // Gather activityfacts for those projects
            foreach (var lonerProject in lonerProjects)
            {
                var key = _keyManager.GetApiKey();

                var requester = new ActivityFactRequester(key, lonerProject.UrlName);

                try
                {
                    items.Add(requester.GetLatestActivityFact());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }

            // Write results to file, NOTE! append old_set to don't overwrite    
            items.ToCsv(DataLocation + ActivityFactsFilename + "_old_set");
        }

        public static void GetLanguages()
        {
            // As of writing, 102 languages, 10 per page => 11 pages
            const int pagesRequired = 11;

            var items = new List<Language>(102);

            for (var i = 1; i <= pagesRequired; i++)
            {
                var key = _keyManager.GetApiKey();
                var requester = new LanguageRequester(key, i);
                try
                {
                    items.AddRange(requester.GetLanguagesOnPage());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }

            items.ToCsv(DataLocation + LanguagesFilename);
        }

        public static void GetLonerLanguageCombos()
        {
            // Load loner projects in
            // Non-portfolio projects (lonerprojects)
            var lonerProjects = CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + ".csv").ToList();
            var oldLonerProjects = CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + "_old_set.csv");
            lonerProjects.AddRange(oldLonerProjects);

            // this holds eventual results
            var items = new List<LonerProjectLanguageCombo>(1950);

            foreach (var lonerProject in lonerProjects)
            {
                var key = _keyManager.GetApiKey();
                var requester = new ProjectLanguageRequester(key, lonerProject.XmlUrl);
                try
                {
                    items.Add(requester.GetCombo());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }

            items.ToCsv(DataLocation + LonerLanguageCombosFilename);
        }

        public static void GetPortfolioLicenses()
        {
            var portfolioProjects =
                CsvFile.Read<ProjectCollectionItem>(DataLocation + ProjectCollectionFilename + ".csv").ToList();

            var items = new List<PortfolioLicenseCombo>(770);

            foreach (var portfolioProject in portfolioProjects)
            {
                var key = _keyManager.GetApiKey();
                var requester = new ProjectLanguageRequester(key, portfolioProject.XmlUrl) {Id = portfolioProject.Id};
                try
                {
                    items.Add(requester.GetPortfolioLicenseCombo());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }

            items.ToCsv(DataLocation + PortfolioLicensesFilename);
        }

        public static void ComposeProjectInformation()
        {
            // Get the required input information

            // First activity facts
            var activityFacts = CsvFile.Read<ActivityFact>(DataLocation + ActivityFactsFilename + ".csv").ToList();
            var oldActivityFacts =
                CsvFile.Read<ActivityFact>(DataLocation + ActivityFactsFilename + "_old_set.csv").ToList();
            activityFacts.AddRange(oldActivityFacts);

            // Non-portfolio projects (lonerprojects)
            var lonerProjects = CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + ".csv").ToList();
            var oldLonerProjects =
                CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + "_old_set.csv").ToList();
            lonerProjects.AddRange(oldLonerProjects);

            // Language file needed for linking
            var languages = CsvFile.Read<Language>(DataLocation + LanguagesFilename + ".csv").ToList();
            // Non-portfolio projects only have a language id, so match them with a language string name
            var lonerLanguages =
                CsvFile.Read<LonerProjectLanguageCombo>(DataLocation + LonerLanguageCombosFilename + ".csv").ToList();
            var lonerLangQuery = lonerLanguages.Join(languages, combo => combo.LanguageId, language => language.Id,
                (combo, lang) => new
                {
                    Language = lang.Name,
                    ProjectId = combo.Id
                }).ToList();

            // Now as more helping information, the file which holds license information for portfolio projects
            var portfolioLicenses =
                CsvFile.Read<PortfolioLicenseCombo>(DataLocation + PortfolioLicensesFilename + ".csv").ToList();

            // Now organization collection items
            var organizations =
                CsvFile.Read<OrganizationCollectionItem>(DataLocation + OrganizationCollectionFilename + ".csv")
                    .ToList();
            // portfolio projects. There are two files, as the initial portfolio project requests 
            // wasn't providing us with nice urlNames.
            var portfolioProjects =
                CsvFile.Read<PortfolioProjectsItem>(DataLocation + PortfolioProjectsFilename + ".csv").ToList();
            var projectsCollection =
                CsvFile.Read<ProjectCollectionItem>(DataLocation + ProjectCollectionFilename + ".csv").ToList();

            // Now comes the hard part, joining them all together

            // we start off with the portfolio projects, the info comes from two separate sources
            var portfolioQuery = portfolioProjects.Join(projectsCollection, project => project.Name
                , colProject => colProject.Name, (proj, colProject) => new
                {
                    colProject.Id,
                    colProject.Name,
                    colProject.UrlName,
                    proj.PrimaryLanguage,
                    proj.OrganizationName,
                    proj.OrganizationProjectCount
                }).ToList();

            // Now add some more organization information to it (affiliated committers)
            var portfolioQuery2 = portfolioQuery.Join(organizations, project => project.OrganizationName,
                organization => organization.Name, (project, organization) => new
                {
                    project.Id,
                    project.Name,
                    project.UrlName,
                    project.PrimaryLanguage,
                    project.OrganizationName,
                    project.OrganizationProjectCount,
                    organization.AffiliatedCommitters
                }).ToList();

            // Outer left join in linq is a bit harder
            // adapted from msdn example
            var portfolioQuery3 =
                from project in portfolioQuery2
                join license in portfolioLicenses on project.Id equals license.ProjectId into foo
                from subLicenses in foo.DefaultIfEmpty()
                select new
                {
                    project.Id,
                    project.Name,
                    project.UrlName,
                    project.PrimaryLanguage,
                    project.OrganizationName,
                    project.OrganizationProjectCount,
                    project.AffiliatedCommitters,
                    License = (subLicenses == null) ? String.Empty : subLicenses.License
                };

            // Now also add the activity information to it
            var portFolioProjectEnhanced = portfolioQuery3.Join(activityFacts, portProject => portProject.UrlName,
                activity => activity.ProjectUrlName, (portProject, activity) => new
                {
                    portProject.Id,
                    portProject.Name,
                    portProject.UrlName,
                    portProject.PrimaryLanguage,
                    portProject.OrganizationName,
                    portProject.OrganizationProjectCount,
                    portProject.AffiliatedCommitters,
                    portProject.License,
                    activity.BlanksAdded,
                    activity.BlanksRemoved,
                    activity.CodeAdded,
                    activity.CodeRemoved,
                    activity.CommentsAdded,
                    activity.CommentsRemoved,
                    activity.Commits,
                    activity.Contributors,
                    activity.MonthsAmount
                }).ToList();

            // Now for the non-portfolio projects
            var lonerProjectsQuery =
                lonerProjects.Join(lonerLangQuery, project => project.Id, langCombo => langCombo.ProjectId,
                    (project, langCombo) => new
                    {
                        project.Id,
                        project.Name,
                        project.UrlName,
                        project.License,
                        langCombo.Language
                    }).ToList();

            // Again, join it with the activity information (the loner projects)
            var lonerProjectsEnhanced =
                lonerProjectsQuery.Join(activityFacts, lonerProject => lonerProject.UrlName,
                    activity => activity.ProjectUrlName, (lonerProject, activity) => new
                    {
                        lonerProject.Id,
                        lonerProject.Name,
                        lonerProject.UrlName,
                        lonerProject.License,
                        lonerProject.Language,
                        activity.BlanksAdded,
                        activity.BlanksRemoved,
                        activity.CodeAdded,
                        activity.CodeRemoved,
                        activity.CommentsAdded,
                        activity.CommentsRemoved,
                        activity.Commits,
                        activity.Contributors,
                        activity.MonthsAmount
                    }).ToList();

            // Now put all results together
            var projects = new List<SpssProject>(2500);
            projects.AddRange(portFolioProjectEnhanced.Select(portfolioProject => new SpssProject
            {
                BlanksAdded = portfolioProject.BlanksAdded,
                BlanksRemoved = portfolioProject.BlanksRemoved,
                CodeAdded = portfolioProject.CodeAdded,
                CodeRemoved = portfolioProject.CodeRemoved,
                CommentsAdded = portfolioProject.CommentsAdded,
                CommentsRemoved = portfolioProject.CommentsRemoved,
                Commits = portfolioProject.Commits,
                Contributors = portfolioProject.Contributors,
                MonthsAmount = portfolioProject.MonthsAmount,
                OrganizationName = portfolioProject.OrganizationName,
                OrganizationProjectCount = portfolioProject.OrganizationProjectCount,
                ProjectManagement = portfolioProject.AffiliatedCommitters > 0 ? 1 : 0,
                PortFolioProject = 1,
                ProjectId = portfolioProject.Id,
                License = (portfolioProject.License == "" ? "UNKNOWN" : portfolioProject.License),
                ProjectUrlName = portfolioProject.UrlName,
                PrimaryLanguage = portfolioProject.PrimaryLanguage
            }));

            // First for portfolio items

            // Now for the loner (non-portfolio) projects
            projects.AddRange(lonerProjectsEnhanced.Select(lonerProject => new SpssProject
            {
                BlanksAdded = lonerProject.BlanksAdded,
                BlanksRemoved = lonerProject.BlanksRemoved,
                CodeAdded = lonerProject.CodeAdded,
                CodeRemoved = lonerProject.CodeRemoved,
                CommentsAdded = lonerProject.CommentsAdded,
                CommentsRemoved = lonerProject.CommentsRemoved,
                Commits = lonerProject.Commits,
                Contributors = lonerProject.Contributors,
                MonthsAmount = lonerProject.MonthsAmount,
                OrganizationName = "",
                OrganizationProjectCount = 0,
                ProjectManagement = 0,
                PortFolioProject = 0,
                ProjectId = lonerProject.Id,
                License = (lonerProject.License == "" ? "UNKNOWN" : lonerProject.License),
                ProjectUrlName = lonerProject.UrlName,
                PrimaryLanguage = lonerProject.Language
            }));

            projects.DistinctBy(proj => proj.ProjectId).ToCsv(DataLocation + ComposedProjectsFilename);
        }

        // This method is not being used
        public static void GetSizeFacts()
        {
            // Set up the collection holding our result, all the activityfacts
            // About 2000 is to be expected, as dictated by previous results
            var items = new List<SizeFact>(2000);

            // first load the file which holds the basic project information for
            // portfolio projects
            var portfolioProjects =
                CsvFile.Read<ProjectCollectionItem>(DataLocation + ProjectCollectionFilename + ".csv").Take(10);

            // Gather activityfacts for those projects
            foreach (var projectCollectionItem in portfolioProjects)
            {
                var key = _keyManager.GetApiKey();

                var requester = new SizeFactRequester(key, projectCollectionItem.UrlName);

                try
                {
                    items.Add(requester.GetLatestSizeFact());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }


            // then load the file which holds the basic project information for
            // non-portfolio projects
            var lonerProjects = CsvFile.Read<LonerProject>(DataLocation + LonerProjectsFilename + ".csv");

            // Gather activityfacts for those projects
            foreach (var lonerProject in lonerProjects)
            {
                var key = _keyManager.GetApiKey();

                var requester = new SizeFactRequester(key, lonerProject.UrlName);

                try
                {
                    items.Add(requester.GetLatestSizeFact());
                }
                catch (FormatException)
                {
                    Console.WriteLine("Malformed xml, could not parse");
                }
                catch (WebException)
                {
                    Console.WriteLine("WebException");
                }
                catch (XmlException)
                {
                    Console.WriteLine("XmlException");
                }
                catch (InvalidOperationException)
                {
                    Console.WriteLine("Source was empty");
                }
            }

            // Write results to file       
            items.ToCsv(DataLocation + SizeFactsFilename);
        }

        #region filenames

        public const string OrganizationCollectionFilename = "OrganizationCollection";
        public const string PortfolioProjectsFilename = "PortfolioProjects";
        public const string ProjectCollectionFilename = "ProjectCollection";
        public const string ActivityFactsFilename = "ActivityFacts";
        public const string SizeFactsFilename = "SizeFacts";
        public const string LonerProjectsFilename = "LonerProjects";
        public const string LanguagesFilename = "Languages";
        public const string LonerLanguageCombosFilename = "LonerLanguages";
        public const string PortfolioLicensesFilename = "PortfolioLicenses";
        public const string ComposedProjectsFilename = "ComposedProjectInformation";

        #endregion
    }
}