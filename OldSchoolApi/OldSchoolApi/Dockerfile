FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["OldSchoolApi.csproj", "."]
RUN dotnet restore "OldSchoolApi.csproj"
COPY . .
RUN dotnet build "OldSchoolApi.csproj" -c Release -o /app/build
RUN dotnet publish "OldSchoolApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OldSchoolApi.dll"]
