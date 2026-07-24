FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["MediCorePMS.csproj", "./"]
RUN dotnet restore "MediCorePMS.csproj"

COPY . .
RUN dotnet publish "MediCorePMS.csproj" --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
CMD ["sh", "-c", "dotnet MediCorePMS.dll --urls http://0.0.0.0:${PORT:-10000}"]
