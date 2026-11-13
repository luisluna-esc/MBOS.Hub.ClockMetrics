# Despliegue de ClockMetrics

ClockMetrics está en el mismo servidor que InvenTrack y FundTrack (`148.230.73.226`, Ubuntu, Docker) y
se abre en **http://148.230.73.226:8100**.

## Cómo está armado

| Pieza | Dónde | Detalle |
|---|---|---|
| Frontend | contenedor `clockmetrics-frontend` | Angular servido por nginx; pasa `/api` al backend. Puerto local `127.0.0.1:5101`. |
| Backend | contenedor `clockmetrics-backend` | .NET 10. Puerto local `127.0.0.1:5100`. Red Docker `clockmetrics-net`. |
| Base de datos | PostgreSQL del servidor | Base `clockmetrics`, usuario `clockmetrics`. El contenedor entra por `host.docker.internal`. |
| Configuración | `/root/clockmetrics-backend.env` | Cadena de conexión, clave JWT y CORS. Solo root la puede leer. **No va a Git.** |
| Entrada pública | nginx del servidor, `/etc/nginx/sites-available/clockmetrics.conf` | Escucha en el 8100 y pasa todo a `127.0.0.1:5101`. |
| Código | `/opt/clockmetrics/backend` y `/opt/clockmetrics/frontend` | Clones de GitHub, desde donde se construyen las imágenes. |

Puertos ocupados en el servidor (para no chocar al agregar otro sistema): InvenTrack 8080 / 5080-5081,
FundTrack 8090 / 5090-5091, Música 8092 / 5092, ClockMetrics 8100 / 5100-5101.

## Actualizar a una nueva versión

1. Sube los cambios a GitHub (backend y frontend).
2. Si hay cambios de base de datos, primero haz un respaldo y después aplica el script:

   ```bash
   sudo -u postgres pg_dump -Fc clockmetrics > /root/clockmetrics_antes_$(date +%Y%m%d_%H%M).dump
   export PGPASSWORD=$(grep ^ConnectionStrings /root/clockmetrics-backend.env | sed 's/.*Password=//')
   psql -h 127.0.0.1 -U clockmetrics -d clockmetrics -v ON_ERROR_STOP=1 -f /opt/clockmetrics/backend/Scripts/<script>.sql
   ```

   `storedProcedures.sql` y `seedData.sql` se pueden volver a correr sin problema; `dataBase.sql` no
   (crea las tablas desde cero): los cambios de tablas van en un `migration_XX_*.sql` nuevo.

3. Actualiza con un solo comando (baja lo último, construye las imágenes y cambia los contenedores):

   ```bash
   bash /opt/clockmetrics/backend/deploy.sh
   ```

4. Revisa que esté bien:

   ```bash
   docker ps | grep clockmetrics
   docker logs --tail 30 clockmetrics-backend
   curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8100/
   ```

   El aviso `Cannot load library libgssapi_krb5.so.2` en el log es normal (librería opcional de Kerberos).

## Respaldos

```bash
sudo -u postgres pg_dump -Fc clockmetrics > /root/clockmetrics_$(date +%Y%m%d_%H%M).dump
```

Las marcas originales del biométrico quedan en la base (`attendance_raw_day`), así que el respaldo de la
base es suficiente; conviene guardar también los `.xls` que exporta el reloj.
