using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WildfireSimulator.Client.Models;

namespace WildfireSimulator.Client.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    private string _baseUrl = "http://localhost:5198";

    public ApiService()
    {
        _httpClient = new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetBaseUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/simulations");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<SimulationDto>> GetSimulationsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/simulations");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<SimulationDto>>(json, _jsonOptions) ?? new();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting simulations: {ex.Message}");
        }

        return new List<SimulationDto>();
    }

    public async Task<Guid?> CreateSimulationAsync(
      CreateSimulationDto dto,
      double temperature,
      double humidity,
      double windSpeed,
      double windDirection)
    {
        try
        {
            var request = new
            {
                name = dto.Name,
                description = dto.Description,
                gridWidth = dto.GridWidth,
                gridHeight = dto.GridHeight,
                graphType = dto.GraphType,
                initialMoistureMin = dto.InitialMoistureMin,
                initialMoistureMax = dto.InitialMoistureMax,
                elevationVariation = dto.ElevationVariation,
                initialFireCellsCount = dto.InitialFireCellsCount,
                simulationSteps = dto.SimulationSteps,
                stepDurationSeconds = dto.StepDurationSeconds,
                randomSeed = dto.RandomSeed,
                vegetationDistributions = dto.VegetationDistributions,

                mapCreationMode = dto.MapCreationMode,
                scenarioType = dto.ScenarioType,
                mapNoiseStrength = dto.MapNoiseStrength,
                mapDrynessFactor = dto.MapDrynessFactor,
                reliefStrengthFactor = dto.ReliefStrengthFactor,
                fuelDensityFactor = dto.FuelDensityFactor,
                mapRegionObjects = dto.MapRegionObjects,

                temperature = temperature,
                humidity = humidity,
                windSpeed = windSpeed,
                windDirection = windDirection,
                precipitation = dto.Precipitation
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/simulations", content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var simulation = JsonSerializer.Deserialize<SimulationDto>(responseJson, _jsonOptions);
                return simulation?.Id;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating simulation: {ex.Message}");
        }

        return null;
    }

    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells, bool IsRunning, double FireArea, int CurrentStep, int Status)> StartSimulationAsync(
     Guid simulationId,
     string ignitionMode = "saved-or-random",
     List<(int X, int Y)>? initialFirePositions = null)
    {
        try
        {
            object? requestBody = null;

            if (ignitionMode == "manual")
            {
                requestBody = new
                {
                    ignitionMode = "manual",
                    initialFirePositions = (initialFirePositions ?? new List<(int X, int Y)>())
                        .Select(p => new { x = p.X, y = p.Y })
                        .ToList()
                };
            }
            else
            {
                requestBody = new
                {
                    ignitionMode = ignitionMode
                };
            }

            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/start", content);
            var responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Start response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = doc.RootElement.TryGetProperty("message", out var msgElement)
                    ? msgElement.GetString() ?? "Ошибка запуска"
                    : "Ошибка запуска";

                return (false, errorMessage, null, false, 0, 0, -1);
            }

            List<GraphCellDto>? cells = null;
            if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
            {
                cells = JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions);
            }

            bool isRunning = false;
            double fireArea = 0;
            int currentStep = 0;
            int status = 0;

            if (doc.RootElement.TryGetProperty("activeSimulation", out var activeSimElement))
            {
                if (activeSimElement.TryGetProperty("isRunning", out var runningElement))
                    isRunning = runningElement.GetBoolean();
                if (activeSimElement.TryGetProperty("fireArea", out var areaElement))
                    fireArea = areaElement.GetDouble();
                if (activeSimElement.TryGetProperty("step", out var stepElement))
                    currentStep = stepElement.GetInt32();
                if (activeSimElement.TryGetProperty("status", out var statusElement))
                    status = statusElement.GetInt32();
            }

            var message = doc.RootElement.TryGetProperty("message", out var okMsg)
                ? okMsg.GetString() ?? "Симуляция запущена"
                : "Симуляция запущена";

            return (true, message, cells, isRunning, fireArea, currentStep, status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting simulation: {ex.Message}");
            return (false, ex.Message, null, false, 0, 0, -1);
        }
    }

    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells, StepResultDto? StepResult, bool IsRunning, int Status)> ExecuteStepAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/step", null);
            var responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Step response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = doc.RootElement.TryGetProperty("message", out var msgElement)
                    ? msgElement.GetString() ?? "Ошибка шага"
                    : "Ошибка шага";

                return (false, errorMessage, null, null, false, -1);
            }

            List<GraphCellDto>? cells = null;
            if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
            {
                cells = JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions);
            }

            StepResultDto? stepResult = null;
            if (doc.RootElement.TryGetProperty("step", out var stepElement))
            {
                stepResult = JsonSerializer.Deserialize<StepResultDto>(stepElement.GetRawText(), _jsonOptions);
            }

            bool isRunning = false;
            if (doc.RootElement.TryGetProperty("isRunning", out var runningElement))
            {
                isRunning = runningElement.GetBoolean();
            }

            int status = -1;
            if (doc.RootElement.TryGetProperty("status", out var statusElement))
            {
                status = statusElement.GetInt32();
            }

            var message = doc.RootElement.TryGetProperty("message", out var okMsg)
                ? okMsg.GetString() ?? "Шаг выполнен"
                : "Шаг выполнен";

            return (true, message, cells, stepResult, isRunning, status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing step: {ex.Message}");
            return (false, ex.Message, null, null, false, -1);
        }
    }

    public async Task<SimulationStatusDto?> GetSimulationStatusAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/status");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("success", out var successElement) && successElement.GetBoolean())
                {
                    var status = new SimulationStatusDto();

                    if (doc.RootElement.TryGetProperty("simulation", out var simElement))
                    {
                        if (simElement.TryGetProperty("id", out var idElement))
                            status.Id = idElement.GetGuid();
                        if (simElement.TryGetProperty("name", out var nameElement))
                            status.Name = nameElement.GetString() ?? string.Empty;
                        if (simElement.TryGetProperty("status", out var statusElement))
                            status.Status = statusElement.GetInt32();
                        if (simElement.TryGetProperty("currentStep", out var stepElement))
                            status.CurrentStep = stepElement.GetInt32();
                        if (simElement.TryGetProperty("isRunning", out var runningElement))
                            status.IsRunning = runningElement.GetBoolean();
                        if (simElement.TryGetProperty("fireArea", out var areaElement))
                            status.FireArea = areaElement.GetDouble();
                        if (simElement.TryGetProperty("totalBurnedCells", out var burnedElement))
                            status.TotalBurnedCells = burnedElement.GetInt32();
                        if (simElement.TryGetProperty("totalBurningCells", out var burningElement))
                            status.TotalBurningCells = burningElement.GetInt32();
                        if (simElement.TryGetProperty("graphType", out var graphTypeElement))
                            status.GraphType = (GraphType)graphTypeElement.GetInt32();
                        if (simElement.TryGetProperty("precipitation", out var precipitationElement))
                            status.Precipitation = precipitationElement.GetDouble();
                    }

                    if (doc.RootElement.TryGetProperty("weather", out var weatherElement) &&
                        weatherElement.ValueKind == JsonValueKind.Object)
                    {
                        if (weatherElement.TryGetProperty("temperature", out var temperatureElement))
                            status.Temperature = temperatureElement.GetDouble();

                        if (weatherElement.TryGetProperty("humidity", out var humidityElement))
                            status.Humidity = humidityElement.GetDouble();

                        if (weatherElement.TryGetProperty("windSpeed", out var windSpeedElement))
                            status.WindSpeed = windSpeedElement.GetDouble();

                        if (weatherElement.TryGetProperty("windDirection", out var windDirectionElement))
                            status.WindDirection = windDirectionElement.GetString() ?? "—";

                        if (weatherElement.TryGetProperty("windDirectionDegrees", out var windDirectionDegreesElement))
                            status.WindDirectionDegrees = windDirectionDegreesElement.GetDouble();

                        if (weatherElement.TryGetProperty("precipitation", out var weatherPrecipitationElement))
                            status.Precipitation = weatherPrecipitationElement.GetDouble();
                    }

                    if (doc.RootElement.TryGetProperty("warning", out var warningElement) &&
                        warningElement.ValueKind != JsonValueKind.Null)
                    {
                        status.Warning = warningElement.GetString();
                    }

                    return status;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting status: {ex.Message}");
        }

        return null;
    }
    public async Task<SimulationGraphDto?> GetSimulationGraphAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/graph");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var graphResponse = JsonSerializer.Deserialize<SimulationGraphResponseDto>(json, _jsonOptions);

            if (graphResponse?.Success == true)
                return graphResponse.Graph;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting simulation graph: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> DeleteSimulationAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/SimulationManager/{simulationId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting simulation: {ex.Message}");
            return false;
        }
    }

    public async Task<List<GraphCellDto>> GetSimulationCellsAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/cells");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();

                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
                {
                    return JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions) ?? new();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting cells: {ex.Message}");
        }

        return new List<GraphCellDto>();
    }

    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells, bool IsRunning, double FireArea, int CurrentStep, int Status)> ResetSimulationAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/reset", null);
            var responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Reset response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = doc.RootElement.TryGetProperty("message", out var msgElement)
                    ? msgElement.GetString() ?? "Ошибка перезапуска"
                    : "Ошибка перезапуска";

                return (false, errorMessage, null, false, 0, 0, -1);
            }

            List<GraphCellDto>? cells = null;
            if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
            {
                cells = JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions);
            }

            bool isRunning = false;
            double fireArea = 0;
            int currentStep = 0;
            int status = 0;

            if (doc.RootElement.TryGetProperty("activeSimulation", out var activeSimElement))
            {
                if (activeSimElement.TryGetProperty("isRunning", out var runningElement))
                    isRunning = runningElement.GetBoolean();
                if (activeSimElement.TryGetProperty("fireArea", out var areaElement))
                    fireArea = areaElement.GetDouble();
                if (activeSimElement.TryGetProperty("step", out var stepElement))
                    currentStep = stepElement.GetInt32();
                if (activeSimElement.TryGetProperty("status", out var statusElement))
                    status = statusElement.GetInt32();
            }

            var message = doc.RootElement.TryGetProperty("message", out var okMsg)
                ? okMsg.GetString() ?? "Симуляция перезапущена"
                : "Симуляция перезапущена";

            return (true, message, cells, isRunning, fireArea, currentStep, status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting simulation: {ex.Message}");
            return (false, ex.Message, null, false, 0, 0, -1);
        }
    }
    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells)> RefreshIgnitionSetupAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/refresh-ignitions", null);
            var responseText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Refresh-ignitions response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = doc.RootElement.TryGetProperty("message", out var msgElement)
                    ? msgElement.GetString() ?? "Ошибка обновления очагов"
                    : "Ошибка обновления очагов";

                return (false, errorMessage, null);
            }

            List<GraphCellDto>? cells = null;
            if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
            {
                cells = JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions);
            }

            var message = doc.RootElement.TryGetProperty("message", out var okMsg)
                ? okMsg.GetString() ?? "Очаги обновлены"
                : "Очаги обновлены";

            return (true, message, cells);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing ignition setup: {ex.Message}");
            return (false, ex.Message, null);
        }
    }
    public async Task<List<FireMetricsHistoryDto>> GetSimulationMetricsHistoryAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/simulations/{simulationId}/metrics");
            if (!response.IsSuccessStatusCode)
                return new List<FireMetricsHistoryDto>();

            var json = await response.Content.ReadAsStringAsync();
            var historyResponse = JsonSerializer.Deserialize<FireMetricsHistoryResponseDto>(json, _jsonOptions);

            if (historyResponse?.Success == true && historyResponse.Metrics != null)
                return historyResponse.Metrics;

            return new List<FireMetricsHistoryDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting simulation metrics history: {ex.Message}");
            return new List<FireMetricsHistoryDto>();
        }
    }
}