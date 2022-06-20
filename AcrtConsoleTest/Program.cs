// See https://aka.ms/new-console-template for more information
using System.Collections.Generic;
using AcrtConsoleTest;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Newtonsoft.Json;

Console.WriteLine("Hello, World!");


// Word file process
string file = @"/Users/juancolo/Projects/AcrtVerify/templafy.docx";

WordprocessingDocument wordDoc = WordprocessingDocument.Open(file, false);

if (wordDoc == null)
{
    return;
}

var pageList = new List<TemplafyDto>();
var errorList = new List<ErrorDto>();

var docs = wordDoc.MainDocumentPart?.Document.Body?.Descendants<Paragraph>().ToList();

for (var i = 2; i < docs.Count; i = i + 3)
{
    try
    {
        Console.WriteLine($"[File process]: {i} / {docs.Count}");
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

IList<AcrtDto> acrtResult = null;
// Get ACRT agenda builder data
using(var agendaClient = new HttpClient())
{
    var response = await agendaClient.GetAsync("https://pwc-acrt-api.azurewebsites.net/agenda-builder/all");
    acrtResult = JsonConvert.DeserializeObject<IList<AcrtDto>>(await response.Content.ReadAsStringAsync());
}


// Dpe process
var count = 0;
var handler = new HttpClientHandler()
{
    AllowAutoRedirect = false
};


foreach (var page in pageList)
{
    var acrtValue = acrtResult?.FirstOrDefault(item => item.href == page.Url);
    var error = !acrtValue.title.Equals(page.Title) || !acrtValue.text.Equals(page.Description);

    if (!error)
        continue;


    var httpClient = new HttpClient(handler);
    var response = await httpClient.GetAsync(page.Url);

    if(!string.IsNullOrWhiteSpace(response.Headers.Location?.AbsoluteUri))
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


    if(error)
    {
        var errorDto = new ErrorDto
        {
            DpePage = new TemplafyDto { Title = title, Description = wdescription },
            TemplafyValue = page,
            AcrtValue = new TemplafyDto { Title = acrtValue.title, Description = acrtValue.text }
        };

        errorList.Add(errorDto);
    }

    Console.WriteLine($"[DPE page process]: {count++} / {pageList.Count}");
}

var json = JsonConvert.SerializeObject(errorList);
File.WriteAllText("templafy-errors.json", json);



Console.ReadLine();
