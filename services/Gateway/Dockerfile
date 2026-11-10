# ==============================================================================
# Dockerfile для сервиса Gateway (C-02)
# ==============================================================================

# ------------------------------------------------------------------------------
# STAGE 1: Сборка (Build)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем только .csproj для кэширования слоя восстановления NuGet
COPY ["Gateway.csproj", "./"]

RUN dotnet restore "Gateway.csproj"

# Копируем весь остальной исходный код
COPY . .

# Собираем в Release и публикуем
RUN dotnet publish "Gateway.csproj" -c Release -o /app/publish --no-restore

# ------------------------------------------------------------------------------
# STAGE 2: Запуск (Runtime)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Gateway — единственный сервис, который будет смотреть наружу (порт 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Gateway.dll"]