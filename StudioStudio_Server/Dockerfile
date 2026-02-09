FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
EXPOSE 8080

COPY ["StudioStudio_Server/StudioStudio_Server.csproj", "StudioStudio_Server/"]
RUN dotnet restore "./StudioStudio_Server/StudioStudio_Server.csproj"
COPY . .
WORKDIR "/src/StudioStudio_Server"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StudioStudio_Server.dll"]
