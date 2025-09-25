namespace Zabota.Contracts;

public sealed class RegisterRequest
{
    // Обязательные
    public string LastName { get; set; } = default!;     // Фамилия
    public string FirstName { get; set; } = default!;    // Имя
    public string Login { get; set; } = default!;        // Логин (уникальный)
    public string Password { get; set; } = default!;     // Пароль

    // Необязательные
    public string? MiddleName { get; set; }              // Отчество
    public string? Email { get; set; }
    public string? Phone { get; set; }                   // Номер телефона
    public DateOnly? BirthDate { get; set; }             // Дата рождения (ISO yyyy-MM-dd)
    public string? Role { get; set; }                    // Роль в семье (произвольная строка/enum)
}
