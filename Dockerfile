FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["IoTHighPerf.sln", "./"]
COPY ["IoTHighPerf.Api/*.csproj", "IoTHighPerf.Api/"]
COPY ["IoTHighPerf.Core/*.csproj", "IoTHighPerf.Core/"]
COPY ["IoTHighPerf.Infrastructure/*.csproj", "IoTHighPerf.Infrastructure/"]

COPY ["IoTHighPerf.UnitTests/*.csproj", "IoTHighPerf.UnitTests/"]
COPY ["IoTHighPerf.IntegrationTests/*.csproj", "IoTHighPerf.IntegrationTests/"]
COPY ["IoTHighPerf.ActivityGenerator/*.csproj", "IoTHighPerf.ActivityGenerator/"]

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .


# Build and publish
RUN dotnet publish "IoTHighPerf.Api/IoTHighPerf.Api.csproj" -c Release -o /app/publish

# Générer certificat
# Générer et copier le certificat dans le répertoire de publication
RUN mkdir /app/publish/cert && \
    dotnet dev-certs https -ep /app/publish/cert/cert.pfx -p YourSecurePassword


# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Configure container
# Update ENV and EXPOSE
ENV ASPNETCORE_URLS="http://+:5000;https://+:5001"
EXPOSE 5000 5001


# Add certificate handling
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/cert/cert.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=YourSecurePassword

# Optimize for performance
ENV COMPlus_TieredCompilation=1
ENV COMPlus_TC_QuickJit=1
ENV COMPlus_ReadyToRun=0

ENTRYPOINT ["dotnet", "IoTHighPerf.Api.dll"]