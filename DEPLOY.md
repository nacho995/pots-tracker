# POTS PWA — Deploy guide (Fly.io + Neon + Resend)

Para v0.1. Asume macOS, `fly` CLI ya instalado en `~/.fly/bin/fly`.

## 1. Crear Postgres en Neon

1. Crea cuenta en https://neon.tech y un proyecto llamado `pots-tracker`. Región: cualquiera de Europa (Frankfurt, AWS eu-central-1).
2. Neon te da un **owner connection string**:
   `postgres://<owner>:<pwd>@<host>/neondb?sslmode=require`
3. En el panel de Neon → SQL Editor, ejecuta este SQL (cambia `<PASSWORD-AQUI>` por una clave fuerte que generes tú):

   ```sql
   CREATE ROLE pots_app WITH LOGIN PASSWORD '<PASSWORD-AQUI>';
   GRANT CONNECT ON DATABASE neondb TO pots_app;
   ```

   El resto de GRANTs los aplica la migración `EnableRowLevelSecurity` cuando la corras en el siguiente paso.

## 2. Aplicar migraciones (desde tu máquina, una vez)

```bash
cd /Users/nachodalesio/pots-pwa
dotnet tool restore
dotnet ef database update \
  --project src/Pots.Infrastructure \
  --startup-project src/Pots.Api \
  --connection "postgres://<owner>:<pwd>@<host>/neondb?sslmode=require"
```

Cuando termine, anota la **connection string de `pots_app`** (la misma host/db, pero usuario `pots_app` y password del paso 1):

```
Host=<host>;Database=neondb;Username=pots_app;Password=<PASSWORD-AQUI>;SslMode=Require
```

## 3. Crear cuenta en Resend

1. https://resend.com → registro.
2. Verifica tu dominio (o usa `onboarding@resend.dev` para pruebas — solo envía a tu propio email de registro).
3. Genera una **API key** en Settings → API Keys. La guardas para el paso 5.

## 4. Login en Fly y crear la app

```bash
fly auth login           # abre el navegador
fly apps create pots-tracker --org personal
```

## 5. Configurar secrets en Fly

Genera la signing key JWT (32 bytes base64):

```bash
openssl rand -base64 32
```

Después:

```bash
fly secrets set \
  ConnectionStrings__Pots='Host=<host>;Database=neondb;Username=pots_app;Password=<PASSWORD-AQUI>;SslMode=Require' \
  Jwt__Issuer='pots-tracker' \
  Jwt__Audience='pots-tracker' \
  Jwt__SigningKey='<lo-que-salio-de-openssl>' \
  Jwt__AccessTokenLifetimeMinutes='1440' \
  Jwt__MagicLinkBaseUrl='https://pots-tracker.fly.dev' \
  Resend__ApiKey='re_xxxxxxxxxxxxxxxx' \
  Resend__FromAddress='POTS <onboarding@resend.dev>' \
  -a pots-tracker
```

Cuando tengas un dominio verificado en Resend, cambia `Resend__FromAddress` a algo tipo `POTS <noreply@tu-dominio.com>`.

## 6. Desplegar

```bash
fly deploy
```

Fly construye la imagen Docker, la sube y arranca una VM en `cdg`. Cuando termine:

```bash
fly open    # abre https://pots-tracker.fly.dev en el navegador
```

## 7. Instalar la PWA en el móvil

Tu hermana abre `https://pots-tracker.fly.dev` en Chrome/Safari → "Añadir a pantalla de inicio". Se comporta como app instalada (offline-first, sin barra del navegador).

## Troubleshooting

- **`fly logs`** muestra los logs en vivo.
- Si el magic-link no llega: comprueba `fly logs` por errores de Resend (suelen ser dominio no verificado o `FromAddress` mal).
- Si la BD da error de permisos: la migración `EnableRowLevelSecurity` debe haberse aplicado. Re-ejecuta `dotnet ef database update`.
- Si la app no arranca: `fly logs` mostrará el `InvalidOperationException` del Program.cs indicando qué secret falta.
