﻿// In TradingConsole.Wpf/ViewModels/TradeSignalViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TradingConsole.Wpf.Services;

namespace TradingConsole.Wpf.ViewModels
{
    /// <summary>
    /// Represents the sentiment of a market factor.
    /// </summary>
    public enum FactorSentiment
    {
        Bullish,
        Bearish,
        Neutral
    }

    /// <summary>
    /// A view model for a single market factor to be displayed in the UI.
    /// </summary>
    public class FactorViewModel : INotifyPropertyChanged
    {
        private string _factorName = string.Empty;
        public string FactorName { get => _factorName; set { _factorName = value; OnPropertyChanged(); } }

        private string _factorValue = string.Empty;
        public string FactorValue { get => _factorValue; set { _factorValue = value; OnPropertyChanged(); } }

        private FactorSentiment _sentiment;
        public FactorSentiment Sentiment { get => _sentiment; set { _sentiment = value; OnPropertyChanged(); } }

        private string _stabilityText = string.Empty;
        public string StabilityText { get => _stabilityText; set { _stabilityText = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class TradeSignalViewModel : INotifyPropertyChanged
    {
        private AnalysisResult? _niftyAnalysisResult;
        public AnalysisResult? NiftyAnalysisResult { get => _niftyAnalysisResult; set { _niftyAnalysisResult = value; OnPropertyChanged(); } }

        public ObservableCollection<FactorViewModel> BullishFactors { get; } = new ObservableCollection<FactorViewModel>();
        public ObservableCollection<FactorViewModel> BearishFactors { get; } = new ObservableCollection<FactorViewModel>();


        public TradeSignalViewModel()
        {
            NiftyAnalysisResult = new AnalysisResult { Symbol = "Initializing..." };
        }

        public void UpdateSignalResult(AnalysisResult newResult)
        {
            if (newResult.InstrumentGroup != "INDEX")
            {
                return;
            }

            if (NiftyAnalysisResult?.Symbol == "Initializing..." || NiftyAnalysisResult?.SecurityId != newResult.SecurityId)
            {
                NiftyAnalysisResult = newResult;
            }
            else
            {
                NiftyAnalysisResult.Update(newResult);
            }

            UpdateFactorLists(NiftyAnalysisResult);
        }

        private void UpdateFactorLists(AnalysisResult result)
        {
            BullishFactors.Clear();
            BearishFactors.Clear();

            var allFactors = new List<FactorViewModel>();

            // --- NEW: Add the Micro-Flow signal to the factor list ---
            AddFactor(allFactors, "Micro-Flow (15s)", result.MicroFlowSignal, result.MicroFlowSignalStability, s => s.Contains("Buying") ? FactorSentiment.Bullish : s.Contains("Selling") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Market Structure & Context
            AddFactor(allFactors, "Multi-Day Structure", result.MarketStructure, result.MarketStructureStability, s => s.Contains("Up") ? FactorSentiment.Bullish : s.Contains("Down") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Opening Type", result.OpenTypeSignal, result.OpenTypeSignalStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Daily Bias", result.DailyBias, result.DailyBiasStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "vs. Yesterday's Profile", result.YesterdayProfileSignal, result.YesterdayProfileSignalStability, s => s.Contains("Above") || s.Contains("Lower Y-Value") ? FactorSentiment.Bullish : s.Contains("Below") || s.Contains("Upper Y-Value") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Initial Balance", result.InitialBalanceSignal, result.InitialBalanceSignalStability, s => s.Contains("Breakout") ? FactorSentiment.Bullish : s.Contains("Breakdown") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Price Action & Key Levels
            AddFactor(allFactors, "Price vs. VWAP", result.PriceVsVwapSignal, result.PriceVsVwapSignalStability, s => s.Contains("Above") ? FactorSentiment.Bullish : s.Contains("Below") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "VWAP Bands", result.VwapBandSignal, result.VwapBandSignalStability, s => s.Contains("Above") ? FactorSentiment.Bullish : s.Contains("Below") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "5m Candle Pattern", result.CandleSignal5Min, result.CandleSignal5MinStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Volume & Open Interest
            AddFactor(allFactors, "Volume", result.VolumeSignal, result.VolumeSignalStability, s => s.Contains("Burst") ? (result.LTP > result.Vwap ? FactorSentiment.Bullish : FactorSentiment.Bearish) : FactorSentiment.Neutral);
            AddFactor(allFactors, "Futures OI", result.OiSignal, result.OiSignalStability, s => s == "Long Buildup" || s == "Short Covering" ? FactorSentiment.Bullish : s == "Short Buildup" || s == "Long Unwinding" ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "Institutional Intent", result.InstitutionalIntent, result.InstitutionalIntentStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Volatility Dynamics
            AddFactor(allFactors, "IV Rank", $"{result.IvRank:F2}%", "", v => result.IvRank < 50 ? FactorSentiment.Bullish : FactorSentiment.Bearish); // Low IV is bullish for option buyers
            AddFactor(allFactors, "Intraday Volatility", result.AtrSignal5Min, result.AtrSignal5MinStability, s => s.Contains("Expanding") ? (result.LTP > result.Vwap ? FactorSentiment.Bullish : FactorSentiment.Bearish) : FactorSentiment.Neutral);
            AddFactor(allFactors, "IV Skew", result.IvSkewSignal, result.IvSkewSignalStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Momentum & Divergence
            AddFactor(allFactors, "5m RSI Divergence", result.RsiSignal5Min, result.RsiSignal5MinStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "5m OBV Divergence", result.ObvDivergenceSignal5Min, result.ObvDivergenceSignal5MinStability, s => s.Contains("Bullish") ? FactorSentiment.Bullish : s.Contains("Bearish") ? FactorSentiment.Bearish : FactorSentiment.Neutral);
            AddFactor(allFactors, "EMA Trend (5m/15m)", $"{result.EmaSignal5Min} / {result.EmaSignal15Min}", result.EmaSignal15MinStability, s => result.EmaSignal5Min == "Bullish Cross" && result.EmaSignal15Min == "Bullish Cross" ? FactorSentiment.Bullish : result.EmaSignal5Min == "Bearish Cross" && result.EmaSignal15Min == "Bearish Cross" ? FactorSentiment.Bearish : FactorSentiment.Neutral);

            // Populate the final lists
            foreach (var factor in allFactors.Where(f => f.Sentiment == FactorSentiment.Bullish))
            {
                BullishFactors.Add(factor);
            }
            foreach (var factor in allFactors.Where(f => f.Sentiment == FactorSentiment.Bearish))
            {
                BearishFactors.Add(factor);
            }
        }

        private void AddFactor(List<FactorViewModel> factors, string name, string value, string stabilityText, Func<string, FactorSentiment> sentimentEvaluator)
        {
            if (string.IsNullOrEmpty(value) || value == "N/A" || value == "Neutral" || value == "Building...")
                return;

            var sentiment = sentimentEvaluator(value);
            if (sentiment != FactorSentiment.Neutral)
            {
                factors.Add(new FactorViewModel { FactorName = name, FactorValue = value, StabilityText = stabilityText, Sentiment = sentiment });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}