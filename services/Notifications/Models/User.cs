namespace Notifications.Models
{
    /// <summary>
    /// Проекция таблицы users для сервиса уведомлений.
    /// 
    /// Содержит только те поля, на которые у notifications_service выдано
    /// право SELECT: id, email, role_id. Хеши паролей и логины сюда не попадают —
    /// принцип наименьших привилегий.
    /// </summary>
    public class User
    {
        /// <summary>Уникальный идентификатор пользователя.</summary>
        public Guid Id { get; set; }

        /// <summary>Адрес электронной почты для доставки уведомлений.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Идентификатор роли: 1=student, 2=teacher, 3=admin, 4=tech.</summary>
        public short RoleId { get; set; }
    }
}