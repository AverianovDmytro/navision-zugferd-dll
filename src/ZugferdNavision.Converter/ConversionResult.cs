using System.Runtime.InteropServices;

namespace ZugferdNavision
{
    [ComVisible(true)]
    [Guid("5D6258F6-7C4B-4F62-9D7A-4143C1D5211A")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class ConversionResult
    {
        public string OutputPath { get; set; }
        public string XmlValidationErrors { get; set; }
        public string PdfValidationErrors { get; set; }
        public string PdfA3ValidationErrors { get; set; }
        public bool HasXmlErrors   => !string.IsNullOrEmpty(XmlValidationErrors);
        public bool HasPdfErrors   => !string.IsNullOrEmpty(PdfValidationErrors);
        public bool HasPdfA3Errors => !string.IsNullOrEmpty(PdfA3ValidationErrors);
    }
}
