using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Practitioners.Api.Migrations
{
    /// <summary>
    /// Data-миграция: начальное наполнение справочника тестовыми врачами.
    /// SearchVector — generated-колонка, БД заполняет её сама.
    /// </summary>
    public partial class SeedPractitioners : Migration
    {
        private static readonly DateTime SeedTimestamp = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

        private static readonly Guid[] SeedIds =
        {
            new Guid("00000000-0000-0000-0000-000000000001"),
            new Guid("00000000-0000-0000-0000-000000000002"),
            new Guid("00000000-0000-0000-0000-000000000003"),
            new Guid("00000000-0000-0000-0000-000000000004"),
            new Guid("00000000-0000-0000-0000-000000000005"),
            new Guid("00000000-0000-0000-0000-000000000006"),
            new Guid("00000000-0000-0000-0000-000000000007"),
            new Guid("00000000-0000-0000-0000-000000000008"),
            new Guid("00000000-0000-0000-0000-000000000009"),
            new Guid("00000000-0000-0000-0000-000000000010"),
            new Guid("00000000-0000-0000-0000-000000000011"),
            new Guid("00000000-0000-0000-0000-000000000012"),
            new Guid("00000000-0000-0000-0000-000000000013"),
            new Guid("00000000-0000-0000-0000-000000000014"),
            new Guid("00000000-0000-0000-0000-000000000015"),
            new Guid("00000000-0000-0000-0000-000000000016"),
            new Guid("00000000-0000-0000-0000-000000000017"),
            new Guid("00000000-0000-0000-0000-000000000018"),
            new Guid("00000000-0000-0000-0000-000000000019"),
            new Guid("00000000-0000-0000-0000-000000000020"),
            new Guid("00000000-0000-0000-0000-000000000021"),
            new Guid("00000000-0000-0000-0000-000000000022"),
            new Guid("00000000-0000-0000-0000-000000000023"),
            new Guid("00000000-0000-0000-0000-000000000024"),
            new Guid("00000000-0000-0000-0000-000000000025")
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "practitioners",
                columns: new[]
                {
                    "Id", "FirstName", "LastName", "Phone", "Speciality", "PlaceOfWork", "Email",
                    "IsActive", "Created", "Updated", "Version", "IsDeleted", "SourceEventId"
                },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Иван", "Петров", "+375(17) 100-00-01", "Терапевт", "Городская поликлиника №1", "doctor1@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Анна", "Смирнова", "+375(17) 100-00-02", "Кардиолог", "Центр кардиологии", "doctor2@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Сергей", "Кузнецов", "+375(17) 100-00-03", "Хирург", "Городская больница №3", "doctor3@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Елена", "Попова", "+375(17) 100-00-04", "Педиатр", "Детская поликлиника №5", "doctor4@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "Дмитрий", "Васильев", "+375(17) 100-00-05", "Невролог", "Клиника «Нейро+»", "doctor5@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "Ольга", "Соколова", "+375(17) 100-00-06", "Офтальмолог", "Центр зрения", "doctor6@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "Алексей", "Михайлов", "+375(17) 100-00-07", "Ортопед", "Травматологический центр", "doctor7@clinic.example", false, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "Мария", "Новикова", "+375(17) 100-00-08", "Дерматолог", "Клиника «Кожа и здоровье»", "doctor8@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "Павел", "Фёдоров", "+375(17) 100-00-09", "Уролог", "Городская больница №3", "doctor9@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000010"), "Наталья", "Морозова", "+375(17) 100-00-10", "Гинеколог", "Женская консультация №2", "doctor10@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000011"), "Андрей", "Волков", "+375(17) 100-00-11", "Эндокринолог", "Эндокринологический центр", "doctor11@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "Екатерина", "Алексеева", "+375(17) 100-00-12", "Онколог", "Онкологический диспансер", "doctor12@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "Максим", "Лебедев", "+375(17) 100-00-13", "Отоларинголог", "Городская поликлиника №1", "doctor13@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000014"), "Татьяна", "Семенова", "+375(17) 100-00-14", "Гастроэнтеролог", "Клиника «Гастро»", "doctor14@clinic.example", false, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000015"), "Владимир", "Егоров", "+375(17) 100-00-15", "Психиатр", "Психоневрологический диспансер", "doctor15@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000016"), "Юлия", "Павлова", "+375(17) 100-00-16", "Стоматолог", "Стоматология «Улыбка»", "doctor16@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000017"), "Николай", "Козлов", "+375(17) 100-00-17", "Анестезиолог", "Городская больница №3", "doctor17@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000018"), "Светлана", "Степанова", "+375(17) 100-00-18", "Аллерголог", "Центр иммунологии", "doctor18@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000019"), "Игорь", "Николаев", "+375(17) 100-00-19", "Пульмонолог", "Городская поликлиника №2", "doctor19@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000020"), "Ирина", "Орлова", "+375(17) 100-00-20", "Ревматолог", "Ревматологический центр", "doctor20@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000021"), "Александр", "Андреев", "+375(17) 100-00-21", "Терапевт", "Городская поликлиника №2", "doctor21@clinic.example", false, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000022"), "Виктория", "Макарова", "+375(17) 100-00-22", "Кардиолог", "Центр кардиологии", "doctor22@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000023"), "Роман", "Захаров", "+375(17) 100-00-23", "Хирург", "Клиника «Хирургия плюс»", "doctor23@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000024"), "Дарья", "Борисова", "+375(17) 100-00-24", "Педиатр", "Детская поликлиника №5", "doctor24@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null },
                    { new Guid("00000000-0000-0000-0000-000000000025"), "Артём", "Комаров", "+375(17) 100-00-25", "Невролог", "Клиника «Нейро+»", "doctor25@clinic.example", true, SeedTimestamp, SeedTimestamp, 1, false, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var id in SeedIds)
            {
                migrationBuilder.DeleteData(table: "practitioners", keyColumn: "Id", keyValue: id);
            }
        }
    }
}
