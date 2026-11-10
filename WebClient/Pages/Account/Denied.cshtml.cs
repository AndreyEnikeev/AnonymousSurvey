using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebClient.Pages.Account
{
    /// <summary>
    /// Страница 403. Показывается cookie-аутентификацией, когда пользователь
    /// аутентифицирован, но не имеет нужной роли (AccessDeniedPath в Program.cs).
    /// </summary>
    public class DeniedModel : PageModel
    {
        /// <summary>Просто рендерит страницу.</summary>
        public void OnGet() { }
    }
}