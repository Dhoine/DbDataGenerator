using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;
using Bogus;
using Bogus.Extensions;
using DbDataGenerator.Models;
using Neo4j.Driver;
using Newtonsoft.Json;

namespace DbDataGenerator
{
    class Program
    {
        

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Randomizer.Seed = new Random((int)DateTime.Now.TimeOfDay.Ticks);
            var personalData = GeneratePersonalData();
            var employees = personalData.Take(20);
            var employeesObjects = GenerateEmployees(employees.ToList());
            var rooms = GenerateRooms();
            var clients = personalData.Skip(20).ToList();
            var roomService = GenerateRoomService(employeesObjects, rooms);
            var bookings = GenerateBookings(employeesObjects, rooms, clients);
            using var helper = new DbHelper();
            helper.ClearAllData();
            helper.AddIdTypes();
            helper.AddPersonalData(personalData);
            helper.AddEmployees(employeesObjects);
            helper.AddRooms(rooms);
            helper.AddDamageCompensationReason();
            helper.AddBooking(bookings);
        }

        #region MyRegion

        private static List<PersonalData> GeneratePersonalData()
        {
            var personalData = new Faker<PersonalData>()
                .CustomInstantiator(f => new PersonalData())
                .RuleFor(u => u.Index, f => f.UniqueIndex)
                .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                .RuleFor(u => u.LastName, f => f.Name.LastName())
                .RuleFor(u => u.IdType, f => f.PickRandom<DocumentType>())
                .RuleFor(u => u.IdNumber, f => $"{f.Random.Guid()}");
            return personalData.Generate(300);
        }

        private static List<Employee> GenerateEmployees(List<PersonalData> personalData)
        {
            var employees = new Faker<Employee>()
                .CustomInstantiator(f => new Employee())
                .RuleFor(u => u.EmployeeIdNumber, f => f.UniqueIndex)
                .RuleFor(u => u.Position, (f, u) => f.PickRandom<EmployeePosition>());
            return employees.Generate(personalData.Count);
        }

        private static List<Room> GenerateRooms()
        {
            var rooms = new Faker<Room>()
                .CustomInstantiator(f => new Room())
                .RuleFor(u => u.RoomNumber, f => f.IndexFaker)
                .RuleFor(u => u.Area, f => f.Random.Number(20, 80))
                .RuleFor(u => u.CostForDay, f => f.Random.Decimal(100, 500));
            return rooms.Generate(60);
        }

        private static List<RoomService> GenerateRoomService(List<Employee> employees, List<Room> Rooms)
        {
            var maids = employees.Where(e => e.Position == EmployeePosition.Сhambermaid);
            var service = new Faker<RoomService>()
                .CustomInstantiator(f => new RoomService())
                .RuleFor(u => u.ServiceEmployee, f => f.PickRandom(maids))
                .RuleFor(u => u.RoomToService, f => f.PickRandom(Rooms))
                .RuleFor(u => u.DaysToService, f =>
                {
                    var alreadyChoosen = new List<DayOfWeek>();
                    return f.Make(3, () =>
                    {
                        var item = f.PickRandomWithout<DayOfWeek>(alreadyChoosen.ToArray());
                        alreadyChoosen.Add(item);
                        return item;
                    });
                });
            return service.Generate(60);
        }

        private static List<RoomBooking> GenerateBookings(List<Employee> employees, List<Room> rooms,
            List<PersonalData> clients)
        {
            var alreadyBooked = new List<BookedRoom>();
            var administrators = employees.Where(e => e.Position == EmployeePosition.Administrator);
            var bookings = new Faker<RoomBooking>().CustomInstantiator(f => new RoomBooking())
                .RuleFor(u => u.Id, f=>f.UniqueIndex)
                .RuleFor(u => u.Client, f => f.PickRandom(clients))
                .RuleFor(u => u.EmployeeWhoRegistered, f => f.PickRandom(administrators))
                .RuleFor(u => u.BookedRoom, f => f.PickRandom(rooms))
                .RuleFor(u => u.DateTo, (f, u) =>
                {
                    var prevMinDate = alreadyBooked.Any(a => a.room == u.BookedRoom)
                        ? alreadyBooked.Where(a => a.room == u.BookedRoom).Min(d => d.DateFrom)
                        : DateTime.Now;
                    var date = f.Date.Past(1, prevMinDate);
                    return date;
                })
                .RuleFor(u => u.DateFrom, (f, u) =>
                {
                    var date = f.Date.Past(1, u.DateTo);
                    alreadyBooked.Add(new BookedRoom{DateFrom = date, room = u.BookedRoom});
                    return date;
                })
                .RuleFor(u => u.AdditionalService, (f, u) => new Faker<AdditionalServiceHistoricalItem>()
                    .RuleFor(a => a.Id, f=> f.UniqueIndex)
                    .RuleFor(a => a.Date, f => f.Date.Between(u.DateFrom, u.DateTo))
                    .RuleFor(a => a.Employee, f => f.PickRandom(employees))
                    .RuleFor(a => a.HistoricalPrice, f.Random.Decimal(20, 5000))
                    .RuleFor(a => a.Type, f => f.PickRandom<AdditionalServiceType>())
                    .GenerateBetween(2, 5))
                .RuleFor(u => u.DamageCompensation, (f, u) => new Faker<DamageCompensationHistoryItem>()
                    .RuleFor(a => a.Id, f => f.UniqueIndex)
                    .RuleFor(a => a.Date, f => f.Date.Between(u.DateFrom, u.DateTo))
                    .RuleFor(a => a.Price, f => f.Random.Decimal(20, 5000))
                    .RuleFor(a => a.Reason, f => f.PickRandom<DamageCompensationReason>())
                    .GenerateBetween(2, 5))
                .Generate(50);
            return bookings;
        }

        #endregion
    }

    class BookedRoom
    {
        public Room room { get; set; }
        public DateTime DateFrom { get; set; }
    }

    class DbHelper : IDisposable
    {
        private IDriver _driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.None);

        public void ClearAllData()
        {
            using var session = _driver.Session();
            foreach (DocumentType value in Enum.GetValues(typeof(DocumentType)))
            {

                var valStr = value.ToString();

                var res = session.Run(@"MATCH (n)
                                                    OPTIONAL MATCH (n)-[r]-()
                                                    DELETE n,r");
                res.Consume();

            }
        }

        public void AddIdTypes()
        {
            using var session = _driver.Session();
            foreach (DocumentType value in Enum.GetValues(typeof(DocumentType)))
            {

                var valStr = value.ToString();

                var res = session.Run("MERGE (a:DocumentType {Type : $valStr})", new { valStr });
                res.Consume();

            }
        }

        public void AddDamageCompensationReason()
        {
            using var session = _driver.Session();
            foreach (DamageCompensationReason value in Enum.GetValues(typeof(DamageCompensationReason)))
            {

                var valStr = value.ToString();

                var res = session.Run("MERGE (a:DamageCompensationReason {Reason : $valStr})", new { valStr });
                res.Consume();

            }
        }


        public void AddPersonalData(List<PersonalData> persons)
        {
            using var session = _driver.Session();
            foreach (var person in persons)
            {
                var IdType = person.IdType.ToString();
                session.Run(@"MERGE (a:PersonalData {id: $Index, FirstName: $FirstName, LastName: $LastName, IdNumber: $IdNumber, IdType: $IdType})",
                    new {person.Index, person.FirstName, person.LastName, person.IdNumber, IdType = person.IdType.ToString()});
            }
        }

        public void AddEmployees(List<Employee> employees)
        {
            using var session = _driver.Session();
            foreach (var employee in employees)
            {
                var Position = employee.Position.ToString();

                session.Run(@"MERGE (a:Employee {EmployeeIdNumber: $EmployeeIdNumber, Position: $Position})",
                    new { employee.EmployeeIdNumber, Position });
            }
        }

        public void AddRooms(List<Room> rooms)
        {
            using var session = _driver.Session();
            foreach (var room in rooms)
            {
                session.Run(@"MERGE (a:Room {RoomNumber: $RoomNumber, Area: $Area, CostForDay: $CostForDay})",
                    new { room.RoomNumber, room.Area, room.CostForDay});
            }
        }

        public void AddBooking(List<RoomBooking> bookings)
        {
            using var session = _driver.Session();
            foreach (var booking in bookings)
            {
                session.Run(@"MERGE (a:RoomBooking {Id: $Id})",
                    new { booking.Id });
                session.Run(@"MATCH (a:RoomBooking), (b:PersonalData)
                                    WHERE a.Id = $Id AND b.id=$PersonIndex
                                    MERGE (a)-[r:IS_BOOKED_BY]->(b)",
                    new {booking.Id, PersonIndex = booking.Client.Index });
                session.Run(@"MATCH (a:RoomBooking), (b:Employee)
                                    WHERE a.Id = $Id AND b.EmployeeIdNumber=$EmployeeIdNumber
                                    MERGE (a)-[r:IS_REGISTERED_BY]->(b)",
                    new { booking.Id, booking.EmployeeWhoRegistered.EmployeeIdNumber });
                session.Run(@"MATCH (a:RoomBooking), (b:Room)
                                    WHERE a.Id = $Id AND b.RoomNumber=$RoomNumber
                                    MERGE (a)-[r:ROOM_BOOKED {From: $DateFrom, To: $DateTo}]->(b)",
                    new { booking.Id, booking.BookedRoom.RoomNumber, booking.DateFrom, booking.DateTo});
                foreach (var addtnlService in booking.AdditionalService)
                {
                    session.Run(@"CREATE (a:AdditionalService {Id:$Id, Price:$Price, Type:$Type })",
                        new {addtnlService.Id, Price = addtnlService.HistoricalPrice, Type = addtnlService.Type.ToString()});
                    session.Run(@"MATCH (a:AdditionalService), (b:Employee)
                                    WHERE a.Id = $Id AND b.EmployeeIdNumber=$EmployeeIdNumber
                                    MERGE (a)-[r:IS_DONE_BY]->(b)",
                        new
                        {
                            addtnlService.Id, addtnlService.Employee.EmployeeIdNumber
                        });
                    session.Run(@"MATCH (a:RoomBooking), (b:AdditionalService)
                                    WHERE a.Id = $BookingId AND b.Id=$ServiceId
                                    MERGE (a)-[r:ORDERED_ADDITIONAL_SERVICE {OnDate: $Date}]->(b)",
                        new
                        {
                            BookingId = booking.Id,
                            ServiceId = addtnlService.Id,
                            addtnlService.Date
                        });
                }
                foreach (var dmgCompensation in booking.DamageCompensation)
                {
                    session.Run(@"CREATE (a:DamageCompensation {Id:$Id, Price:$Price, Reason:$Reason })",
                        new { dmgCompensation.Id, dmgCompensation.Price, Reason = dmgCompensation.Reason.ToString() });


                    session.Run(@"MATCH (a:RoomBooking), (b:DamageCompensation)
                                    WHERE a.Id = $BookingId AND b.Id=$CompensationId
                                    MERGE (a)-[r:COMPENSATES_DAMAGE {OnDate: $Date}]->(b)",
                        new
                        {
                            BookingId = booking.Id,
                            CompensationId = dmgCompensation.Id,
                            dmgCompensation.Date
                        });
                }
            }
        }


        public void Dispose()
        {
            _driver?.Dispose();
        }
    }
}
