using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Auth.Services
{
    /// <summary>
    /// Сервис для безопасного хеширования и проверки паролей с использованием алгоритма Argon2id.
    /// Включает поддержку соли (уникальной для каждого пользователя) и перца (глобального секрета).
    /// </summary>
    public class PasswordHasher
    {
        /// <summary>перец</summary>
        private readonly string _pepper;

        // Параметры Argon2id, рекомендованные OWASP.
        /// <summary>заставляет алгоритм использовать 19 МБ RAM (защита от перебора на GPU).</summary>
        private const int MemorySize = 19456;
        /// <summary>количество проходов (увеличивает время вычисления).</summary>
        private const int Iterations = 2;
        /// <summary>количество потоков процессора для одного хеша.</summary>
        private const int DegreeOfParallelism = 1;

        /// <summary>Инициализирует новый экземпляр хешера, загружая "перец" из конфигурации (User Secrets).</summary>
        /// <param name="configuration">Конфигурация приложения, содержащая секретный ключ "Argon2:Pepper".</param>
        public PasswordHasher(IConfiguration configuration)
        {
            _pepper = configuration["Argon2:Pepper"] ?? throw new ArgumentNullException("Pepper not configured");
        }

        /// <summary>
        /// Генерирует криптографически стойкую случайную соль для нового пользователя.
        /// </summary>
        /// <returns>Соль в формате Base64.</returns>
        public string GenerateSalt()
        {
            var salt = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return Convert.ToBase64String(salt);
        }

        /// <summary>
        /// Хеширует пароль с использованием соли и глобального "перца".
        /// </summary>
        /// <param name="password">Пароль в открытом виде.</param>
        /// <param name="saltBase64">Соль пользователя в формате Base64.</param>
        /// <returns>Хеш пароля в формате Base64.</returns>
        public string HashPassword(string password, string saltBase64)
        {
            var salt = Convert.FromBase64String(saltBase64);

            // "Перец" добавляется к паролю перед хешированием. Даже если БД украдут, без перца 
            // злоумышленник не сможет подобрать пароль.
            var passwordBytes = Encoding.UTF8.GetBytes(password + _pepper);

            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = DegreeOfParallelism,
                Iterations = Iterations,
                MemorySize = MemorySize
            };

            var hash = argon2.GetBytes(32);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Проверяет, соответствует ли введенный пароль сохраненному хешу.
        /// </summary>
        /// <param name="password">Введенный пароль в открытом виде.</param>
        /// <param name="saltBase64">Сохраненная соль пользователя.</param>
        /// <param name="hashBase64">Сохраненный хеш пароля.</param>
        /// <returns>True, если пароль верный, иначе False.</returns>
        public bool VerifyPassword(string password, string saltBase64, string hashBase64)
        {
            var computedHash = HashPassword(password, saltBase64);

            // FixedTimeEquals защищает от атак по времени (Timing Attacks), сравнивая байты за одинаковое время.
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(computedHash),
                Convert.FromBase64String(hashBase64)
            );
        }
    }
}