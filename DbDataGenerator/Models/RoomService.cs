using System;
using System.Collections.Generic;

namespace DbDataGenerator.Models
{
    public class RoomService
    {
        public Employee ServiceEmployee { get; set; }
        public Room RoomToService { get; set; }
        public List<DayOfWeek> DaysToService { get; set; }
    }
}