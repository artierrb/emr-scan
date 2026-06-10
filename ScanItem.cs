namespace EMRScan
{
    public class ScanItem
    {
        public string ImagePath  { get; set; }   // temp JPG path
        public string OcrPk      { get; set; }   // extracted from image
        public string Hn         { get; set; }   // from DB
        public string FormCode   { get; set; }   // from DB
        public string Status     { get; set; }   // "Approve" / "Not Found" / "Skipped"
        public bool   IsEditing  { get; set; }   // user editing OcrPk
        public string TreatNo    { get; set; }   // from DB via CHARTPAGET
    }
}
