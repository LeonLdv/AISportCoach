FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config .
COPY ["src/AISportCoach.API/AISportCoach.API.csproj", "src/AISportCoach.API/"]
COPY ["src/AISportCoach.Application/AISportCoach.Application.csproj", "src/AISportCoach.Application/"]
COPY ["src/AISportCoach.Infrastructure/AISportCoach.Infrastructure.csproj", "src/AISportCoach.Infrastructure/"]
COPY ["src/AISportCoach.Domain/AISportCoach.Domain.csproj", "src/AISportCoach.Domain/"]
COPY ["aspire/AISportCoach.ServiceDefaults/AISportCoach.ServiceDefaults.csproj", "aspire/AISportCoach.ServiceDefaults/"]
RUN dotnet restore src/AISportCoach.API/AISportCoach.API.csproj
COPY . .
RUN dotnet publish src/AISportCoach.API -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AISportCoach.API.dll"]
