# ==============================================================================
# Dockerfile для сервиса Notifications (C-07)
# 
# Это Worker-сервис (фоновый процесс), поэтому используется базовый 
# рантайм dotnet/runtime, а не aspnet (нет HTTP-сервера).
# ==============================================================================

# ------------------------------------------------------------------------------
# STAGE 1: Сборка (Build)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Кэшируем слой восстановления NuGet-пакетов
COPY ["Notifications.csproj", "./"]
RUN dotnet restore "Notifications.csproj"

# Копируем весь исходный код
COPY . .

# Собираем в Release и публикуем
RUN dotnet publish "Notifications.csproj" -c Release -o /app/publish --no-restore

# ------------------------------------------------------------------------------
# STAGE 2: Запуск (Runtime)
# ------------------------------------------------------------------------------
# Используем облегченный образ runtime (без ASP.NET Core)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Нет EXPOSE, так как сервис не принимает входящие HTTP-соединения.
# Он работает как демон и слушает только RabbitMQ.

ENTRYPOINT ["dotnet", "Notifications.dll"]