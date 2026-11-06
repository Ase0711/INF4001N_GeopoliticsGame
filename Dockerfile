FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project definitions
COPY *.sln .
COPY src/GeopoliticsGame.Core/*.csproj src/GeopoliticsGame.Core/
COPY src/GeopoliticsGame.Web/*.csproj src/GeopoliticsGame.Web/
COPY tests/GeopoliticsGame.Tests/*.csproj tests/GeopoliticsGame.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source files and publish
COPY . .
WORKDIR /app/src/GeopoliticsGame.Web
RUN dotnet publish -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GeopoliticsGame.Web.dll"]