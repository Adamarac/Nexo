# Baixa um mês real de carteiras e o cadastro da CVM e recorta só as linhas de alguns fundos.
# As amostras ficam em Latin-1, como na fonte, para os testes exercitarem a codificação real.
param(
    [string]$Mes = "202609",
    [string]$Destino = (Join-Path $PSScriptRoot "..\tests\fixtures\cvm")
)

$ErrorActionPreference = "Stop"
$latin1 = [System.Text.Encoding]::GetEncoding(28591)

# Fundos escolhidos porque juntos cobrem todos os blocos, sigilo, duplicidade e estruturados.
$fundos = @(
    "00.017.024/0001-53", # blocos 1 e 8
    "00.807.777/0001-62", # blocos 1, 2 e 6
    "00.102.322/0001-41", # bloco 4 (ações)
    "02.668.201/0001-97", # blocos 3 (swap) e 7 (exterior)
    "00.598.452/0001-17", # bloco 5 (CDB)
    "35.591.513/0001-07", # mesma debênture em duas linhas (negociação e vencimento)
    "00.180.995/0001-10", # carteira sob sigilo
    "10.947.525/0001-92", # estruturado (cda_fie)
    "12.358.507/0001-09"  # estruturado (cda_fie)
)
$digitos = $fundos | ForEach-Object { $_ -replace "\D", "" }

$trabalho = Join-Path ([System.IO.Path]::GetTempPath()) "nexo-fixtures"
New-Item -ItemType Directory -Force $trabalho | Out-Null

function Baixar([string]$url, [string]$arquivo) {
    $caminho = Join-Path $trabalho $arquivo
    if (-not (Test-Path $caminho)) {
        Write-Host "Baixando $url"
        Invoke-WebRequest -Uri $url -OutFile $caminho -UseBasicParsing
    }
    return $caminho
}

function Extrair([string]$zip) {
    $pasta = $zip -replace "\.zip$", ""
    if (-not (Test-Path $pasta)) { Expand-Archive -Path $zip -DestinationPath $pasta }
    return $pasta
}

# Mantém o cabeçalho e as linhas cujo campo da coluna indicada está na lista.
function Recortar([string]$origem, [string]$destino, [int]$coluna, [string[]]$valores) {
    $conjunto = New-Object 'System.Collections.Generic.HashSet[string]' (, [string[]]$valores)
    $linhas = New-Object System.Collections.Generic.List[string]
    $primeira = $true
    foreach ($linha in [System.IO.File]::ReadLines($origem, $latin1)) {
        if ($primeira) { $linhas.Add($linha); $primeira = $false; continue }
        $campos = $linha.Split(";")
        if ($campos.Length -gt $coluna -and $conjunto.Contains($campos[$coluna])) { $linhas.Add($linha) }
    }
    [System.IO.File]::WriteAllText($destino, (($linhas -join "`r`n") + "`r`n"), $latin1)
    return $linhas.Count - 1
}

$cda = Extrair (Baixar "https://dados.cvm.gov.br/dados/FI/DOC/CDA/DADOS/cda_fi_$Mes.zip" "cda_fi_$Mes.zip")
$cad = Extrair (Baixar "https://dados.cvm.gov.br/dados/FI/CAD/DADOS/registro_fundo_classe.zip" "registro_fundo_classe.zip")

$pastaCda = Join-Path $Destino "cda_fi_$Mes"
$pastaCad = Join-Path $Destino "registro_fundo_classe"
New-Item -ItemType Directory -Force $pastaCda, $pastaCad | Out-Null

$contagens = [ordered]@{}
foreach ($csv in Get-ChildItem $cda -Filter *.csv) {
    $contagens[$csv.Name] = Recortar $csv.FullName (Join-Path $pastaCda $csv.Name) 1 $fundos
}

# No cadastro o CNPJ vem sem pontuação; a classe aponta para o fundo pelo ID de registro.
$contagens["registro_fundo.csv"] = Recortar (Join-Path $cad "registro_fundo.csv") (Join-Path $pastaCad "registro_fundo.csv") 1 $digitos
$contagens["registro_classe.csv"] = Recortar (Join-Path $cad "registro_classe.csv") (Join-Path $pastaCad "registro_classe.csv") 2 $digitos
$contagens["registro_subclasse.csv"] = Recortar (Join-Path $cad "registro_subclasse.csv") (Join-Path $pastaCad "registro_subclasse.csv") 0 @()

$contagens | ConvertTo-Json | Set-Content -Path (Join-Path $Destino "manifest.json") -Encoding UTF8
Write-Host "Amostras gravadas em $Destino"
$contagens.GetEnumerator() | ForEach-Object { Write-Host ("{0,-32} {1,6} linhas" -f $_.Key, $_.Value) }
