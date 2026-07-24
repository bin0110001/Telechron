@echo off
setlocal
title Telechron Launcher

echo ===================================================
echo             Telechron System Launcher              
echo ===================================================
echo Root Directory: %~dp0
echo.

rem 1. Check & Generate Dev Certificates if needed
if not exist "%~dp0certs\ca.crt" (
    echo [1/3] Dev certificates missing. Generating mTLS dev certs...
    powershell -ExecutionPolicy Bypass -File "%~dp0scripts\Generate-DevCerts.ps1"
    if errorlevel 1 (
        echo ERROR: Failed to generate dev certificates.
        pause
        exit /b 1
    )
) else (
    echo [1/3] Dev certificates verified in certs\
)

rem 2. Check Frontend dependencies
if not exist "%~dp0Frontend\node_modules" (
    echo [2/3] Frontend dependencies not found. Installing node_modules...
    cd /d "%~dp0Frontend"
    call npm install
    if errorlevel 1 (
        echo ERROR: Failed to install Frontend dependencies.
        pause
        exit /b 1
    )
    cd /d "%~dp0"
) else (
    echo [2/3] Frontend dependencies verified in Frontend\node_modules
)

echo [3/3] Launching Backend (Host), Agent, and Frontend UI...
echo.

rem Configure shared dev environment variables for child processes
set "TELECHRON_MTLS_CA_PATH=%~dp0certs\ca.crt"
set "TELECHRON_MTLS_HOST_CERT_PATH=%~dp0certs\host-server.pfx"
set "TELECHRON_MTLS_HOST_CERT_PASSWORD=telechron-dev-host"
set "TELECHRON_MTLS_AGENT_CERT_PATH=%~dp0certs\agent-dev.pfx"
set "TELECHRON_MTLS_AGENT_CERT_PASSWORD=telechron-dev-agent"
set "TELECHRON_JWT_SIGNING_KEY=telechron-dev-jwt-signing-key-32bytes-secret"
set "TELECHRON_AGENT_ENROLLMENT_TOKEN=telechron-dev-enrollment-token"
set "TELECHRON_ALLOWED_ORIGINS=http://localhost:5173,http://localhost:3000,http://127.0.0.1:5173"
set "TELECHRON_HOST_ADDRESS=https://localhost:5300"

rem Launch Backend (Host)
echo Starting Backend (Host) on http://localhost:5280 (REST) and https://localhost:5300 (gRPC)...
start "Telechron Backend (Host)" cmd /k "title Telechron Backend (Host) && cd /d "%~dp0Host" && dotnet run"

rem Brief pause for Host to bind ports
ping 127.0.0.1 -n 3 >nul

rem Launch Agent
echo Starting Agent connecting to Host at https://localhost:5300...
start "Telechron Agent" cmd /k "title Telechron Agent && cd /d "%~dp0Agent" && dotnet run"

rem Launch Frontend
echo Starting Frontend UI dev server...
start "Telechron Frontend" cmd /k "title Telechron Frontend && cd /d "%~dp0Frontend" && npm run dev"

echo.
echo ===================================================
echo All services launched successfully!
echo   - Backend (Host API): http://localhost:5280
echo   - Agent gRPC Server:  https://localhost:5300
echo   - Frontend Web App:   http://localhost:5173
echo ===================================================
echo.
