FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/TalentPilot.Database/TalentPilot.Database.csproj src/TalentPilot.Database/
COPY src/TalentPilot.Common/TalentPilot.Common.csproj src/TalentPilot.Common/
RUN dotnet restore src/TalentPilot.Database/TalentPilot.Database.csproj

COPY . .
RUN dotnet publish src/TalentPilot.Database/TalentPilot.Database.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY scripts ./scripts
ENTRYPOINT ["dotnet", "TalentPilot.Database.dll", "--scripts", "/app/scripts"]
