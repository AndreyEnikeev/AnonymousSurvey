using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebClient.Pages
{
    /// <summary>
    /// Главная страница.
    /// 
    /// Логика:
    ///   - не аутентифицирован → редирект на /Account/Login;
    ///   - аутентифицирован → приветствие с ролью.
    /// 
    /// Ссылки на разделы (анкеты, отчёты, периоды, аудит) появятся в следующем шаге,
    /// когда сами страницы будут готовы. Пока — только проверка, что cookie-auth
    /// и разбор роли из access-токена работают.
    /// </summary>
    public class IndexModel : PageModel
    {
        /// <summary>
        /// GET /. Если пользователь не вошёл — отправляем на логин.
        /// </summary>
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Login");

            return Page();
        }

        /// <summary>
        /// Возвращает человекочитаемое имя роли из claims.
        /// Используется только для отображения — авторизация проверяется через [Authorize(Roles="...")].
        /// </summary>
        public string GetRoleName()
        {
            if (User.IsInRole("1")) return "Студент";
            if (User.IsInRole("2")) return "Преподаватель";
            if (User.IsInRole("3")) return "Администратор";
            if (User.IsInRole("4")) return "Технический специалист";
            return "Неизвестная роль";
        }
    }
}