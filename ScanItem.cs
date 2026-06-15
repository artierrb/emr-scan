namespace EMRScan
{
    public class ScanItem
    {
        public string ImagePath { get; set; } = "";
        public string OcrPk     { get; set; } = "";
        public string Hn        { get; set; } = "";
        public string FormCode  { get; set; } = "";
        public string Status    { get; set; } = "";
        public string TreatNo   { get; set; } = "";
        public int    PageSeq   { get; set; } = 1;   // which page this image is (1-based)
        public int    PageCount { get; set; } = 1;   // total pages for this form
    }
}
