namespace DbDataGenerator.Models
{
    public class PersonalData
    {
        public int Index { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DocumentType IdType { get; set; }
        public string IdNumber { get; set; }
    }
}