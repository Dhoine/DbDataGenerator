using System;

namespace DbDataGenerator.Models
{
    public class DamageCompensationHistoryItem
    {
        public int Id { get; set; }
        public DamageCompensationReason Reason { get; set; }
        public decimal Price { get; set; }
        public DateTime Date { get; set; }
    }
}