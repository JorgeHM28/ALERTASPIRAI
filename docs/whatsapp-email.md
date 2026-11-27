# WhatsApp a un grupo y Email reales

Además del SMS (ver `docs/sms-gateway.md`), la difusión puede salir por:

- **WhatsApp a grupos:** un solo mensaje por alerta a un grupo (o a varios), en lugar de uno por número. Lleva el texto completo, con el nivel en palabras. Los vecinos se suman o se salen del grupo sin cargar sus números en el sistema.
- **Email:** un correo por contacto que tenga un canal Email en la lista de destinos, con el titular como asunto y el texto completo.

Los dos usan la misma cola que el SMS: un envío por grupo o por correo, reintentos a los 60 s solo ante fallos pasajeros (como máximo 2), cancelación si la versión fue reemplazada o venció, y su estado en **Seguimiento**. Un simulacro (`status` Exercise o Test) no sale por ninguno. Mientras falten datos de configuración, el canal queda apagado y la difusión no lo usa.

Radio, altavoz y redes sociales siguen siendo tareas manuales que una persona hace y confirma.

## WhatsApp con WAHA

[WAHA](https://waha.devlike.pro) (WhatsApp HTTP API) mantiene una sesión de WhatsApp Web vinculada a un número y la expone como API. **No es la API oficial de Meta**: va contra los términos de WhatsApp y el número podría ser bloqueado. Para la demo conviene usar un **número de prueba**, no el personal. La API oficial de Meta casi no permite escribir a grupos.

### 1. Levantar WAHA

En `.env` (copia de `.env.ejemplo`), inventa dos claves largas:

```
WAHA_API_KEY=una-clave-larga-inventada-por-ustedes
WAHA_DASHBOARD_CONTRASENA=otra-clave-para-el-panel
```

Y arranca WAHA (solo arranca con el perfil `whatsapp`):

```bash
docker compose --profile whatsapp up -d waha
```

En una Mac con chip Apple usa la imagen `devlikeapro/waha:arm` (es la de por defecto). En una PC Intel o AMD, agrega `WAHA_IMAGEN=devlikeapro/waha` al `.env`.

### 2. Vincular el número

1. Abre http://localhost:3000/dashboard (usuario `admin` y la contraseña de `WAHA_DASHBOARD_CONTRASENA`).
2. Inicia la sesión **default**. Cuando diga `SCAN_QR`, toca el ícono de la cámara.
3. En el celular del número de prueba: WhatsApp → **Dispositivos vinculados** → **Vincular un dispositivo**, y escanea el QR. La sesión pasa a `WORKING`.

### 3. Crear el grupo y encontrar su id

1. Desde ese número, crea el grupo (por ejemplo "Alertas río Piraí") y agrega a los integrantes del equipo.
2. Busca el id del grupo, que termina en `@g.us`:

```bash
curl -s -H "X-Api-Key: TU_WAHA_API_KEY" "http://localhost:3000/api/default/groups?exclude=participants"
```

3. Ponlo en el `.env`:

```
WHATSAPP_URL=http://waha:3000
WHATSAPP_GRUPO_NOMBRE=Alertas río Piraí
WHATSAPP_GRUPO_ID=120363012345678901@g.us
```

`WHATSAPP_URL` es `http://waha:3000` cuando la API corre en Docker. Si la API corre con `dotnet run`, usa user-secrets con `http://localhost:3000`:

```bash
dotnet user-secrets set "WhatsApp:Url" "http://localhost:3000" --project src/SistemaAlertas.Api
dotnet user-secrets set "WhatsApp:ApiKey" "TU_WAHA_API_KEY" --project src/SistemaAlertas.Api
dotnet user-secrets set "WhatsApp:Grupos:0:Nombre" "Alertas río Piraí" --project src/SistemaAlertas.Api
dotnet user-secrets set "WhatsApp:Grupos:0:Id" "120363012345678901@g.us" --project src/SistemaAlertas.Api
```

Para más de un grupo (por ejemplo, uno por municipio), agrega `WhatsApp:Grupos:1:...`, `WhatsApp:Grupos:2:...`. Cada alerta va a todos los grupos configurados.

## Email con Gmail

1. En la cuenta de Gmail del equipo, activa la **verificación en dos pasos**.
2. En https://myaccount.google.com/apppasswords crea una **contraseña de aplicación** (16 letras).
3. En el `.env`:

```
EMAIL_SERVIDOR=smtp.gmail.com
EMAIL_PUERTO=587
EMAIL_USUARIO=cuenta.del.equipo@gmail.com
EMAIL_CONTRASENA=la-contraseña-de-aplicacion
EMAIL_REMITENTE=cuenta.del.equipo@gmail.com
```

Con `dotnet run`, los mismos datos van en user-secrets como `Email:Servidor`, `Email:Puerto`, `Email:Usuario`, `Email:Contrasena` y `Email:Remitente`.

4. En **Comunidades**, agrega a un contacto el canal **Email** con su correo. Con el correo configurado, Email pasa a ser un canal aprobado y entra en la lista de destinos.

## Probar todo junto

1. `docker compose --profile whatsapp up -d --build` (o sin `--profile whatsapp` si no usan WhatsApp).
2. Importa `docs/ejemplos/sprint-3/01-alerta-naranja-el-torno.xml`, prepara la versión (`operador`), apruébala y **Difundir** (`aprobador`).
3. El mensaje de la difusión dice cuántos SMS, mensajes a grupos y correos se encolaron.
4. En **Seguimiento** cada envío aparece con su canal, su destino y su estado. El mensaje debe llegar al grupo y a los correos en unos segundos.

## Qué pasa si algo falla

| Situación | Qué hace el sistema |
|---|---|
| WAHA apagado, la sesión reconectando (5xx) o demasiados pedidos (429) | Fallo pasajero: reintenta a los 60 s, como máximo 2 veces. |
| Clave de WAHA incorrecta (401) o grupo inexistente (404, 422) | Fallo definitivo, con el motivo en Seguimiento. |
| Servidor de correo ocupado o sin conexión | Fallo pasajero. |
| Contraseña de aplicación incorrecta o dirección rechazada | Fallo definitivo. |
| Un canal sin configurar | La difusión no crea envíos por ese canal. |
