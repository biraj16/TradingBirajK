// TradingConsole.Wpf/Services/PerformanceService.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TradingConsole.Wpf.ViewModels;

namespace TradingConsole.Wpf.Services
{
    public class PnlDataPoint
    {
        public DateTime Timestamp { get; set; }
        public decimal Pnl { get; set; }
    }

    public class PerformanceService
    {
        private readonly PortfolioViewModel _portfolioViewModel;
        // --- FIX: Reference AnalysisService directly ---
        private readonly AnalysisService _analysisService;
        private readonly string _logFilePath;
        private static readonly object _fileLock = new object();

        // --- FIX: Constructor now accepts AnalysisService ---
        public PerformanceService(PortfolioViewModel portfolioViewModel, AnalysisService analysisService)
        {
            _portfolioViewModel = portfolioViewModel;
            _analysisService = analysisService; // Store the reference

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolderPath = Path.Combine(appDataPath, "TradingConsole", "PerformanceLogs");
            Directory.CreateDirectory(appFolderPath);

            _logFilePath = Path.Combine(appFolderPath, $"pnl_history_{DateTime.Now:yyyy-MM-dd}.json");

            InitializeLogFile();

            _portfolioViewModel.PropertyChanged += OnPortfolioPropertyChanged;
        }

        private void InitializeLogFile()
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);

            if (File.Exists(_logFilePath) && istNow.TimeOfDay < new TimeSpan(8, 0, 0))
            {
                try
                {
                    File.Delete(_logFilePath);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"[PerformanceService] Could not clear old log file: {ex.Message}");
                }
            }
        }

        private void OnPortfolioPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // --- THE FIX: Use the existing IsMarketOpen method from AnalysisService ---
            if (e.PropertyName == nameof(PortfolioViewModel.NetPnl) && _analysisService.IsMarketOpen())
            {
                var newDataPoint = new PnlDataPoint
                {
                    Timestamp = DateTime.Now,
                    Pnl = _portfolioViewModel.NetPnl
                };
                Task.Run(() => WriteDataPointToFile(newDataPoint));
            }
        }

        private void WriteDataPointToFile(PnlDataPoint dataPoint)
        {
            lock (_fileLock)
            {
                try
                {
                    string jsonString = JsonSerializer.Serialize(dataPoint);
                    File.AppendAllText(_logFilePath, jsonString + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerformanceService] Error writing to P&L log file: {ex.Message}");
                }
            }
        }

        public List<PnlDataPoint> LoadPnlHistory()
        {
            var history = new List<PnlDataPoint>();
            lock (_fileLock)
            {
                if (!File.Exists(_logFilePath))
                {
                    return history;
                }

                try
                {
                    string firstLine = File.ReadLines(_logFilePath).FirstOrDefault()?.Trim() ?? string.Empty;

                    if (firstLine.StartsWith("["))
                    {
                        string json = File.ReadAllText(_logFilePath);
                        var loadedHistory = JsonSerializer.Deserialize<List<PnlDataPoint>>(json);
                        if (loadedHistory != null)
                        {
                            history = loadedHistory;
                        }
                    }
                    else
                    {
                        var lines = File.ReadAllLines(_logFilePath);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            try
                            {
                                var dataPoint = JsonSerializer.Deserialize<PnlDataPoint>(line);
                                if (dataPoint != null)
                                {
                                    history.Add(dataPoint);
                                }
                            }
                            catch (JsonException ex)
                            {
                                Debug.WriteLine($"[PerformanceService] Skipped corrupted line in P&L log: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PerformanceService] Error reading P&L log file: {ex.Message}");
                }
            }
            return history;
        }

        public void Cleanup()
        {
            _portfolioViewModel.PropertyChanged -= OnPortfolioPropertyChanged;
        }
    }
}