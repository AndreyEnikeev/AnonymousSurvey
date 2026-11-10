using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebClient.Pages
{
    /// <summary>
    /// Страница для всех ошибок: исключений (500) и статус-кодов 4xx/5xx.
    /// 
    /// ВАЖНО: сюда НЕЛЬЗЯ выводить текст исключения, стек или SQL —
    /// это утечка внутренней информации. Показываем только нейтральный код,
    /// человекочитаемое описание и RequestId (для сопоставления с логом сервера).
    /// </summary>
    public class ErrorModel : PageModel
    {
        /// <summary>Идентификатор запроса — для связи с журналом сервера.</summary>
        public string? RequestId { get; set; }

        /// <summary>HTTP-код ошибки (из ?code=404 и т.п.) или null для 500.</summary>
        public int? Code { get; set; }

        /// <summary>Показывать ли RequestId (только если он есть).</summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>
        /// Вызывается для любого редиректа сюда: с параметром code (4xx/5xx) или без (500).
        /// </summary>
        public void OnGet([FromQuery] int? code = null)
        {
            Code = code;
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }

        /// <summary>Человекочитаемое описание ошибки по её коду.</summary>
        public string GetMessage() => Code switch
        {
            400 => "Сервер не смог понять запрос.",
            401 => "Требуется аутентификация.",
            403 => "У вас недостаточно прав для этой страницы.",
            404 => "Страница не найдена.",
            405 => "Метод запроса не поддерживается.",
            429 => "Слишком много запросов. Попробуйте позже.",
            500 => "Внутренняя ошибка сервера.",
            502 => "Ошибка шлюза. Попробуйте позже.",
            503 => "Сервис временно недоступен.",
            _ => "Что-то пошло не так."
        };

        /// <summary>Заголовок страницы: «Ошибка 404» или просто «Ошибка».</summary>
        public string GetTitle() => Code.HasValue ? $"Ошибка {Code}" : "Ошибка";
    }
}