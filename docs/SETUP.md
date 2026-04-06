# Setup Guide

## Prerequisites
- .NET 8 SDK
- Node.js 18+
- Python 3.10+ with pip
- MetaTrader 5 Terminal (for coverage collector)
- MT5 Manager API DLLs (for B-Book connector, Windows only)

## Installation

### Backend (C# .NET)
```bash
cd src/CoverageManager.Api
dotnet restore
dotnet build
```

### Frontend (React + Vite)
```bash
cd web
npm install
```

### Python Collector
```bash
cd collector
pip install -r requirements.txt
```

## Environment Variables
```bash
cp .env.example .env
```
See `.env.example` for all required variables. At minimum you need:
- `SUPABASE_URL` and `SUPABASE_KEY` for data persistence
- MT5 Manager credentials for B-Book connection
- MT5 Terminal credentials for coverage collector

## Running Locally

### 1. Start Backend (port 5000)
```bash
cd src/CoverageManager.Api
dotnet run
```

### 2. Start Frontend (port 5173)
```bash
cd web
npm run dev
```

### 3. Start Coverage Collector (port 8100)
```bash
cd collector
uvicorn main:app --host 0.0.0.0 --port 8100
```
Requires MT5 Terminal running with "Allow algorithmic trading" enabled.

### 4. Run Tests
```bash
dotnet test CoverageManager.sln
```

## Common Issues
- **Port 5000 in use**: Kill the existing process with `taskkill /F /PID <pid>` or change the port
- **Coverage showing 0**: Ensure the Python collector is running on port 8100 and MT5 Terminal is open
- **Symbol mismatch**: Check symbol mappings at `/api/mappings` — coverage symbols must map to B-Book canonical names
- **MT5 Manager DLLs missing**: Place MetaQuotes native DLLs in `src/CoverageManager.Connector/Libs/`
