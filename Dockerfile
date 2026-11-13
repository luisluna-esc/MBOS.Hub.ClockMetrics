FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MBOS.Hub.ClockMetrics.sln ./
COPY ClockMetrics.Web/ClockMetrics.Web.csproj ClockMetrics.Web/
COPY ClockMetrics.Application/ClockMetrics.Application.csproj ClockMetrics.Application/
COPY ClockMetrics.Core/ClockMetrics.Core.csproj ClockMetrics.Core/
COPY ClockMetrics.Infrastructure/ClockMetrics.Infrastructure.csproj ClockMetrics.Infrastructure/
RUN dotnet restore ClockMetrics.Web/ClockMetrics.Web.csproj

COPY ClockMetrics.Web/ ClockMetrics.Web/
COPY ClockMetrics.Application/ ClockMetrics.Application/
COPY ClockMetrics.Core/ ClockMetrics.Core/
COPY ClockMetrics.Infrastructure/ ClockMetrics.Infrastructure/

RUN dotnet publish ClockMetrics.Web/ClockMetrics.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ClockMetrics.Web.dll"]
