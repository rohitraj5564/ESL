$env:PATH = "C:\Users\deept\AppData\Local\nvm\v20.18.0;$env:PATH"
Write-Host "Using Node: $(node -v)" -ForegroundColor Green
Write-Host "Starting ESL Dashboard..." -ForegroundColor Cyan
npm start
