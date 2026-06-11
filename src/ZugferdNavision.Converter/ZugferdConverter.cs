using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace ZugferdNavision
{
    [ComVisible(true)]
    [Guid("66D83865-A815-47C5-8376-568369E8B428")]
    public class ZugferdConverter
    {
        private readonly HttpMessageHandler _handler;

        public ZugferdConverter() { }

        // Internal constructor used by unit tests to inject a mock handler
        internal ZugferdConverter(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public ConversionResult ConvertToZugferd(
            string apiUrl,
            string pdfFilePath,
            string xmlFilePath,
            string profile = "BASIC",
            string apiKey  = null)
        {
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

                HttpResponseMessage response = client
                    .PostAsync(apiUrl, content)
                    .GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    string error = response.Content
                        .ReadAsStringAsync().GetAwaiter().GetResult();
                    throw new Exception(
                        $"API error {(int)response.StatusCode}: {error}");
                }

                byte[] resultPdf = response.Content
                    .ReadAsByteArrayAsync().GetAwaiter().GetResult();

                string outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"{Path.GetFileNameWithoutExtension(pdfFilePath)}_zugferd_{Guid.NewGuid():N}.pdf"
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

            client.Timeout = TimeSpan.FromSeconds(60);

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
