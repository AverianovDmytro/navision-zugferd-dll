using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZugferdNavision;

namespace ZugferdNavision.Tests
{
    [TestClass]
    public class ZugferdConverterTests
    {
        private static string _tempPdf;
        private static string _tempXml;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            _tempPdf = Path.GetTempFileName() + ".pdf";
            _tempXml = Path.GetTempFileName() + ".xml";
            File.WriteAllBytes(_tempPdf, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
            File.WriteAllText(_tempXml, "<Invoice />");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (File.Exists(_tempPdf)) File.Delete(_tempPdf);
            if (File.Exists(_tempXml)) File.Delete(_tempXml);
        }

        // --- 4.1 PDF file not found ---
        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void ConvertToZugferd_PdfNotFound_ThrowsFileNotFoundException()
        {
            var converter = new ZugferdConverter();
            converter.ConvertToZugferd("http://localhost", "nonexistent.pdf", _tempXml);
        }

        // --- 4.2 XML file not found ---
        [TestMethod]
        [ExpectedException(typeof(FileNotFoundException))]
        public void ConvertToZugferd_XmlNotFound_ThrowsFileNotFoundException()
        {
            var converter = new ZugferdConverter();
            converter.ConvertToZugferd("http://localhost", _tempPdf, "nonexistent.xml");
        }

        // --- 4.3 API returns 400 ---
        [TestMethod]
        public void ConvertToZugferd_Api400_ThrowsExceptionWithStatusCode()
        {
            var handler = new MockHttpHandler(HttpStatusCode.BadRequest, "bad request body");
            var converter = new ZugferdConverter(handler);
            var ex = Assert.ThrowsException<Exception>(
                () => converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml));
            StringAssert.Contains(ex.Message, "400");
        }

        // --- 4.4 API returns 429 ---
        [TestMethod]
        public void ConvertToZugferd_Api429_ThrowsException()
        {
            var handler = new MockHttpHandler((HttpStatusCode)429, "rate limit");
            var converter = new ZugferdConverter(handler);
            var ex = Assert.ThrowsException<Exception>(
                () => converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml));
            StringAssert.Contains(ex.Message, "429");
        }

        // --- 4.5 API returns 500 ---
        [TestMethod]
        public void ConvertToZugferd_Api500_ThrowsException()
        {
            var handler = new MockHttpHandler(HttpStatusCode.InternalServerError, "server error");
            var converter = new ZugferdConverter(handler);
            var ex = Assert.ThrowsException<Exception>(
                () => converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml));
            StringAssert.Contains(ex.Message, "500");
        }

        // --- 4.6 200 OK, no validation headers ---
        [TestMethod]
        public void ConvertToZugferd_Success_NoValidationHeaders_OutputFileExistsAndErrorsNull()
        {
            byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
            var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, contentType: "application/pdf");
            var converter = new ZugferdConverter(handler);

            ConversionResult result = converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml);

            Assert.IsNotNull(result.OutputPath);
            Assert.IsTrue(File.Exists(result.OutputPath), "Output file should exist on disk");
            Assert.IsNull(result.XmlValidationErrors);
            Assert.IsNull(result.PdfValidationErrors);
            Assert.IsNull(result.PdfA3ValidationErrors);
            Assert.IsFalse(result.HasXmlErrors);
            Assert.IsFalse(result.HasPdfErrors);
            Assert.IsFalse(result.HasPdfA3Errors);

            File.Delete(result.OutputPath);
        }

        // --- 4.7 200 OK, all three validation headers present ---
        [TestMethod]
        public void ConvertToZugferd_Success_AllValidationHeadersPresent_ErrorFieldsNonNull()
        {
            byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
            var headers = new (string, string)[]
            {
                ("X-XML-Validation-Errors",   "[{\"message\":\"xml error\"}]"),
                ("X-PDF-Validation-Errors",   "[{\"message\":\"pdf error\"}]"),
                ("X-PDFA3-Validation-Errors", "[{\"message\":\"pdfa3 error\"}]")
            };
            var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, contentType: "application/pdf", extraHeaders: headers);
            var converter = new ZugferdConverter(handler);

            ConversionResult result = converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml);

            Assert.IsNotNull(result.XmlValidationErrors);
            Assert.IsNotNull(result.PdfValidationErrors);
            Assert.IsNotNull(result.PdfA3ValidationErrors);
            Assert.IsTrue(result.HasXmlErrors);
            Assert.IsTrue(result.HasPdfErrors);
            Assert.IsTrue(result.HasPdfA3Errors);
            StringAssert.Contains(result.XmlValidationErrors,   "xml error");
            StringAssert.Contains(result.PdfValidationErrors,   "pdf error");
            StringAssert.Contains(result.PdfA3ValidationErrors, "pdfa3 error");

            if (File.Exists(result.OutputPath)) File.Delete(result.OutputPath);
        }

        // --- 4.8 Concurrent calls produce different output paths ---
        [TestMethod]
        public void ConvertToZugferd_ConcurrentCalls_OutputPathsAreUnique()
        {
            byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
            string path1 = null, path2 = null;

            var t1 = Task.Run(() =>
            {
                var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, contentType: "application/pdf");
                var converter = new ZugferdConverter(handler);
                path1 = converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml).OutputPath;
            });
            var t2 = Task.Run(() =>
            {
                var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, contentType: "application/pdf");
                var converter = new ZugferdConverter(handler);
                path2 = converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml).OutputPath;
            });
            Task.WaitAll(t1, t2);

            Assert.AreNotEqual(path1, path2, "Concurrent calls must produce unique output paths");

            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }

        // --- 5.1 null apiUrl throws ArgumentNullException ---
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ConvertToZugferd_NullApiUrl_ThrowsArgumentNullException()
        {
            var converter = new ZugferdConverter();
            converter.ConvertToZugferd(null, _tempPdf, _tempXml);
        }

        // --- 5.2 invalid profile throws ArgumentException ---
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ConvertToZugferd_InvalidProfile_ThrowsArgumentException()
        {
            byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
            var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, "application/pdf");
            var converter = new ZugferdConverter(handler);
            converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml, profile: "INVALID");
        }

        // --- 5.3 custom outputDirectory (auto-created) ---
        [TestMethod]
        public void ConvertToZugferd_CustomOutputDirectory_FileWrittenThere()
        {
            string customDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
            var handler = new MockHttpHandler(HttpStatusCode.OK, fakePdf, "application/pdf");
            var converter = new ZugferdConverter(handler);

            ConversionResult result = converter.ConvertToZugferd(
                "http://localhost/convert", _tempPdf, _tempXml, outputDirectory: customDir);

            Assert.IsTrue(result.OutputPath.StartsWith(customDir),
                "OutputPath should be inside the custom directory");
            Assert.IsTrue(File.Exists(result.OutputPath), "Output file should exist on disk");

            Directory.Delete(customDir, true);
        }

        // --- 5.4 X-Api-Key header is sent when apiKey is provided ---
        [TestMethod]
        public void ConvertToZugferd_WithApiKey_SendsApiKeyHeader()
        {
            byte[] fakePdf = Encoding.UTF8.GetBytes("%PDF-1.4 fake");
            var handler = new InspectableHttpHandler(HttpStatusCode.OK, fakePdf, "application/pdf");
            var converter = new ZugferdConverter(handler);

            converter.ConvertToZugferd("http://localhost/convert", _tempPdf, _tempXml, apiKey: "my-secret");

            Assert.IsNotNull(handler.LastRequest, "Handler should have received a request");
            Assert.IsTrue(handler.LastRequest.Headers.Contains("X-Api-Key"),
                "Request should contain X-Api-Key header");
            var values = new System.Collections.Generic.List<string>(
                handler.LastRequest.Headers.GetValues("X-Api-Key"));
            Assert.AreEqual("my-secret", values[0]);
        }

        // --- 5.5 default TimeoutSeconds is 60 ---
        [TestMethod]
        public void ZugferdConverter_DefaultTimeoutSeconds_Is60()
        {
            var converter = new ZugferdConverter();
            Assert.AreEqual(60, converter.TimeoutSeconds);
        }

        // --- 4.9 ConversionResult bool properties ---
        [TestMethod]
        public void ConversionResult_BoolProperties_ReflectStringValues()
        {
            var r = new ConversionResult();
            Assert.IsFalse(r.HasXmlErrors);
            Assert.IsFalse(r.HasPdfErrors);
            Assert.IsFalse(r.HasPdfA3Errors);

            r.XmlValidationErrors   = "[{}]";
            r.PdfValidationErrors   = "[{}]";
            r.PdfA3ValidationErrors = "[{}]";
            Assert.IsTrue(r.HasXmlErrors);
            Assert.IsTrue(r.HasPdfErrors);
            Assert.IsTrue(r.HasPdfA3Errors);

            r.XmlValidationErrors   = null;
            r.PdfValidationErrors   = "";
            r.PdfA3ValidationErrors = null;
            Assert.IsFalse(r.HasXmlErrors);
            Assert.IsFalse(r.HasPdfErrors);
            Assert.IsFalse(r.HasPdfA3Errors);
        }
    }

    // ---------------------------------------------------------------------------
    // Mock HTTP handler — no third-party dependency required
    // ---------------------------------------------------------------------------
    internal class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _body;
        private readonly string _contentType;
        private readonly (string Name, string Value)[] _extraHeaders;

        public MockHttpHandler(
            HttpStatusCode statusCode,
            string body,
            string contentType = "application/json",
            (string, string)[] extraHeaders = null)
            : this(statusCode, Encoding.UTF8.GetBytes(body ?? ""), contentType, extraHeaders) { }

        public MockHttpHandler(
            HttpStatusCode statusCode,
            byte[] body,
            string contentType = "application/octet-stream",
            (string Name, string Value)[] extraHeaders = null)
        {
            _statusCode   = statusCode;
            _body         = body ?? Array.Empty<byte>();
            _contentType  = contentType;
            _extraHeaders = extraHeaders;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_body)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);

            if (_extraHeaders != null)
                foreach (var (name, value) in _extraHeaders)
                    response.Headers.TryAddWithoutValidation(name, value);

            return Task.FromResult(response);
        }
    }

    internal class InspectableHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage LastRequest { get; private set; }

        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _body;
        private readonly string _contentType;

        public InspectableHttpHandler(HttpStatusCode statusCode, byte[] body, string contentType)
        {
            _statusCode  = statusCode;
            _body        = body ?? Array.Empty<byte>();
            _contentType = contentType;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new ByteArrayContent(_body)
            };
            response.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(_contentType);
            return Task.FromResult(response);
        }
    }
}
