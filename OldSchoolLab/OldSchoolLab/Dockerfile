FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OldSchoolLab.csproj", "."]
RUN dotnet restore "OldSchoolLab.csproj"

COPY . .

RUN dotnet build "OldSchoolLab.csproj" -c Release -o /app/build
RUN dotnet publish "OldSchoolLab.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OldSchoolLab.dll"]
