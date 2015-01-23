using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using DataGatheringApplication.DataObjects.Api;
using DataGatheringApplication.requests;

namespace DataGatheringApplication.Requests
{
    internal class LanguageRequester : OhlohApiRequester
    {
        private readonly string _callUrl = "languages.xml?api_key={api_key}&page={page}";

        public LanguageRequester(string key, int iPage) : base(key)
        {
            var obj = new {api_key = ApiKey, page = iPage};
            _callUrl = NamedFormat.Format(_callUrl, obj);
            Request = WebRequest.Create(BaseUrl + _callUrl);
        }

        public List<Language> GetLanguagesOnPage()
        {
            var xdocument = XDocument.Load(GetResponseStream());

            return xdocument.Descendants("language").Select(language =>
                new Language
                {
                    Category = language.Element("category").Value,
                    Id = Convert.ToInt32(language.Element("id").Value),
                    Name = language.Element("name").Value
                }).ToList();
        }
    }
}