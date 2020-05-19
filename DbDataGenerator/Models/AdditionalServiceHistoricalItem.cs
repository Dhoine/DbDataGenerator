using System;

namespace DbDataGenerator.Models
{
    public class AdditionalServiceHistoricalItem
    {
        public int BookingId { get; set; }
        public int Id { get; set; }
        public AdditionalServiceType Type { get; set; }
        public decimal HistoricalPrice { get; set; }
        public Employee Employee { get; set; }
        public DateTime Date { get; set; }
    }
}