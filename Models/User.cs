namespace Zabota.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

public enum RecurrenceType
{
    Daily = 0,         // ежедневно
    SpecificDays = 1   // по выбранным дням недели
}

public enum WeekDay
{
    Monday = 1, Tuesday = 2, Wednesday = 3, Thursday = 4, Friday = 5, Saturday = 6, Sunday = 7
}

/// Пользователь с Guid PK
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Аутентификация
    [MaxLength(32)] public string Login { get; set; } = default!;
    [MaxLength(200)] public string PasswordHash { get; set; } = default!;
    [MaxLength(320)] public string? Email { get; set; }
    [MaxLength(32)] public string? Phone { get; set; }

    // Профиль
    [MaxLength(100)] public string LastName { get; set; } = default!;
    [MaxLength(100)] public string FirstName { get; set; } = default!;
    [MaxLength(100)] public string? MiddleName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [MaxLength(20)] public string? Role { get; set; }

    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Навигация
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<BpRecord> BpRecords { get; set; } = new List<BpRecord>();
    public ICollection<Medication> Medications { get; set; } = new List<Medication>();
    public ICollection<FamilyMember> FamilyMemberships { get; set; } = new List<FamilyMember>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
/// Таблица задач пользователя
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = default!;
    [MaxLength(2000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;     // активная/неактивная
    public bool IsCompleted { get; set; } = false; // завершена/нет
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueAtUtc { get; set; }

    public User User { get; set; } = default!;
}

/// Таблица давления/пульса пользователя
public class BpRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    // значения
    public int Systolic { get; set; }   // верхнее
    public int Diastolic { get; set; }  // нижнее
    public int? Pulse { get; set; }     // пульс
    public DateTime MeasuredAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(500)] public string? Note { get; set; }

    public User User { get; set; } = default!;
}

/// Лекарства пользователя
public class Medication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [MaxLength(200)] public string Name { get; set; } = default!;
    [MaxLength(1000)] public string? Notes { get; set; }

    public RecurrenceType Recurrence { get; set; } = RecurrenceType.Daily;

    // Времена приёма (несколько раз в день)
    public ICollection<MedicationTime> Times { get; set; } = new List<MedicationTime>();

    // Дни недели, если Recurrence = SpecificDays
    public ICollection<MedicationDay> Days { get; set; } = new List<MedicationDay>();

    public User User { get; set; } = default!;
}

/// Конкретное время приёма для лекарства (часы:минуты)
public class MedicationTime
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicationId { get; set; }

    public int Hour { get; set; }   // 0..23
    public int Minute { get; set; } // 0..59

    public Medication Medication { get; set; } = default!;
}

/// День недели, когда принимать лекарство (если SpecificDays)
public class MedicationDay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicationId { get; set; }

    public WeekDay DayOfWeek { get; set; }

    public Medication Medication { get; set; } = default!;
}

/// Семья
public class Family
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)] public string Name { get; set; } = default!;

    // Уникальный код приглашения, 12 символов
    [MaxLength(12)] public string InviteCode { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Участники (многие-ко-многим через таблицу связей с ролями/флагами)
    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();

    // Сообщения чата
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// Участник семьи (связь User<->Family с атрибутами)
public class FamilyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }

    [MaxLength(50)] public string RoleInFamily { get; set; } = "Member"; // например: Мама/Папа/Сын/...

    public bool IsAdmin { get; set; }

    public Family Family { get; set; } = default!;
    public User User { get; set; } = default!;
}

/// Сообщения чата семьи
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }
    public Guid AuthorUserId { get; set; }

    [MaxLength(4000)] public string Text { get; set; } = default!;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAtUtc { get; set; }
    public bool IsDeleted { get; set; } = false;

    public Family Family { get; set; } = default!;
    public User AuthorUser { get; set; } = default!;
}