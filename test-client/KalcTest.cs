using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kaleidoscope.Kalcium.Client;
using Kaleidoscope.Kalcium.Client.Models.SearchSettings;
using Kaleidoscope.Kalcium.Client.Models.TaskManagement;
using Kaleidoscope.Kalcium.Client.Models.Terminology;
using Kaleidoscope.Kalcium.Client.Services;
using Newtonsoft.Json;

namespace Kaleidoscope.Kalcium.TestClient
{
    public partial class KalcTest
    {
        private readonly string serverUrl;
        private KalcClient kalcClient;
        private Dictionary<int, Termbase> myTermbases;
        private Dictionary<int, SchemaDefinition> myTermbaseDefinitions;


        public KalcTest(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        public async Task<object> RunTests(string username, string password, string testTermbaseName)
        {
            try
            {
                kalcClient = new KalcClient(serverUrl);
                //This setting is used only for testing purposes. Use the appropriate NuGet package of Kaleidoscope.Kalcium.Client according to the server you want to connect
                kalcClient.KalcHttp.IgnoreKalcVersion = true;

                Log($"Connecting to Kalcium REST API on address {kalcClient.KalcHttp.BackendUrl}");
                await kalcClient.Login(username, password);
                Log($"Successfully logged in with user {username}");
            }
            catch (KalcHttpException exception)
            {
                LogKalcError(exception);
                return null;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return null;
            }

            try
            {
                myTermbases = await QueryTermbases();
                myTermbaseDefinitions = await QueryTermbaseSchemaDefinitions();

                var termbase = myTermbases.Values.First(tb => tb.Name == testTermbaseName);
                await Search(termbase);
                var entry = await CreateEntry(termbase);
                await DeleteEntry(entry.Id.UUID, entry.TermbaseId);
                var termRequest = await CreateTermRequest(termbase);
                await DeleteTermRequest(termRequest);
            }
            catch (KalcHttpException exception)
            {
                LogKalcError(exception);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            try
            {
                await RunSegmentAnalysisAsync();
            }
            catch (KalcHttpException exception)
            {
                LogKalcError(exception);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            try
            {
                await kalcClient.Logout();
                Log("Successfully logged out");
            }
            catch (KalcHttpException exception)
            {
                LogKalcError(exception);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
            return null;
        }


        private async Task<Dictionary<int, Termbase>> QueryTermbases()
        {
            Log("Querying available termbases");
            var myTermbaseIds = kalcClient.AuthenticationData.Groups
                .SelectMany(group => group.Termbases.Where(gtb => gtb.IsEnabled.Value).Select(gtb => gtb.TermbaseId)).Distinct().ToList();

            var termbases = await kalcClient.TerminologyService.GetTermbasesAsync(myTermbaseIds);
            foreach (var tb in termbases)
            {
                Log($" > Termbase '{tb.Name}' [#{tb.Id}] found");
            }
            return termbases.ToDictionary(tb => tb.Id, tb => tb);
        }

        private async Task<Dictionary<int, SchemaDefinition>> QueryTermbaseSchemaDefinitions()
        {
            Log("Querying termbase schema definitions");
            var myTermbaseIds = kalcClient.AuthenticationData.Groups
                .SelectMany(group => group.Termbases.Where(gtb => gtb.IsEnabled.Value).Select(gtb => gtb.TermbaseId)).Distinct().ToList();
            var termbaseSchemaDefinitions = await kalcClient.TerminologyService.GetTermbaseDefinitionsAsync(myTermbaseIds);
            foreach (var tbdef in termbaseSchemaDefinitions)
            {
                Log($" > Definition for termbase '{tbdef.TermbaseName}' [#{tbdef.TermbaseId}] found");
            }
            return termbaseSchemaDefinitions.ToDictionary(tbdef => tbdef.TermbaseId, tbdef => tbdef);
        }


        private async Task Search(Termbase termbase)
        {
            var term = ""; //term to search for
            Log($"Test searching for '{term}'");
            //for high performance search, use kalcClient.Search.SearchRawAsync instead
            var results = await kalcClient.SearchService.SearchAsync(new SearchRequest
            {
                Term = term, 
                Mode = SearchMode.Prefix,
                StartIndex = 0, // start index
                MaxCount = 20,// max number of hits to search for
                SourceLanguageIds = termbase.LanguageIds,
                TargetLanguageIds = termbase.LanguageIds,
                TermbaseSettings = new List<SearchTermbaseSettings> { new SearchTermbaseSettings { TermbaseId = termbase.Id } }, // termbases to search for
                Feature = SearchFeature.Search,
            });
            Log($" > Total number of matches: {results.Total}");
            Log($" > Number of hits returned: {results.Hits.Count}");
            Log($" > Number of related entries: {results.Entries.Count}");
        }

        private async Task DeleteEntry(string entryUuid, int termbaseId)
        {
            Log($"Deleting entry {entryUuid}");
            await kalcClient.TerminologyService.DeleteEntryAsync(entryUuid, termbaseId);
            Log($" > Entry {entryUuid} has been deleted");
        }

        private async Task<Entry> CreateEntry(Termbase termbase)
        {
            Log("Test creating entry");
            var tbDef = myTermbaseDefinitions[termbase.Id];
            var ee = new EditableEntry
            {
                TermbaseId = termbase.Id,
                Languages = new List<EditableLanguageGroup>
                {
                    new EditableLanguageGroup
                    {
                        LanguageId = tbDef.LanguageGroupDefinitions.First().LanguageId,
                       Terms = new List<EditableTermGroup>
                       {
                           new EditableTermGroup
                           {
                               Term = $"test term in termbase {termbase.Name}, language ${tbDef.LanguageGroupDefinitions.First().LanguageName}"
                           }
                       }
                    }
                }

            };
            Log($" > Adding term to language {tbDef.LanguageGroupDefinitions.First().LanguageName} (#{tbDef.LanguageGroupDefinitions.First().LanguageId}): '${ee.Languages[0].Terms[0].Term}'");
            ee.Fields = new List<EditableFieldGroup>();
            //test text fields
            var textFieldDef = tbDef.FieldDefinitions.FirstOrDefault(fieldDef => fieldDef.FieldType == FieldTypes.Text);
            if (textFieldDef != null)
            {
                ee.Fields.Add(new EditableFieldGroup
                {
                    Name = textFieldDef.Name,
                    Value = "sample text field content"
                });
                Log($" > Adding entry level text field, field name: ${textFieldDef.Name}; value: ${ee.Fields[0].Value}");
            }
            //test media fields
            var mediaFiles = new List<UploadFileModel>();
            var imageFieldDef =
                tbDef.FieldDefinitions.FirstOrDefault(fieldDef => fieldDef.FieldType == FieldTypes.Multimedia);
            if (imageFieldDef != null)
            {
                var sampleImagePath = Path.Combine(Environment.CurrentDirectory, "Resources/sample.png");
                if (File.Exists(sampleImagePath))
                {
                    var sampleUploadFileModel = UploadFileModel.LoadUploadFileModel(sampleImagePath);
                    ee.Fields.Add(new EditableFieldGroup
                    {
                        Name = imageFieldDef.Name,
                        Value = sampleUploadFileModel.FileName,
                    });

                    mediaFiles.Add(sampleUploadFileModel);
                }
                Log($" > Adding media field {ee.Fields.Last().Name}: {ee.Fields.Last().Value}");
            }
            var createdEntry = await kalcClient.TerminologyService.CreateEntryAsync(ee, termbase.Id, null, mediaFiles);
            Log($" > Entry created with id #{createdEntry.Id.UUID}");
            Log(JsonConvert.SerializeObject(createdEntry, Formatting.Indented));

            //test reading entry
            var queryEntryResult = await kalcClient.TerminologyService.GetEntriesByUUIDAsync(tbDef.TermbaseId, new[] { createdEntry.Id.UUID },
                termbase.LanguageIds, false, false, true, false, false);
            var queriedEntry = queryEntryResult.Entries[0];
            if (queriedEntry != null)
            {
                Log($" > Entry #{createdEntry.Id.UUID} has been queried again");
                Log(JsonConvert.SerializeObject(queriedEntry, Formatting.Indented));
            }

            if (imageFieldDef != null && queriedEntry != null)
            {
                var fileName = queriedEntry.Fields.Last().Value;
                var streamResult = await kalcClient.TerminologyService.GetMediaFileAsync(
                    tbDef.TermbaseId, fileName, false, 
                    null, queriedEntry.Id.UUID, null, 300, 300);
                var bytes = await streamResult.ReadAsByteArrayAsync();
                var tempFile = Path.GetTempPath() + Guid.NewGuid() + Path.GetExtension(queriedEntry.Fields.Last().Value);
                await File.WriteAllBytesAsync(tempFile, bytes);
                Log($" >> Image attachment has been downloaded to {tempFile}");
            }

            return queriedEntry;
        }

        private async Task DeleteTermRequest(TermRequest termRequest)
        {
            Log($"Deleting term request {termRequest.Id}");
            await kalcClient.BaseTasksService.DeleteAsync(new[] { termRequest.Id });
            Log($" > Term request {termRequest.Id} has been deleted");
        }

        private async Task<TermRequest> CreateTermRequest(Termbase termbase)
        {
            Log("Test creating entry");
            var tbDef = myTermbaseDefinitions[termbase.Id];
            var ee = new EditableEntry
            {
                TermbaseId = termbase.Id,
                Languages = new List<EditableLanguageGroup>
                {
                    new EditableLanguageGroup
                    {
                        LanguageId = tbDef.LanguageGroupDefinitions.First().LanguageId,
                        Terms = new List<EditableTermGroup>
                        {
                            new EditableTermGroup
                            {
                                Term = $"test term request in termbase {termbase.Name}, language ${tbDef.LanguageGroupDefinitions.First().LanguageName}"
                            }
                        }
                    }
                }
            };
            var createTermRequestModel = new CreateTermRequestModel
            {
                Content = ee,
                TermbaseId = termbase.Id,
                Comment = "This is a sample term request",
                SourceExpression = ee.Languages[0].Terms[0].Term,
                SourceLanguageId = ee.Languages[0].LanguageId,
            };
            var newTermRequestId = await kalcClient.TermRequestsService.CreateTermRequestAsync(createTermRequestModel, null, null);

            return (await kalcClient.TermRequestsService.GetTasksByIdAsync(new[] { newTermRequestId.Id }, false, false))[0];
        }



        private void LogKalcError(KalcHttpException exception)
        {
            Log($"ERROR [{exception.StatusCode}] {exception.Error.Message ?? exception.Error.ShortMessage ?? exception.HttpResponse.ReasonPhrase}");
        }

        private void Log(string data)
        {
            Console.WriteLine(data);
        }

    }
}
