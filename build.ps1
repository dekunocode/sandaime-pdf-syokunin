param(
    [ValidateSet('Build', 'Test', 'Publish')]
    [string]$Action = 'Publish'
)

# dotnet の作業用フォルダー（.dotnet）はリポジトリ直下に作る（.gitignore 済み）
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
$solution = Join-Path $PSScriptRoot 'PdfSyokuninAvalonia.slnx'

dotnet restore $solution -p:Configuration=Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Action -eq 'Build') {
    dotnet build $solution -c Release --no-restore
} elseif ($Action -eq 'Test') {
    dotnet test $solution -c Release --no-restore
} else {
    dotnet test $solution -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet publish (Join-Path $PSScriptRoot 'PdfSyokuninAvalonia\PdfSyokuninAvalonia.csproj') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $publishDirectory = Join-Path $PSScriptRoot 'artifacts\publish'
    $publishedFiles = @(Get-ChildItem -LiteralPath $publishDirectory -File)
    if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].Name -ne '三代目PDF職人.exe') {
        throw "単一EXEの検証に失敗しました: $($publishedFiles.Name -join ', ')"
    }
}

exit $LASTEXITCODE
