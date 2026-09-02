# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY . .
RUN dotnet restore "OnlineBankingSystem.csproj"

# Publish the application in Release mode
RUN dotnet publish "OnlineBankingSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy the published output
COPY --from=build /app/publish .

# Railway provides the PORT environment variable; bind to 0.0.0.0 on that port.
CMD ["sh", "-c", "dotnet OnlineBankingSystem.dll --urls http://0.0.0.0:${PORT:-8080}"]
