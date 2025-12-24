# TechnicalAnalyzer

TechnicalAnalyzer is an application for collecting, persisting, and analyzing NEPSE (Nepal Stock Exchange) market data. It provides interactive OHLC charts, a suite of technical indicators, and tooling to prototype and evaluate trading signals. The project is designed for research and experimentation — not financial advice.

## Key features

- Fetches real-time NEPSE market data using an unofficial Python API server and stores OHLC data automatically into a local database.
- Computes common technical indicators (SMA, EMA, RSI, MACD) with user-adjustable parameters and overlays them on interactive charts.
- Background service that auto-saves incoming OHLC data into SQL Server (or any provider you configure for EF Core).
- Machine learning utilities to construct feature vectors, run simple classifiers, and evaluate trading signals.
- Runs inside an isolated Python virtual environment for the external API component; the .NET app consumes that service via HttpClient.

## Quick start

1. Clone the repository and open it in your IDE.
2. Prepare environment variables (recommended): create a file named `.env` in the project root and add your connection string and API base address using the `__` (double underscore) convention for nested config keys.

Example `.env`:

```
ConnectionStrings__DefaultConnection=Server=ServerName;Database=DbName;Trusted_Connection=True;TrustServerCertificate=True;
```

3. Start the external unofficial NEPSE API server (Python)

```ps
cd Python
python -m venv venv
venv\Scripts\activate     # Windows CMD
pip install -r requirements.txt
python nepse_server.py      # or use nepse-cli --start-server if available
```

4. Run the .NET application

```ps
dotnet run --project TechnicalAnalyzer.csproj
```

The application will start and — if the background worker is enabled — begin fetching and persisting OHLC data automatically.

## Machine learning & signal testing

This project includes basic machine learning tools intended to assist experimentation and backtesting of trading ideas:

- Feature generation: constructs time-series feature vectors from price data and indicator values (SMA, EMA, RSI, MACD histogram, etc.).
- Classifiers: simple K-Nearest Neighbors and a Random Forest filter are provided as an initial exploration for signal filtering and ranking.
- Evaluation: the code includes backtest utilities to measure simple metrics (returns, win-rate) on historical signals.

Important: these algorithms are provided as research tools. They are not tuned for live trading and should be used to test indicator hypotheses rather than to make production trading decisions.

## Notes on reliability and robustness

- The background fetcher intentionally swallows per-ticker exceptions to keep the service running; check logs for failed downloads.
- Use the `.env` file locally for convenience, but set production secrets in your deployment environment.

## Configuration and customization

- Connection strings and other settings can come from `appsettings.json`, environment variables, or a `.env` file loaded at startup.
- Indicator periods (SMA/EMA/RSI) are passed from controller parameters and can be adjusted in the UI.
- The background interval for auto-saving can be adjusted in `Services/OhlcBackgroundService.cs` or converted to a configurable setting.

## Troubleshooting

- If the app logs `No connection could be made because the target machine actively refused it`, the external Python server is not running or not reachable at the configured address.
- Verify your database connection string and apply EF Core migrations if necessary:

```ps
dotnet ef database update
```

- If you want to prevent the background worker from starting while debugging, simply comment out the registration of the hosted service in `Program.cs`:

```csharp
// builder.Services.AddHostedService<OhlcBackgroundService>();
```

## Contributing

Contributions are welcome. If you submit changes, include a description of the problem being solved, tests or steps to validate, and keep commits focused and small.
