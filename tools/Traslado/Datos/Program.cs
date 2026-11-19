using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Npgsql;

// Archivo de intercambio privado. Nunca se incluye en Git ni en la entrega académica.
string[] tablas = ["usuarios","zonas","comunidades","contactos","contacto_canales","usuario_roles","usuario_bitacora","contacto_historial","avisos","versiones_mensaje","listas_destinos","destinos_envio","comunidades_pendientes"];
if (args.Length != 2 || args[0] is not ("exportar" or "importar"))
{ Console.Error.WriteLine("Uso: dotnet run --project tools/Traslado/Datos -- exportar|importar ruta.transfer.json"); return 2; }
try
{
    if (args[0] == "exportar")
    {
        if (File.Exists(args[1])) throw new InvalidOperationException("El archivo de destino ya existe.");
        await using var origen = new NpgsqlConnection(Environment.GetEnvironmentVariable("ALERTAS_POSTGRES") ?? throw new InvalidOperationException("Falta ALERTAS_POSTGRES."));
        await origen.OpenAsync();
        await using var tx = await origen.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await new NpgsqlCommand("SET TRANSACTION READ ONLY",origen,tx).ExecuteNonQueryAsync();
        var paquete = new Paquete(1,DateTimeOffset.UtcNow,[]);
        foreach(var tabla in tablas)
        {
            var cols=new List<Columna>();
            await using(var meta=new NpgsqlCommand("SELECT column_name, udt_name FROM information_schema.columns WHERE table_schema='public' AND table_name=@tabla ORDER BY ordinal_position",origen,tx))
            {
                meta.Parameters.AddWithValue("tabla",tabla);await using var rd=await meta.ExecuteReaderAsync();
                while(await rd.ReadAsync())cols.Add(new(rd.GetString(0),rd.GetString(1)));
            }
            if(cols.Count==0)throw new InvalidOperationException($"Falta la tabla {tabla}; se requiere el esquema completo de sprint-2.");
            string Expr(Columna c) => c.Tipo=="geometry" ? $"CASE WHEN {Pg(c.Nombre)} IS NULL THEN NULL ELSE json_build_object('wkb',encode(ST_AsBinary({Pg(c.Nombre)}),'hex'),'srid',ST_SRID({Pg(c.Nombre)}))::text END AS {Pg(c.Nombre)}" : Pg(c.Nombre);
            var filas=new List<Dictionary<string,JsonElement>>();
            await using(var cmd=new NpgsqlCommand($"SELECT {string.Join(',',cols.Select(Expr))} FROM public.{Pg(tabla)}",origen,tx))
            {
                await using var rd=await cmd.ExecuteReaderAsync();
                while(await rd.ReadAsync())
                {
                    var fila=new Dictionary<string,JsonElement>();
                    for(int i=0;i<cols.Count;i++)
                    {
                        object? v=await rd.IsDBNullAsync(i)?null:rd.GetValue(i);
                        if(v is not null && cols[i].Tipo is "geometry" or "jsonb" or "json") fila[cols[i].Nombre]=JsonSerializer.Deserialize<JsonElement>((string)v);
                        else fila[cols[i].Nombre]=JsonSerializer.SerializeToElement(v);
                    }
                    filas.Add(fila);
                }
            }
            paquete.Tablas.Add(new(tabla,cols,filas));Console.WriteLine($"Exportada {tabla}: {filas.Count}");
        }
        await tx.CommitAsync();
        await using var salida=new FileStream(args[1],FileMode.CreateNew,FileAccess.Write,FileShare.None);
        await JsonSerializer.SerializeAsync(salida,paquete);await salida.FlushAsync();
        Console.WriteLine("Exportación consistente completada. Guarda el archivo fuera del repositorio y transfiérelo por un canal privado.");
    }
    else
    {
        await using var entrada=File.OpenRead(args[1]);
        var paquete=await JsonSerializer.DeserializeAsync<Paquete>(entrada)??throw new InvalidOperationException("Archivo vacío.");
        if(paquete.Version!=1||!paquete.Tablas.Select(t=>t.Nombre).Order().SequenceEqual(tablas.Order()))throw new InvalidOperationException("Versión o tablas de intercambio no válidas.");
        await using var destino=new SqlConnection(Environment.GetEnvironmentVariable("ALERTAS_SQLSERVER")??throw new InvalidOperationException("Falta ALERTAS_SQLSERVER."));
        await destino.OpenAsync();
        await using var tx=(SqlTransaction)await destino.BeginTransactionAsync(IsolationLevel.Serializable);
        async Task Exec(string sql){await using var cmd=new SqlCommand(sql,destino,tx){CommandTimeout=120};await cmd.ExecuteNonQueryAsync();}
        var metadatos=new Dictionary<string,Dictionary<string,(string Tipo,bool Identidad)>>();
        // Toda validación de esquema/vacío ocurre antes de insertar.
        foreach(var t in paquete.Tablas)
        {
            await using var cuenta=new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.{Sql(t.Nombre)} WITH (TABLOCKX,HOLDLOCK)",destino,tx);
            if(Convert.ToInt64(await cuenta.ExecuteScalarAsync())!=0)throw new InvalidOperationException($"La tabla {t.Nombre} no está vacía. Importa en una base nueva, sin semilla.");
            var columnas=new Dictionary<string,(string,bool)>();
            await using(var cmd=new SqlCommand("SELECT c.name,ty.name,c.is_identity FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID(@tabla)",destino,tx))
            {cmd.Parameters.AddWithValue("tabla","dbo."+t.Nombre);await using var rd=await cmd.ExecuteReaderAsync();while(await rd.ReadAsync())columnas.Add(rd.GetString(0),(rd.GetString(1),rd.GetBoolean(2)));}
            if(!columnas.Keys.Order().SequenceEqual(t.Columnas.Select(c=>c.Nombre).Order()))throw new InvalidOperationException($"El esquema de {t.Nombre} no coincide. No se descartan columnas.");
            if(t.Filas.Any(f=>!f.Keys.Order().SequenceEqual(columnas.Keys.Order())))throw new InvalidOperationException("Fila incompleta: "+t.Nombre);
            metadatos[t.Nombre]=columnas;
        }
        // Las referencias circulares se validan juntas antes del COMMIT; cualquier fallo revierte todo.
        foreach(var t in paquete.Tablas)await Exec($"ALTER TABLE dbo.{Sql(t.Nombre)} NOCHECK CONSTRAINT ALL");
        foreach(var t in paquete.Tablas)
        {
            var meta=metadatos[t.Nombre];bool identidad=meta.Values.Any(c=>c.Identidad);
            if(identidad)await Exec($"SET IDENTITY_INSERT dbo.{Sql(t.Nombre)} ON");
            foreach(var fila in t.Filas)
            {
                await using var cmd=new SqlCommand{Connection=destino,Transaction=tx};var values=new List<string>();
                for(int i=0;i<t.Columnas.Count;i++)
                {
                    var c=t.Columnas[i];var v=fila[c.Nombre];var tipo=meta[c.Nombre].Tipo;string p="@p"+i;
                    if(tipo=="geometry")
                    {
                        if(v.ValueKind==JsonValueKind.Null){values.Add("NULL");continue;}
                        int srid=v.GetProperty("srid").GetInt32();if(srid!=4326)throw new InvalidOperationException("Geometría con SRID distinto de 4326.");
                        cmd.Parameters.Add(p,SqlDbType.VarBinary,-1).Value=Convert.FromHexString(v.GetProperty("wkb").GetString()!);
                        values.Add($"geometry::STGeomFromWKB({p},4326)");continue;
                    }
                    values.Add(p);object value=Valor(v,tipo,c.Tipo);cmd.Parameters.AddWithValue(p,value);
                }
                cmd.CommandText=$"INSERT INTO dbo.{Sql(t.Nombre)} ({string.Join(',',t.Columnas.Select(c=>Sql(c.Nombre)))}) VALUES ({string.Join(',',values)})";
                await cmd.ExecuteNonQueryAsync();
            }
            if(identidad)await Exec($"SET IDENTITY_INSERT dbo.{Sql(t.Nombre)} OFF");
        }
        foreach(var t in paquete.Tablas)await Exec($"ALTER TABLE dbo.{Sql(t.Nombre)} WITH CHECK CHECK CONSTRAINT ALL");
        // Reconciliación por todas las columnas; geometrías comparadas por WKB + SRID.
        foreach(var t in paquete.Tablas)
        {
            var meta=metadatos[t.Nombre];var expresiones=t.Columnas.Select(c=>meta[c.Nombre].Tipo=="geometry"?$"{Sql(c.Nombre)}.STAsBinary() AS {Sql(c.Nombre)}":Sql(c.Nombre));
            var actual=new List<string>();
            await using(var cmd=new SqlCommand($"SELECT {string.Join(',',expresiones)} FROM dbo.{Sql(t.Nombre)}",destino,tx))
            {await using var rd=await cmd.ExecuteReaderAsync();while(await rd.ReadAsync()){var valores=new List<string>();for(int i=0;i<t.Columnas.Count;i++){var c=t.Columnas[i];valores.Add(Canon(await rd.IsDBNullAsync(i)?DBNull.Value:rd.GetValue(i),meta[c.Nombre].Tipo,c.Tipo));}actual.Add(Hash(valores));}}
            var esperado=t.Filas.Select(f=>Hash(t.Columnas.Select(c=>meta[c.Nombre].Tipo=="geometry"?f[c.Nombre].ValueKind==JsonValueKind.Null?"null":f[c.Nombre].GetProperty("wkb").GetString()!.ToUpperInvariant():Canon(Valor(f[c.Nombre],meta[c.Nombre].Tipo,c.Tipo),meta[c.Nombre].Tipo,c.Tipo)))).Order().ToList();
            if(!actual.Order().SequenceEqual(esperado))throw new InvalidOperationException($"Reconciliación fallida: {t.Nombre}. Se revierte toda la importación.");
            Console.WriteLine($"Verificada {t.Nombre}: {actual.Count} filas, contenido y relaciones.");
        }
        await tx.CommitAsync();Console.WriteLine("Importación confirmada. Origen sin cambios. Verifica el login y los mapas antes de cambiar de entorno.");
    }
    return 0;
}
catch(Exception ex){Console.Error.WriteLine($"Traslado interrumpido ({ex.GetType().Name}). No se confirma la transacción de importación. Revisa localmente esquema, datos y conexión; no publiques archivos ni credenciales.");Console.Error.WriteLine(ex is InvalidOperationException?ex.Message:"El proveedor rechazó la operación. Comprueba permisos, restricciones y conectividad.");return 1;}
static string Pg(string n)=>"\""+n.Replace("\"","\"\"")+"\"";
static string Sql(string n)=>"["+n.Replace("]","]]")+"]";
static object Valor(JsonElement v,string tipo,string origen)
{
    if(v.ValueKind==JsonValueKind.Null)return DBNull.Value;
    return tipo switch {"uniqueidentifier"=>v.GetGuid(),"bit"=>v.GetBoolean(),"int"=>v.GetInt32(),"bigint"=>v.GetInt64(),"datetimeoffset"=>v.GetDateTimeOffset(),"datetime2"=>v.GetDateTime(),"date"=>DateTime.Parse(v.GetString()!,CultureInfo.InvariantCulture),"float"=>v.GetDouble(),"decimal"=>v.GetDecimal(),_=>origen is "jsonb" or "json"||origen.StartsWith('_')?v.GetRawText():v.GetString()??v.GetRawText()};
}
static string Canon(object v,string tipo,string origen)
{
    if(v is DBNull)return "null";
    if(v is byte[] bytes)return Convert.ToHexString(bytes);
    if(v is Guid guid)return guid.ToString();
    if(v is DateTimeOffset d)return d.ToUniversalTime().ToString("O",CultureInfo.InvariantCulture);
    if(v is DateTime dt)return dt.ToString(tipo=="date"?"yyyy-MM-dd":"O",CultureInfo.InvariantCulture);
    if(v is bool b)return b?"true":"false";
    if(origen is "jsonb" or "json"||origen.StartsWith('_'))return JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(v.ToString()!));
    return Convert.ToString(v,CultureInfo.InvariantCulture)??"";
}
static string Hash(IEnumerable<string> values)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));
record Columna(string Nombre,string Tipo);
record Tabla(string Nombre,List<Columna> Columnas,List<Dictionary<string,JsonElement>> Filas);
record Paquete(int Version,DateTimeOffset ExportadoEn,List<Tabla> Tablas);
