using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ZugferdNavision
{
    [ComVisible(true)]
    [Guid("66D83865-A815-47C5-8376-568369E8B428")]
    [ProgId("ZugferdNavision.ZugferdConverter")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class ZugferdConverter
    {
        private readonly HttpMessageHandler _handler;

        public int TimeoutSeconds { get; set; } = 60;

        public ZugferdConverter() { }

        internal ZugferdConverter(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        private static readonly string[] ValidProfiles =
            { "MINIMUM", "BASIC WL", "BASIC", "EN16931", "EXTENDED", "XRECHNUNG" };

        public ConversionResult ConvertToZugferd(
            string apiUrl,
            string pdfFilePath,
            string xmlFilePath,
            string profile         = "BASIC",
            string apiKey          = null,
            string outputDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentNullException("apiUrl", "API URL must not be empty.");

            if (string.IsNullOrWhiteSpace(profile))
                profile = "BASIC";
            else if (Array.IndexOf(ValidProfiles, profile.ToUpperInvariant()) < 0)
                throw new ArgumentException(
                    string.Format("Unknown profile '{0}'. Valid values: {1}",
                        profile, string.Join(", ", ValidProfiles)), "profile");

            if (!File.Exists(pdfFilePath))
                throw new FileNotFoundException("PDF file not found", pdfFilePath);
            if (!File.Exists(xmlFilePath))
                throw new FileNotFoundException("XML file not found", xmlFilePath);

            byte[] pdfBytes = File.ReadAllBytes(pdfFilePath);
            byte[] xmlBytes = File.ReadAllBytes(xmlFilePath);

            using (var client = CreateClient(apiKey))
            using (var content = new MultipartFormDataContent())
            {
                content.Add(new ByteArrayContent(pdfBytes), "file",    Path.GetFileName(pdfFilePath));
                content.Add(new ByteArrayContent(xmlBytes), "xmlFile", Path.GetFileName(xmlFilePath));
                content.Add(new StringContent(profile),    "profile");

                HttpResponseMessage response;
                try
                {
                    response = client.PostAsync(apiUrl, content).GetAwaiter().GetResult();
                }
                catch (AggregateException ae) when (ae.InnerException is TaskCanceledException)
                {
                    throw new TimeoutException(
                        string.Format("Request to {0} timed out after {1} seconds.",
                            apiUrl, TimeoutSeconds));
                }

                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content
                        .ReadAsStringAsync().GetAwaiter().GetResult();
                    throw new Exception(
                        string.Format("API error {0} calling {1}: {2}",
                            (int)response.StatusCode, apiUrl, error));
                }

                byte[] resultPdf = response.Content
                    .ReadAsByteArrayAsync().GetAwaiter().GetResult();

                string outDir = !string.IsNullOrEmpty(outputDirectory)
                    ? outputDirectory
                    : Path.GetTempPath();

                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                string outputPath = Path.Combine(
                    outDir,
                    string.Format("{0}_zugferd_{1:N}.pdf",
                        Path.GetFileNameWithoutExtension(pdfFilePath), Guid.NewGuid())
                );
                File.WriteAllBytes(outputPath, resultPdf);

                return new ConversionResult
                {
                    OutputPath            = outputPath,
                    XmlValidationErrors   = GetHeader(response, "X-XML-Validation-Errors"),
                    PdfValidationErrors   = GetHeader(response, "X-PDF-Validation-Errors"),
                    PdfA3ValidationErrors = GetHeader(response, "X-PDFA3-Validation-Errors")
                };
            }
        }

        private HttpClient CreateClient(string apiKey)
        {
            var client = _handler != null
                ? new HttpClient(_handler, disposeHandler: false)
                : new HttpClient();

            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);

            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            return client;
        }

        private static string GetHeader(HttpResponseMessage response, string name)
        {
            IEnumerable<string> values;
            return response.Headers.TryGetValues(name, out values)
                ? string.Join("", values)
                : null;
        }
    }
}
