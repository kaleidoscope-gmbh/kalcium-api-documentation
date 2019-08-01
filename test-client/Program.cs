using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kaleidoscope.Kalcium.Client;
using Kaleidoscope.Kalcium.Client.Models;
using Kaleidoscope.Kalcium.Client.Models.AccountManagement;
using Kaleidoscope.Kalcium.Client.Models.Terminology.Entries;
using Kaleidoscope.Kalcium.Client.Models.Terminology.Tasks.TermRequest;
using Kaleidoscope.Kalcium.Client.Models.Terminology.Termbases;
using Kaleidoscope.Kalcium.Client.Services;
using Newtonsoft.Json;

namespace Kaleidoscope.Kalcium.TestClient
{
    class Program
    {

        private static KalcClient kalcClient;

        private static Dictionary<int, Termbase> myTermbases;
        private static Dictionary<int, SchemaDefinition> myTermbaseDefinitions;

        static void Main(string[] args)
        {
            Task.Run(RunTests).Wait();

        }

        static async Task RunTests()
        {
            try
            {
                Console.WriteLine("Tests started");

                var serverUrl = "http://localhost:42000";
                kalcClient = new KalcClient(serverUrl);
                //This setting is used only for testing purposes. Use the appropriate NuGet package of Kaleidoscope.Kalcium.Client according to the server you want to connect
                kalcClient.KalcHttp.IgnoreKalcVersion = true;

                await Login();

                myTermbases = await QueryTermbases();
                await Search();

                myTermbaseDefinitions = await QueryTermbaseSchemaDefinitions();
                var entry = await CreateEntry();
                await DeleteEntry(entry.Id.UUID, entry.TermbaseId);

                var termRequest = await CreateTermRequest();
                await DeleteTermRequest(termRequest);


                await Logout();

                Console.WriteLine("Tests have been finished");

            }
            catch (KalcHttpException exception)
            {
                Console.WriteLine($"ERROR [{exception.StatusCode}] {exception.Error.Message ?? exception.Error.ShortMessage ?? exception.HttpResponse.ReasonPhrase}");
            }

            Console.ReadLine();
        }

        static async Task Login()
        {
            Console.WriteLine($"Connecting to Kalcium REST API on address {kalcClient.KalcHttp.BackendUrl}");
            string userName = "joker";
            string password = "joker";
            await kalcClient.Login(userName, password);
            Console.WriteLine($"Successfully logged in with user {userName}");
        }

        static async Task<Dictionary<int, Termbase>> QueryTermbases()
        {
            Console.WriteLine("Querying available termbases");
            var myTermbaseIds = kalcClient.AuthenticationData.Groups
                .SelectMany(group => group.Termbases.Where(gtb => gtb.IsEnabled.Value).Select(gtb => gtb.TermbaseId)).Distinct().ToList();

            var termbases = await kalcClient.Terminology.GetTermbasesAsync(myTermbaseIds);
            foreach (var tb in termbases)
            {
                Console.WriteLine($" > Termbase '{tb.Name}' [#{tb.Id}] found");
            }
            return termbases.ToDictionary(tb => tb.Id, tb => tb);
        }

        static async Task<Dictionary<int, SchemaDefinition>> QueryTermbaseSchemaDefinitions()
        {
            Console.WriteLine("Querying termbase schema definitions");
            var myTermbaseIds = kalcClient.AuthenticationData.Groups
                .SelectMany(group => group.Termbases.Where(gtb => gtb.IsEnabled.Value).Select(gtb => gtb.TermbaseId)).Distinct().ToList();
            var termbaseSchemaDefinitions = await kalcClient.Terminology.GetTermbaseDefinitionsAsync(myTermbaseIds);
            foreach (var tbdef in termbaseSchemaDefinitions)
            {
                Console.WriteLine($" > Definition for termbase '{tbdef.TermbaseName}' [#{tbdef.TermbaseId}] found");
            }
            return termbaseSchemaDefinitions.ToDictionary(tbdef => tbdef.TermbaseId, tbdef => tbdef);
        }

        static async Task Search()
        {
            var termbaseName = "Kalcium";
            var termbase = myTermbases.Values.First(tb => tb.Name == termbaseName);

            var term = "";
            Console.WriteLine($"Test searching for '{term}'");
            //for high performance search, use kalcClient.Search.SearchRawAsync instead
            var results = await kalcClient.Search.SearchAsync("",
                SearchMode.Prefix,
                null,
                false,
                0, // start index
                20, // max number of hits to search for
                termbase.LanguageIds, // source language ids - where the term is searched for
                termbase.LanguageIds, // target language ids - languages to include in the results
                new List<UserSearchTermbaseSettings>
                {
                    new UserSearchTermbaseSettings
                    {
                        TermbaseId = termbase.Id // termbases to search for
                    }
                }, null,
                null,
                null,
                null);
            Console.WriteLine($" > Total number of matches: {results.Total}");
            Console.WriteLine($" > Number of hits returned: {results.Hits.Count}");
            Console.WriteLine($" > Number of related entries: {results.Entries.Count}");
        }

        static async Task DeleteEntry(string entryUUID, int termbaseId)
        {
            Console.WriteLine($"Deleting entry {entryUUID}");
            await kalcClient.Terminology.DeleteEntryAsync(entryUUID, termbaseId);
            Console.WriteLine($" > Entry {entryUUID} has been deleted");
        }

        static async Task<Entry> CreateEntry()
        {
            Console.WriteLine("Test creating entry");
            var termbaseName = "Kalcium";
            var termbase = myTermbases.Values.First(tb => tb.Name == termbaseName);
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
            Console.WriteLine($" > Adding term to language {tbDef.LanguageGroupDefinitions.First().LanguageName} (#{tbDef.LanguageGroupDefinitions.First().LanguageId}): '${ee.Languages[0].Terms[0].Term}'");
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
                Console.WriteLine($" > Adding entry level text field, field name: ${textFieldDef.Name}; value: ${ee.Fields[0].Value}");
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
                Console.WriteLine($" > Adding media field {ee.Fields.Last().Name}: {ee.Fields.Last().Value}");
            }
            var createdEntry = await kalcClient.Terminology.CreateEntryAsync(ee, termbase.Id, mediaFiles);
            Console.WriteLine($" > Entry created with id #{createdEntry.Id.UUID}");
            Console.WriteLine(JsonConvert.SerializeObject(createdEntry, Formatting.Indented));

            //test reading entry
            var queryEntryResult = await kalcClient.Terminology.GetEntriesByUUIDAsync(tbDef.TermbaseId, new string[] { createdEntry.Id.UUID },
                termbase.LanguageIds, false, false, true, false);
            var queriedEntry = queryEntryResult.Entries[0];
            if (queriedEntry != null)
            {
                Console.WriteLine($" > Entry #{createdEntry.Id.UUID} has been queried again");
                Console.WriteLine(JsonConvert.SerializeObject(queriedEntry, Formatting.Indented));
            }

            if (imageFieldDef != null)
            {
                var streamResult = await kalcClient.Terminology.GetMediaFileAsync(tbDef.TermbaseId, queriedEntry.Fields.Last().Value, false,
                    null, queriedEntry.Id.UUID, null);
                var bytes = await streamResult.ReadAsByteArrayAsync();
                var tempFile = Path.GetTempPath() + Guid.NewGuid().ToString() + Path.GetExtension(queriedEntry.Fields.Last().Value);
                File.WriteAllBytes(tempFile, bytes);
                Console.WriteLine($" >> Image attachment has been downloaded to {tempFile}");
            }

            return queriedEntry;
        }

        static async Task DeleteTermRequest(TermRequest termRequest)
        {
            Console.WriteLine($"Deleting term request {termRequest.Id}");
            await kalcClient.BaseTasks.DeleteAsync(new[] { termRequest.Id });
            Console.WriteLine($" > Term request {termRequest.Id} has been deleted");
        }

        static async Task<TermRequest> CreateTermRequest()
        {
            Console.WriteLine("Test creating entry");
            var termbaseName = "Kalcium";
            var termbase = myTermbases.Values.First(tb => tb.Name == termbaseName);
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
            var newTermRequestId = await kalcClient.TermRequests.CreateTermRequestAsync(createTermRequestModel, null, null);

            return (await kalcClient.TermRequests.GetTasksByIdAsync(new[] { newTermRequestId }, false))[0];
        }



        static async Task Logout()
        {
            await kalcClient.Logout();
            Console.WriteLine("Successfully logged out");
        }


    }
}
