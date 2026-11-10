# ==============================================================================
# Dockerfile для сервиса LlmOrchestrator (C-06)
# ==============================================================================

# ------------------------------------------------------------------------------
# STAGE 1: Сборка (Build)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Кэшируем слой восстановления NuGet-пакетов
COPY ["LlmOrchestrator.csproj", "./"]
RUN dotnet restore "LlmOrchestrator.csproj"

# Копируем весь исходный код
COPY . .

# Собираем в Release и публикуем
RUN dotnet publish "LlmOrchestrator.csproj" -c Release -o /app/publish --no-restore

# ------------------------------------------------------------------------------
# STAGE 2: Запуск (Runtime)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Слушаем порт 8080 внутри контейнера (для HTTP-эндпоинта O-11)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LlmOrchestrator.dll"]