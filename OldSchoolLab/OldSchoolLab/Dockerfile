FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OldSchoolLab/OldSchoolLab.csproj", "OldSchoolLab/"]
RUN dotnet restore "OldSchoolLab/OldSchoolLab.csproj"

COPY . .

WORKDIR "/src/OldSchoolLab"
RUN dotnet build "OldSchoolLab.csproj" -c Release -o /app/build
RUN dotnet publish "OldSchoolLab.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8085
EXPOSE 8085
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OldSchoolLab.dll"]