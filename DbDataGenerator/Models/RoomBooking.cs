using System;
using System.Collections.Generic;

namespace DbDataGenerator.Models
{
    public class RoomBooking
    {
        public int Id { get; set; }
        public PersonalData Client { get; set; }
        public Room BookedRoom { get; set; }
        public List<AdditionalServiceHistoricalItem> AdditionalService { get; set; }
        public List<DamageCompensationHistoryItem> DamageCompensation { get; set; }
        public Employee EmployeeWhoRegistered { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
    }
}