param([Security.SecureString]$Clave)
$ErrorActionPreference = 'Stop'
if (!$Clave) { $Clave = Read-Host 'Clave de la base de demostracion entregada por el equipo' -AsSecureString }
$password = [Net.NetworkCredential]::new('', $Clave).Password
if (!$password) { throw 'Se requiere la clave.' }
$cert = (Resolve-Path (Join-Path $PSScriptRoot 'docs/supabase-ca.crt')).Path
$builder = New-Object System.Data.Common.DbConnectionStringBuilder
$builder['Host'] = 'aws-0-us-east-2.pooler.supabase.com'
$builder['Port'] = '5432'
$builder['Database'] = 'postgres'
$builder['Username'] = 'pirai_docente.yfhialclxxapngdvlgmc'
$builder['Password'] = $password
$builder['SSL Mode'] = 'VerifyFull'
$builder['Root Certificate'] = $cert
$builder['Search Path'] = 'public,extensions'
$project = Join-Path $PSScriptRoot 'src/SistemaAlertas.Api/SistemaAlertas.Api.csproj'
$config = @{ 'ConnectionStrings:Alertas' = $builder.ConnectionString; 'BaseDeDatos:MigrarAlIniciar' = 'false' }
$previousEncoding = $OutputEncoding
try {
    $OutputEncoding = [Text.UTF8Encoding]::new($false)
    $config | ConvertTo-Json -Compress | dotnet user-secrets set --project $project
    if ($LASTEXITCODE) { throw 'No se pudo guardar la configuracion.' }
} finally { $OutputEncoding = $previousEncoding }
Write-Host 'Conexion guardada. Abre SistemaAlertas.sln, selecciona SistemaAlertas.Api y pulsa F5 con el perfil http.'
