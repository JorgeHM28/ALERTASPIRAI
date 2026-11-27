# Enviar SMS reales con un celular Android (SMS Gateway)

Twilio no da cuentas de prueba en Bolivia. Para que los SMS de la demo lleguen de verdad, el sistema puede usar un celular Android con la app **SMS Gateway for Android** (código abierto, https://sms-gate.app): el celular recibe el pedido de la API y manda el SMS con su propio chip y su saldo.

En el código es un adaptador más del canal SMS (`SmsGatewayEmisor`, patrón Adapter), junto al simulador y a Twilio. Se elige con `Sms:Modo = SmsGateway`. Pasa por los mismos decoradores que los demás: bitácora y modo prueba (un simulacro nunca sale por el celular). Los reintentos de RNF-08 los maneja la cola.

## 1. Preparar el celular

1. Instala **SMS Gateway for Android** desde Google Play, o el APK de https://github.com/capcom6/android-sms-gateway/releases.
2. Ábrela y dale permiso para **enviar SMS**.
3. Activa **Local Server** y toca **Start Service** (los cambios valen después de reiniciar el servicio).
4. En la sección *Local Server* la app muestra:
   - la **dirección local**, por ejemplo `192.168.1.50:8080`,
   - el **usuario** y la **contraseña** que genera la app.
5. El celular y la computadora con la API deben estar en la **misma red Wi‑Fi**. Si el celular entra en reposo, el servicio puede pausarse: conviene dejarlo cargando y con la pantalla activa durante la demo.

Si no se puede estar en la misma red, la app tiene un modo **Cloud Server**: hay que registrarse en la app, y la URL pasa a ser `https://api.sms-gate.app/3rdparty/v1`, con el usuario y la contraseña de la sección *Cloud Server*.

## 2. Configurar la API

### Si la API corre con `dotnet run`

Los datos van en los *user-secrets* de .NET (quedan en la computadora, no en el repositorio):

```bash
dotnet user-secrets set "Sms:Modo" "SmsGateway" --project src/SistemaAlertas.Api
dotnet user-secrets set "Sms:SmsGateway:Url" "http://192.168.1.50:8080" --project src/SistemaAlertas.Api
dotnet user-secrets set "Sms:SmsGateway:Usuario" "el-usuario-de-la-app" --project src/SistemaAlertas.Api
dotnet user-secrets set "Sms:SmsGateway:Contrasena" "la-contraseña-de-la-app" --project src/SistemaAlertas.Api
```

### Si la API corre con Docker (`docker compose up`)

Copia `.env.ejemplo` como `.env` en la raíz del proyecto, pon los datos de la app y reconstruye:

```bash
cp .env.ejemplo .env      # y edita los cuatro valores
docker compose up -d --build
```

El `.env` está en `.gitignore`: no se sube.

Si falta algún dato, la API no arranca y dice cuál falta. Para volver al simulador: `Sms:Modo = Simulador` (o `SMS_MODO=Simulador` en el `.env`).

## 3. Probar

1. **Comunidades:** abre una localidad del área del aviso (por ejemplo, Jorochito, en El Torno) y agrega tu contacto con el canal **SMS**, tu número en formato `+5917xxxxxxx`, **activo** y **autorizado** (procedencia y referencia, por ejemplo "Prueba del equipo").
2. **Avisos → Importar CAP** con `docs/ejemplos/sprint-3/01-alerta-naranja-el-torno.xml`. No uses el simulacro (06): por seguridad, un simulacro no sale por el celular.
3. **Mensajes:** con `operador`, prepara la versión, vincula un protocolo y envíala a revisión. Con `aprobador`, apruébala, revisa los destinos y **Difundir**.
4. **Seguimiento:** en unos segundos el SMS pasa a **Enviado**, con el código que devolvió la app. En el celular de la pasarela, el mensaje aparece en la app con su estado.

## Qué pasa si algo falla

| Situación | Qué hace el sistema |
|---|---|
| El celular está apagado, sin red o fuera de la Wi‑Fi | Fallo pasajero: reintenta a los 60 s, como máximo 2 veces (RNF-08). |
| La app responde 429 o 5xx | Fallo pasajero: igual que arriba. |
| Usuario o contraseña incorrectos (401), o un pedido inválido (400) | Fallo definitivo: no reintenta. El error queda en Seguimiento. |
| El celular no responde en 15 s (`Sms:SmsGateway:TiempoEsperaSegundos`) | Fallo pasajero. |

**Límites:** el envío depende del saldo y del plan del chip, y de que la operadora no bloquee envíos masivos. Para la demo alcanza; para producción hace falta un proveedor con convenio.
