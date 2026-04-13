#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TMP_DIR="$(mktemp -d)"
PROJ_DIR="$TMP_DIR/SlopePhysicsTest"

mkdir -p "$PROJ_DIR"

cat > "$PROJ_DIR/SlopePhysicsTest.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="__ROOT__/WildfireSimulator.Domain/WildfireSimulator.Domain.csproj" />
    <ProjectReference Include="__ROOT__/WildfireSimulator.Application/WildfireSimulator.Application.csproj" />
  </ItemGroup>
</Project>
XML

sed -i "s|__ROOT__|$ROOT_DIR|g" "$PROJ_DIR/SlopePhysicsTest.csproj"

cat > "$PROJ_DIR/Program.cs" <<'CS'
using WildfireSimulator.Application.Services;
using WildfireSimulator.Domain.Models;

Console.WriteLine("============================================================");
Console.WriteLine(" ТЕСТ 11.7a: slope physics должен давать больше тепла вверх");
Console.WriteLine("============================================================");

var calculator = new FireSpreadCalculator();

var weather = new WeatherCondition(
    timestamp: DateTime.UtcNow,
    temperature: 25,
    humidity: 40,
    windSpeedMps: 0,
    windDirectionDegrees: 0,
    precipitation: 0);

var source = new ForestCell(
    x: 10,
    y: 10,
    vegetation: VegetationType.Grass,
    moisture: 0.30,
    elevation: 50);

source.Ignite(DateTime.UtcNow);
source.SetBurningElapsedSeconds(120);

var uphill = new ForestCell(
    x: 10,
    y: 11,
    vegetation: VegetationType.Grass,
    moisture: 0.30,
    elevation: 80);

var downhill = new ForestCell(
    x: 10,
    y: 9,
    vegetation: VegetationType.Grass,
    moisture: 0.30,
    elevation: 20);

double stepDurationSeconds = 900;

double uphillHeat = calculator.CalculateHeatFlow(source, uphill, weather, stepDurationSeconds);
double downhillHeat = calculator.CalculateHeatFlow(source, downhill, weather, stepDurationSeconds);

double uphillThreshold = calculator.CalculateIgnitionThreshold(uphill, weather);
double downhillThreshold = calculator.CalculateIgnitionThreshold(downhill, weather);

double uphillProb = calculator.CalculateIgnitionProbability(uphillHeat, uphillThreshold);
double downhillProb = calculator.CalculateIgnitionProbability(downhillHeat, downhillThreshold);

Console.WriteLine($"uphill_heat      = {uphillHeat:F4}");
Console.WriteLine($"downhill_heat    = {downhillHeat:F4}");
Console.WriteLine($"uphill_threshold = {uphillThreshold:F4}");
Console.WriteLine($"downhill_threshold = {downhillThreshold:F4}");
Console.WriteLine($"uphill_prob      = {uphillProb:F6}");
Console.WriteLine($"downhill_prob    = {downhillProb:F6}");

if (uphillHeat <= downhillHeat)
{
    Console.WriteLine("❌ HeatFlow вверх не больше, чем вниз");
    Environment.Exit(1);
}

if (uphillProb < downhillProb)
{
    Console.WriteLine("❌ Вероятность вверх меньше, чем вниз");
    Environment.Exit(1);
}

Console.WriteLine("✅ Уклон реально усиливает перенос вверх при прочих равных");
Console.WriteLine("============================================================");
CS

dotnet run --project "$PROJ_DIR/SlopePhysicsTest.csproj"