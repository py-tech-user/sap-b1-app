# Script pour arrêter et relancer l'application SapB1App

Write-Host "🛑 Arrêt de l'application SapB1App en cours..." -ForegroundColor Yellow

# Trouver et arrêter tous les processus SapB1App.exe
$processes = Get-Process -Name "SapB1App" -ErrorAction SilentlyContinue

if ($processes) {
    Write-Host "   Processus trouvés: $($processes.Count)" -ForegroundColor Cyan
    $processes | ForEach-Object {
        Write-Host "   Arrêt du processus PID: $($_.Id)" -ForegroundColor Gray
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Seconds 2
    Write-Host "✅ Application arrêtée avec succès!" -ForegroundColor Green
} else {
    Write-Host "ℹ️  Aucun processus SapB1App en cours d'exécution" -ForegroundColor Gray
}

Write-Host ""
Write-Host "🔨 Nettoyage des fichiers de build..." -ForegroundColor Yellow
Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "🔧 Rebuild de la solution..." -ForegroundColor Yellow
dotnet build --configuration Debug

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Build réussie!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🚀 Voulez-vous lancer l'application maintenant? (O/N)" -ForegroundColor Cyan
    $response = Read-Host
    
    if ($response -eq "O" -or $response -eq "o") {
        Write-Host ""
        Write-Host "🚀 Lancement de l'application..." -ForegroundColor Green
        Write-Host "   URL: http://localhost:5000" -ForegroundColor Cyan
        Write-Host "   URL HTTPS: https://localhost:5001" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "📝 Surveillez les logs pour voir les messages de diagnostic:" -ForegroundColor Yellow
        Write-Host "   🔐 = Tentative de connexion" -ForegroundColor Gray
        Write-Host "   ✅ = Connexion réussie" -ForegroundColor Gray
        Write-Host "   ❌ = Erreur" -ForegroundColor Gray
        Write-Host ""
        dotnet run
    } else {
        Write-Host ""
        Write-Host "ℹ️  Pour lancer l'application manuellement, utilisez:" -ForegroundColor Cyan
        Write-Host "   dotnet run" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "❌ Erreur lors du build. Vérifiez les messages d'erreur ci-dessus." -ForegroundColor Red
    exit 1
}
