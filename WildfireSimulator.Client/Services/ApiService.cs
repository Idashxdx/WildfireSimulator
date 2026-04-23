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
                return JsonSerializer.Deserialize<List<SimulationDto>>(json, _jsonOptions) ?? new List<SimulationDto>();
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
                graphScaleType = dto.GraphScaleType,

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
                clusteredScenarioType = dto.ClusteredScenarioType,

                mapNoiseStrength = dto.MapNoiseStrength,
                mapDrynessFactor = dto.MapDrynessFactor,
                reliefStrengthFactor = dto.ReliefStrengthFactor,
                fuelDensityFactor = dto.FuelDensityFactor,

                mapRegionObjects = dto.MapRegionObjects,
                clusteredBlueprint = dto.ClusteredBlueprint,
                initialFirePositions = dto.InitialFirePositions,

                selectedDemoPreset = dto.SelectedDemoPreset,
                preparedMap = dto.PreparedMap,

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

            var errorText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"CreateSimulation failed: {response.StatusCode} {errorText}");
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
            object requestBody;

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
                return (
                    false,
                    ReadMessage(doc, "Ошибка запуска"),
                    null,
                    false,
                    0,
                    0,
                    -1);
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

            return (
                true,
                ReadMessage(doc, "Симуляция запущена"),
                cells,
                isRunning,
                fireArea,
                currentStep,
                status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting simulation: {ex.Message}");
            return (false, ex.Message, null, false, 0, 0, -1);
        }
    }

    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells, StepResultDto? StepResult, bool IsRunning, int Status)> ExecuteStepAsync(
        Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/step", null);
            var responseText = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"ExecuteStep response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                return (
                    false,
                    ReadMessage(doc, "Ошибка выполнения шага"),
                    null,
                    null,
                    false,
                    -1);
            }

            StepResultDto? stepResult = null;
            if (doc.RootElement.TryGetProperty("step", out var stepElement))
            {
                stepResult = JsonSerializer.Deserialize<StepResultDto>(stepElement.GetRawText(), _jsonOptions);
            }

            List<GraphCellDto>? cells = null;
            if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
            {
                cells = JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions);
            }

            bool isRunning = false;
            int status = -1;

            if (doc.RootElement.TryGetProperty("activeSimulation", out var activeSimElement))
            {
                if (activeSimElement.TryGetProperty("isRunning", out var runningElement))
                    isRunning = runningElement.GetBoolean();

                if (activeSimElement.TryGetProperty("status", out var statusElement))
                    status = statusElement.GetInt32();
            }

            return (
                true,
                ReadMessage(doc, "Шаг выполнен"),
                cells,
                stepResult,
                isRunning,
                status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing step: {ex.Message}");
            return (false, ex.Message, null, null, false, -1);
        }
    }

    public async Task<List<GraphCellDto>> GetSimulationCellsAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/cells");
            if (!response.IsSuccessStatusCode)
                return new List<GraphCellDto>();

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("cells", out var cellsElement))
                return new List<GraphCellDto>();

            return JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions)
                   ?? new List<GraphCellDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting simulation cells: {ex.Message}");
            return new List<GraphCellDto>();
        }
    }

    public async Task<SimulationGraphDto?> GetSimulationGraphAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/graph");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("graph", out var graphElement))
                return null;

            return JsonSerializer.Deserialize<SimulationGraphDto>(graphElement.GetRawText(), _jsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting simulation graph: {ex.Message}");
            return null;
        }
    }

    public async Task<SimulationStatusDto?> GetSimulationStatusAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/status");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;

            if (!root.TryGetProperty("simulation", out var simulationElement))
                return null;

            root.TryGetProperty("weather", out var weatherElement);
            root.TryGetProperty("warning", out var warningElement);
            root.TryGetProperty("graph", out var graphElement);

            var result = new SimulationStatusDto
            {
                Id = simulationElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetGuid()
                    : Guid.Empty,

                Name = simulationElement.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString() ?? string.Empty
                    : string.Empty,

                Status = simulationElement.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.Number
                    ? statusEl.GetInt32()
                    : 0,

                CurrentStep = simulationElement.TryGetProperty("currentStep", out var stepEl) && stepEl.ValueKind == JsonValueKind.Number
                    ? stepEl.GetInt32()
                    : 0,

                IsRunning = simulationElement.TryGetProperty("isRunning", out var runningEl) && runningEl.ValueKind == JsonValueKind.True
                    || (simulationElement.TryGetProperty("isRunning", out runningEl) && runningEl.ValueKind == JsonValueKind.False
                        ? runningEl.GetBoolean()
                        : false),

                FireArea = simulationElement.TryGetProperty("fireArea", out var fireAreaEl) && fireAreaEl.ValueKind == JsonValueKind.Number
                    ? fireAreaEl.GetDouble()
                    : 0.0,

                TotalBurnedCells = simulationElement.TryGetProperty("totalBurnedCells", out var burnedEl) && burnedEl.ValueKind == JsonValueKind.Number
                    ? burnedEl.GetInt32()
                    : 0,

                TotalBurningCells = simulationElement.TryGetProperty("totalBurningCells", out var burningEl) && burningEl.ValueKind == JsonValueKind.Number
                    ? burningEl.GetInt32()
                    : 0,

                GraphType = simulationElement.TryGetProperty("graphType", out var graphTypeEl) && graphTypeEl.ValueKind == JsonValueKind.Number
                    ? (GraphType)graphTypeEl.GetInt32()
                    : GraphType.Grid,

                Precipitation = simulationElement.TryGetProperty("precipitation", out var precipitationEl) && precipitationEl.ValueKind == JsonValueKind.Number
                    ? precipitationEl.GetDouble()
                    : 0.0,

                Temperature = weatherElement.ValueKind == JsonValueKind.Object &&
                              weatherElement.TryGetProperty("temperature", out var tempEl) &&
                              tempEl.ValueKind == JsonValueKind.Number
                    ? tempEl.GetDouble()
                    : 0.0,

                Humidity = weatherElement.ValueKind == JsonValueKind.Object &&
                           weatherElement.TryGetProperty("humidity", out var humidityEl) &&
                           humidityEl.ValueKind == JsonValueKind.Number
                    ? humidityEl.GetDouble()
                    : 0.0,

                WindSpeed = weatherElement.ValueKind == JsonValueKind.Object &&
                            weatherElement.TryGetProperty("windSpeed", out var windSpeedEl) &&
                            windSpeedEl.ValueKind == JsonValueKind.Number
                    ? windSpeedEl.GetDouble()
                    : 0.0,

                WindDirection = weatherElement.ValueKind == JsonValueKind.Object &&
                                weatherElement.TryGetProperty("windDirection", out var windDirectionEl)
                    ? windDirectionEl.GetString() ?? "—"
                    : "—",

                WindDirectionDegrees = weatherElement.ValueKind == JsonValueKind.Object &&
                                       weatherElement.TryGetProperty("windDirectionDegrees", out var windDirectionDegreesEl) &&
                                       windDirectionDegreesEl.ValueKind == JsonValueKind.Number
                    ? windDirectionDegreesEl.GetDouble()
                    : 0.0,

                Warning = warningElement.ValueKind == JsonValueKind.String
                    ? warningElement.GetString()
                    : null
            };

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting simulation status: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> StopSimulationAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/stop", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping simulation: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells, bool IsRunning, double FireArea, int CurrentStep, int Status)> ResetSimulationAsync(
        Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/reset", null);
            var responseText = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Reset response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                return (
                    false,
                    ReadMessage(doc, "Ошибка перезапуска"),
                    null,
                    false,
                    0,
                    0,
                    -1);
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

            return (
                true,
                ReadMessage(doc, "Симуляция перезапущена"),
                cells,
                isRunning,
                fireArea,
                currentStep,
                status);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting simulation: {ex.Message}");
            return (false, ex.Message, null, false, 0, 0, -1);
        }
    }

    public async Task<(bool Success, string Message, List<GraphCellDto>? Cells, SimulationGraphDto? Graph)> RefreshIgnitionSetupAsync(
        Guid simulationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/SimulationManager/{simulationId}/refresh-ignitions", null);
            var responseText = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Refresh-ignitions response: {responseText}");

            using JsonDocument doc = JsonDocument.Parse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                return (
                    false,
                    ReadMessage(doc, "Ошибка обновления очагов"),
                    null,
                    null);
            }

            List<GraphCellDto>? cells = null;
            if (doc.RootElement.TryGetProperty("cells", out var cellsElement))
            {
                cells = JsonSerializer.Deserialize<List<GraphCellDto>>(cellsElement.GetRawText(), _jsonOptions);
            }

            SimulationGraphDto? graph = null;
            if (doc.RootElement.TryGetProperty("graph", out var graphElement))
            {
                graph = JsonSerializer.Deserialize<SimulationGraphDto>(graphElement.GetRawText(), _jsonOptions);
            }

            return (
                true,
                ReadMessage(doc, "Очаги обновлены"),
                cells,
                graph);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing ignition setup: {ex.Message}");
            return (false, ex.Message, null, null);
        }
    }

    public async Task<bool> DeleteSimulationAsync(Guid simulationId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/simulations/{simulationId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting simulation: {ex.Message}");
            return false;
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

    private static string ReadMessage(JsonDocument doc, string fallback)
    {
        return doc.RootElement.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString() ?? fallback
            : fallback;
    }
}