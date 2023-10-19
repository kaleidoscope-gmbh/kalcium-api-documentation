using System.Linq;
using System.Threading.Tasks;
using Kaleidoscope.Kalcium.Client.Models.CheckTerm;

namespace Kaleidoscope.Kalcium.TestClient
{
    public partial class KalcTest
    {
        public async Task RunSegmentAnalysisAsync()
        {
            //Check if CheckTerm is licensed and enabled for the group
            if (!kalcClient.AuthenticationData.Groups.Any(x => x.IsCheckTermModuleEnabled.Value)) 
            {
                Log("CT module is not enabled for the user");
                return;
            }
            //Get analysis settings available for the user
            var profileIds = kalcClient.AuthenticationData.Groups.SelectMany(x => x.AnalysisProfileIds.Value).Distinct().ToList();
            if (!profileIds.Any())
            {
                Log("No Analysis profile is enabled for the user");
                return;
            }

            //Analysis Profiles gather options related to analysis (which termbase to use, with what settings...etc)
            int profileId = profileIds.First(); 
            var profile =  await kalcClient.AnalysisProfilesService.GetAnalysisProfileAsync(profileId);
            //Get termbases for the profile to identify language ids
            var termbases = await kalcClient.TerminologyService.GetTermbasesAsync(profile.TermbaseSettings.Select(x => x.TermbaseId));
            var allLanguageIds = termbases.SelectMany(x => x.LanguageIds).Distinct().ToList();
            var languages = await kalcClient.TerminologyService.GetLanguagesAsync(allLanguageIds);
            var english = languages.FirstOrDefault(x => x.Code == "en-US"); //if you only have language code, you can determine language like this
            var sourceLanguage = allLanguageIds.Take(1).ToArray(); // source language(s) to use 
            var targetLanguages = english != null 
                ? new []{ english.Id } 
                : allLanguageIds.Skip(1).Take(1).Where(x => x > 0).ToArray(); // target language(s) to use (Optional, only used if not Source analysis is used)

            var segment = new Segment { SourceValue = "Some sentence to analyze.", Id = "UNIQUE-ID", Index = 0 }; //Id and Index is optional for this endpoint, however it might be useful or client side if you have any segment-id
            var analysisResults = await kalcClient.AnalysisService.AnalyzeSegmentAsync(segment, profileId, sourceLanguage, targetLanguages, AnalyzeType.Source);
            foreach (var result in analysisResults.AnalyzeResultPairs)
            {
                var isProblematical = result.Source.IsProblematical; //true if it is problematical based on the analysis profile settings 
                if (isProblematical)
                {
                    Log($"Problematical hit is found '{result.Source.Searched}'");
                }
            }
        }
    }
}
