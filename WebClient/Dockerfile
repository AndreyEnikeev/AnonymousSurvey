# ==============================================================================
# Dockerfile для веб-клиента WebClient (C-01)
# ==============================================================================

# ------------------------------------------------------------------------------
# STAGE 1: Сборка (Build)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Кэшируем слой восстановления NuGet-пакетов
COPY ["WebClient.csproj", "./"]
RUN dotnet restore "WebClient.csproj"

# Копируем весь исходный код (включая Pages, wwwroot и т.д.)
COPY . .

# Собираем в Release и публикуем
RUN dotnet publish "WebClient.csproj" -c Release -o /app/publish --no-restore

# ------------------------------------------------------------------------------
# STAGE 2: Запуск (Runtime)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Слушаем порт 8080 внутри контейнера
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WebClient.dll"]