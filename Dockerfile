# ===== Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
# Kestrel слушает HTTP внутри контейнера
ENV ASPNETCORE_URLS=http://0.0.0.0:5000

# ===== Build =====
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем проект и восстанавливаем зависимости
COPY Zabota.csproj ./
RUN dotnet restore Zabota.csproj

# Копируем весь исходник и публикуем
COPY . ./
RUN dotnet publish Zabota.csproj -c Release -o /out

# ===== Final =====
FROM base AS final
WORKDIR /app
COPY --from=build /out ./
# Имя dll = имя проекта (Zabota.csproj -> Zabota.dll)
ENTRYPOINT ["dotnet", "Zabota.dll"]
