FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/TalentPilot.Api/TalentPilot.Api.csproj src/TalentPilot.Api/
COPY src/TalentPilot.Application/TalentPilot.Application.csproj src/TalentPilot.Application/
COPY src/TalentPilot.Common/TalentPilot.Common.csproj src/TalentPilot.Common/
COPY src/TalentPilot.Domain/TalentPilot.Domain.csproj src/TalentPilot.Domain/
COPY src/TalentPilot.Infrastructure/TalentPilot.Infrastructure.csproj src/TalentPilot.Infrastructure/
RUN dotnet restore src/TalentPilot.Api/TalentPilot.Api.csproj

COPY . .
RUN dotnet publish src/TalentPilot.Api/TalentPilot.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TalentPilot.Api.dll"]
