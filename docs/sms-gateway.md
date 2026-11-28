# Enviar SMS reales con un celular Android (SMS Gateway)

Twilio no da cuentas de prueba en Bolivia. Para que los SMS de la demo lleguen de verdad, el sistema puede usar un celular Android con la app **SMS Gateway for Android** (código abierto, https://sms-gate.app): el celular recibe el pedido de la API y manda el SMS con su propio chip y su saldo.

> **Importante: el celular y la computadora que corre la API tienen que estar en la misma red Wi‑Fi.** Si están en redes distintas (por ejemplo, el celular con datos móviles o en otra Wi‑Fi), la API no lo alcanza y todos los SMS quedan en reintento. Comprueben que las dos direcciones empiecen igual (por ejemplo, `192.168.1.x`). Si no se puede, usen el modo nube (al final de la sección 1).

En el código es un adaptador más del canal SMS (`SmsGatewayEmisor`, patrón Adapter), junto al simulador y a Twilio. Se elige con `Sms:Modo = SmsGateway`. Pasa por los mismos decoradores que los demás: bitácora y modo prueba (un simulacro nunca sale por el celular). Los reintentos de RNF-08 los maneja la cola.

## 1. Preparar el celular

1. Instala **SMS Gateway for Android**, de preferencia desde Google Play. El APK de https://github.com/capcom6/android-sms-gateway/releases puede ser bloqueado por Play Protect: en ese caso, pausen Play Protect solo para instalarla y vuelvan a activarlo.
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
4. **Seguimiento:** el SMS pasa a **Enviado** cuando el celular confirma que lo mandó, lo que puede tardar hasta un minuto. En el celular de la pasarela, el mensaje aparece en la app con su estado.

## Cómo sabe el sistema que el SMS salió

Que el celular acepte el pedido no quiere decir que el SMS salió: el chip o la operadora pueden rechazarlo después. Por eso, después de enviarlo, el sistema consulta al celular (`GET {Url}/message/{id}`) cada 3 segundos, hasta 90:

| Lo que informa el celular | Qué hace el sistema |
|---|---|
| `Delivered` | Enviado. |
| `Sent`, y sigue así 25 segundos | Enviado. En la prueba real, un `Sent` pasó a `Failed` 15 segundos después, por eso se espera ese margen. |
| `Failed` | Fallo pasajero con el error del chip (por ejemplo, `RESULT_ERROR_GENERIC_FAILURE`). La cola reintenta a los 60 s, como máximo 2 veces, y si sigue fallando queda **Fallido** con el motivo en Seguimiento. |
| Nada definitivo en 90 segundos | Fallo pasajero: "el celular no confirmó el envío a tiempo". |

Los tiempos se ajustan con `Sms:SmsGateway:TiempoConfirmacionSegundos`, `IntervaloConsultaSegundos` y `MargenTrasEnviadoSegundos`. Mientras espera la confirmación, cada SMS ocupa uno de los 10 envíos simultáneos (`Sms:EnviosSimultaneos`). Para la demo no se nota, pero con cientos de contactos el lote tarda más, y hay que medirlo contra la meta de 5 minutos (RNF-02).

## El texto del SMS va sin tildes

Una sola tilde obliga al celular a usar otra codificación (UCS-2): el mensaje se parte cada 70 caracteres. En la prueba real, el chip rechazó la alerta partida, y el mismo texto sin tildes llegó. Por eso el SMS sale sin tildes, sin ñ y sin emojis ("río" pasa a "rio", "Diríjase" a "Dirijase"). Así una alerta de hasta 160 caracteres cabe en un solo mensaje. Si es más larga, se manda en varias partes, sin cortar la instrucción. El nivel sigue escrito en palabras ("ALERTA NARANJA"). WhatsApp, el correo, la web, la radio y el altavoz conservan las tildes.

## Qué pasa si algo falla

| Situación | Qué hace el sistema |
|---|---|
| El celular está apagado, sin red o fuera de la Wi‑Fi | Fallo pasajero: reintenta a los 60 s, como máximo 2 veces (RNF-08). |
| El celular acepta el pedido pero el chip o la operadora lo rechazan (`Failed`) | Fallo pasajero con el error del chip: reintenta, y si sigue fallando queda Fallido con el motivo visible. Ya no se muestra como "Enviado". |
| El celular acepta pero no informa el resultado en 90 s | Fallo pasajero: reintenta. |
| La app responde 429 o 5xx | Fallo pasajero: igual que arriba. |
| Usuario o contraseña incorrectos (401), o un pedido inválido (400) | Fallo definitivo: no reintenta. El error queda en Seguimiento. |
| El celular no responde en 15 s (`Sms:SmsGateway:TiempoEsperaSegundos`) | Fallo pasajero. |

**Límites:** el envío depende del saldo y del plan del chip, y de que la operadora no bloquee envíos masivos. Para la demo alcanza; para producción hace falta un proveedor con convenio.
