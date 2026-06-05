FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/TalentPilot.Worker/TalentPilot.Worker.csproj src/TalentPilot.Worker/
COPY src/TalentPilot.Application/TalentPilot.Application.csproj src/TalentPilot.Application/
COPY src/TalentPilot.Common/TalentPilot.Common.csproj src/TalentPilot.Common/
COPY src/TalentPilot.Domain/TalentPilot.Domain.csproj src/TalentPilot.Domain/
COPY src/TalentPilot.Infrastructure/TalentPilot.Infrastructure.csproj src/TalentPilot.Infrastructure/
RUN dotnet restore src/TalentPilot.Worker/TalentPilot.Worker.csproj

COPY . .
RUN dotnet publish src/TalentPilot.Worker/TalentPilot.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TalentPilot.Worker.dll"]
