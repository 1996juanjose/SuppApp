FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["OldSchool.AuthService.csproj", "./"]
RUN dotnet restore "OldSchool.AuthService.csproj"
COPY . .
RUN dotnet publish "OldSchool.AuthService.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=America/Lima
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "OldSchool.AuthService.dll"]
