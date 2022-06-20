using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Packaging;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Linq;
using System.Net.Http;
using HtmlAgilityPack;

namespace AcrtVerify
{
    public static class AcrtVerify
    {
        [FunctionName("AcrtVerify")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            //log.LogInformation("C# HTTP trigger function processed a request.");

            //string name = req.Query["name"];

            //string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //name = name ?? data?.name;

            //string responseMessage = string.IsNullOrEmpty(name)
            //    ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            //    : $"Hello, {name}. This HTTP triggered function executed successfully.";

            //return new OkObjectResult(responseMessage);

            //string file = @"/Users/juancolo/Projects/AcrtVerify/templafy.docx";

            var templafyData = ParseFile(req);

            if (templafyData == null)
            {
                log.LogError("Bad file");
                return new BadRequestObjectResult("Bad File");
            }

            var acrtData = await GetAcrtData();


            // Dpe process
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };

            var errorList = new List<ErrorDto>();

            foreach (var page in templafyData)
            {
                var acrtValue = acrtData?.FirstOrDefault(item => item.href == page.Url);
                var error = !acrtValue.title.Equals(page.Title) || !acrtValue.text.Equals(page.Description);

                if (!error)
                    continue;


                var httpClient = new HttpClient(handler);
                var response = await httpClient.GetAsync(page.Url);

                if (!string.IsNullOrWhiteSpace(response.Headers.Location?.AbsoluteUri))
                {
                    continue;
                }
                var web = new HtmlWeb();
                var doc = web.Load(page.Url);
                var wtitle = doc.DocumentNode.SelectSingleNode("/html/head/meta[@name='pwcTitle']/@content")?.Attributes["content"]?.Value;
                var wogTitle = doc.DocumentNode.SelectSingleNode("/html/head/meta[@name='og:title']/@content")?.Attributes["content"]?.Value;
                var wdescription = doc.DocumentNode.SelectSingleNode("/html/head/meta[@name='description']/@content")?.Attributes["content"]?.Value;
                var title = !string.IsNullOrWhiteSpace(wtitle) ? wtitle : wogTitle;




                //var error = (string.IsNullOrWhiteSpace(title) || !title.Equals(page.Title) || !acrtValue.title.Equals(page.Title))
                //    || (string.IsNullOrWhiteSpace(wdescription) || !wdescription.Equals(page.Description) || !acrtValue.text.Equals(page.Description));


                if (error)
                {
                    var errorDto = new ErrorDto
                    {
                        DpePage = new TemplafyDto { Title = title, Description = wdescription },
                        TemplafyValue = page,
                        AcrtValue = new TemplafyDto { Title = acrtValue.title, Description = acrtValue.text }
                    };

                    errorList.Add(errorDto);
                }
            }

            //var json = JsonConvert.SerializeObject(errorList);
            //File.WriteAllText("templafy-errors.json", json);



            return new OkObjectResult(errorList);
        }


        public static List<TemplafyDto> ParseFile(HttpRequest req)
        {
            var file = new StreamReader(req.Body);

            WordprocessingDocument wordDoc = WordprocessingDocument.Open(file.BaseStream, false);

            if (wordDoc == null)
            {
                return null;
            }

            var pageList = new List<TemplafyDto>();
            var errorList = new List<ErrorDto>();

            var docs = wordDoc.MainDocumentPart?.Document.Body?.Descendants<Paragraph>().ToList();

            for (var i = 2; i < docs.Count; i = i + 3)
            {
                try
                {
                    var title = docs[i - 2];
                    var description = docs[i - 1];
                    var url = docs[i];
                    pageList.Add(new TemplafyDto
                    {
                        Title = title.InnerText,
                        Description = description.InnerText,
                        Url = url.InnerText.Trim()
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return pageList;
        }

        public static async Task<IList<AcrtDto>> GetAcrtData()
        {

            // Get ACRT agenda builder data
            using (var agendaClient = new HttpClient())
            {
                var response = await agendaClient.GetAsync("https://pwc-acrt-api.azurewebsites.net/agenda-builder/all");
                var acrtResult = JsonConvert.DeserializeObject<IList<AcrtDto>>(await response.Content.ReadAsStringAsync());
                return acrtResult;
            }
        }
    }
}

