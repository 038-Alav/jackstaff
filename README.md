# JACKSTAFF v0.1 — Fleet & Communications Operations Platform

JACKSTAFF es una plataforma modular y multiempresa para administrar activos de comunicación, su organización operativa y las solicitudes asociadas. Metro, Empresa X y Empresa Y aparecen como organizaciones de demostración independientes; sus nombres y slugs son datos de la tabla `tenants`, no ramas de código ni identidades globales del producto.

> v0.1 es un primer entregable funcional con inventario y workflow de tickets. Los estados de radio incluidos en la demo son datos ficticios. Esta versión todavía no se conecta a radios, dispatch consoles, GPS ni sistemas de fabricantes.

## 1. Ejecutar en Windows

Esta distribución se ejecuta directamente en Windows. PostgreSQL funciona como servicio local; la API y el frontend se ejecutan en procesos independientes de PowerShell.

### Requisitos

- Windows 10/11 de 64 bits.
- Node.js 22.12.0 o superior (Node.js 24 LTS también es compatible) y npm 10 o superior: [descarga oficial](https://nodejs.org/en/download/).
- .NET SDK 10.0.100 o superior dentro de la versión 10: [descarga oficial](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Instala el **SDK** (no basta con el Runtime); también puedes usar `winget install Microsoft.DotNet.SDK.10`. Si el lanzador informa que no hay un SDK compatible, ejecuta `dotnet --list-sdks` para comprobarlo y vuelve a abrir PowerShell después de instalarlo.
- PostgreSQL 17 de 64 bits: [instaladores para Windows](https://www.postgresql.org/download/windows/).
- PostGIS instalado para esa misma instancia de PostgreSQL. En Windows, StackBuilder se incluye con el instalador de PostgreSQL y permite instalar los paquetes espaciales; selecciona el paquete PostGIS estable compatible con PostgreSQL 17. Consulta la [guía de instalación de PostGIS para Windows](https://postgis.net/documentation/getting_started/install_windows/).

Durante la instalación de PostgreSQL, recuerda la contraseña que asignes al usuario administrador `postgres`. Mantén el puerto predeterminado `5432`.

### Preparación inicial de la base de datos

1. Comprueba que el servicio PostgreSQL esté iniciado desde **Servicios** de Windows (`services.msc`).
2. Abre PowerShell en la carpeta raíz `JACKSTAFF-v0.1`. Si Windows bloquea scripts locales por la política de ejecución, habilita scripts solo para esa ventana con `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`.
3. Ejecuta:

```powershell
.\scripts\Setup-Database.ps1
```

El script encuentra `psql.exe` en el `PATH` o en la carpeta habitual de PostgreSQL 17, solicita la contraseña de `postgres` sin guardarla y realiza lo siguiente:

- crea la base `jackstaff` si aún no existe;
- aplica `db/init/001_schema_and_demo.sql` una sola vez;
- aplica las migraciones pendientes de `db/migrations/` sin borrar los datos existentes;
- habilita PostGIS y carga las organizaciones y cuentas demo;
- prepara el rol local `jackstaff_app` que utiliza la API.

Si el esquema inicial ya está aplicado, el script conserva los datos existentes. Si PostGIS no está instalado para esa instancia, el script mostrará el error de PostgreSQL correspondiente.

### Iniciar la aplicación

Una vez preparada la base de datos, desde la raíz del proyecto ejecuta:

```powershell
.\Start-JACKSTAFF.ps1
```

El lanzador comprueba las versiones locales, valida la base de datos, restaura los paquetes de .NET, instala las dependencias web la primera vez y abre dos ventanas de PowerShell: una para la API y otra para Vite.

Abre:

- **Aplicación:** http://localhost:5173
- **API health:** http://localhost:8080/api/health
- **OpenAPI (Development):** http://localhost:8080/openapi/v1.json

Para detener la aplicación, presiona `Ctrl+C` en cada una de las dos ventanas de ejecución. El servicio PostgreSQL permanece activo y conserva los datos.

### Comandos alternativos de desarrollo

API, desde la carpeta `api`:

```powershell
dotnet restore Jackstaff.sln
dotnet test Jackstaff.sln
dotnet run --project Jackstaff.Api/Jackstaff.Api.csproj --launch-profile Jackstaff.Api
```

Frontend, desde la carpeta `web` y en otra terminal:

```powershell
npm install
npm run dev
```

Para ejecutar las comprobaciones de compilación y pruebas de ambos proyectos desde la raíz:

```powershell
.\scripts\check.ps1
```

El checklist funcional está en [`docs/acceptance-checklist.md`](docs/acceptance-checklist.md).

## 2. Restablecer los datos de demostración

Para eliminar completamente la base `jackstaff` y volver a crearla con el esquema y los datos iniciales, ejecuta:

```powershell
.\scripts\reset-demo.ps1
```

El script exige escribir `RESET`, solicita la contraseña de `postgres`, cierra las conexiones activas y reconstruye la base. **Esta operación elimina todos los datos locales de JACKSTAFF y no se puede deshacer.**

## 3. Accesos de demostración

Todos los usuarios demo usan la contraseña `JackstaffDemo!2026`.

| Cuenta | Rol | Espacio de trabajo |
|---|---|---|
| `admin@jackstaff.demo` | Tenant Admin | Metro, Empresa X y Empresa Y |
| `ops@jackstaff.demo` | Operator | Metro |
| `support@jackstaff.demo` | Platform Support | Soporte JACKSTAFF (acceso transversal a tickets) |

Todos usan la contraseña demo indicada arriba. La cuenta administradora sirve para comprobar el aislamiento al cambiar de organización. El inventario, grupos, instalaciones y tickets cambian con el tenant activo. La cuenta de soporte no pertenece a ningún tenant: tiene una membresía de plataforma propia y accede a la bandeja transversal de tickets.

## 4. Alcance funcional de v0.1

| Módulo | Funcionalidad disponible |
|---|---|
| Identidad | Login, JWT de acceso, membresías, roles y permisos |
| Multiempresa | Selector de workspace dinámico y autorización de membresía por tenant |
| Soporte de plataforma | Login independiente, bandeja transversal de tickets, comentarios e intervención del workflow |
| Instalaciones | Listado y creación de sitios genéricos por organización |
| Inventario | Modelos, radios, estado demo, instalación y etiqueta de activo |
| Grupos operativos | Creación y consulta de grupos |
| Asignaciones | Una asignación efectiva vigente por radio; historial de asignaciones |
| Mesa de ayuda | Creación de incidencias, mantenciones y cambios de grupo vinculados a activos/instalaciones |
| Seguimiento | Comentarios append-only por ticket, autor y fecha |
| Workflow | Revisión, aprobación, ejecución, resolución o rechazo |
| Auditoría | Registro de altas y transiciones relevantes |
| Dashboard | KPIs y tickets recientes del tenant seleccionado |

### Flujo de cambio de grupo

1. Un operador crea un ticket `group_change` con radio y grupo de destino.
2. La solicitud queda en `open` y después puede pasar a `in_review`.
3. Soporte puede aprobarla o iniciar su ejecución.
4. Solo al pasar de `in_progress` a `resolved`, la transacción cierra la asignación vigente e inserta la nueva.
5. El cambio de estado, la historia y la auditoría se guardan en la misma transacción.

El grupo solicitado no reemplaza al grupo efectivo mientras el ticket está abierto, en revisión, aprobado o en ejecución.

## 5. Arquitectura

```text
Browser
  └── React 19.3 + TypeScript + Vite (localhost:5173)
        └── HTTP / JSON
              └── ASP.NET Core 10 Web API (localhost:8080)
                    ├── Auth & Tenant Context
                    ├── Dashboard
                    ├── Asset Management
                    │     ├── Sites
                    │     ├── Radio Inventory
                    │     └── Operational Groups & Assignments
                    ├── Service Desk & Ticket Workflow
                    ├── Audit
                    ├── Provider Contracts (interfaces defined)
                    └── PostgreSQL 17 + PostGIS (localhost:5432)
```

La API es un **monolito modular**: módulos separados por capacidad de negocio, con un único proceso y base de datos. No se introducen microservicios, Kafka o Redis en esta etapa.

### Backend

- ASP.NET Core 10 Web API.
- Dapper + Npgsql para SQL explícito y transacciones claras.
- PostgreSQL 17 con extensión PostGIS habilitada.
- JWT de acceso y permisos por membresía.
- Un `TenantDbSession` por petición autenticada, con contexto tenant o contexto de staff de plataforma.
- PostgreSQL Row-Level Security en todas las tablas operacionales; el acceso global de plataforma usa políticas explícitas y permisos propios, no `BYPASSRLS`.
- Revalidación de cuenta y membresía del tipo de workspace en cada petición autenticada.
- Comentarios, historia de workflow y auditoría append-only para el rol de aplicación.

### Frontend

- React 19.3 y TypeScript.
- Vite 8 para desarrollo y build.
- Componentes por módulo; consumo HTTP centralizado en `src/api.ts`.
- La API se consume directamente desde `http://localhost:8080/api`.
- Interfaz responsive con navegación por workspace.

## 6. Estructura del repositorio

```text
JACKSTAFF-v0.1/
├── Start-JACKSTAFF.ps1          # Valida requisitos y lanza API + frontend
├── api/
│   ├── Jackstaff.Api/
│   │   ├── Infrastructure/       # Tenant DB session, JWT, hashing, audit
│   │   ├── Modules/
│   │   │   ├── Auth/
│   │   │   ├── Dashboard/
│   │   │   ├── Inventory/
│   │   │   ├── Integrations/Contracts/
│   │   │   └── Tickets/
│   │   └── Program.cs
│   ├── Jackstaff.Api.Tests/      # Workflow tests
│   └── Jackstaff.sln
├── db/init/                      # PostgreSQL/PostGIS, RLS and demo seed
├── db/migrations/                # Incremental schema/data migrations
├── docs/                         # Architecture, security, API and references
├── scripts/                      # Setup DB, launch processes, reset and checks
└── web/                          # React + Vite frontend
```

## 7. Seguridad y configuración antes de cualquier despliegue

**Los secretos incluidos son exclusivamente para desarrollo local.** Antes de desplegar:

1. Sustituye la clave JWT por un secreto aleatorio de al menos 48 bytes y guárdalo en un secret manager.
2. Cambia las contraseñas del usuario owner y `jackstaff_app`. Actualiza el rol PostgreSQL creado por `db/init/001_schema_and_demo.sql` o reemplázalo por tu propio proceso de provisioning.
3. Elimina las cuentas demo, sus hashes y el seed ficticio.
4. Usa HTTPS, CORS restringido, logging protegido y gestión de secretos por ambiente.
5. Cambia el almacenamiento del token del prototipo por cookies `HttpOnly`, `Secure`, `SameSite` y protección CSRF adecuada al despliegue. En v0.1 el token se mantiene en `sessionStorage`.
6. Añade rate limiting, política de bloqueo/recuperación de cuenta, MFA y políticas de expiración/rotación según el riesgo.
7. Revisa el modelo de permisos, auditoría, retención y requisitos legales con el cliente.

El script `scripts/Setup-Database.ps1` aplica la inicialización solo cuando no existe la migración `001_initial_v0_1` y luego aplica, en orden, las migraciones pendientes de `db/migrations/`. No edites una migración ya aplicada; agrega una nueva versión para cada cambio de esquema.

## 8. Próxima etapa

El siguiente hito debería ser la **Fase 0 de integración técnica**, no asumir que el inventario ya es telemetría. Antes de prometer localización o configuración remota de radios se debe validar fabricante, modelo, sistema de despacho, interfaces disponibles, licencias, conectividad y localización indoor.

Los contratos de proveedor deben cubrir por separado:

- `IRadioProvider`: identidad, consulta y comandos soportados por el fabricante.
- `ILocationProvider`: observaciones de ubicación con timestamp, fuente y precisión.
- `IGroupManagementProvider`: lectura/cambio de grupos cuando el vendor lo permita.
- `IDeviceEventProvider`: batería, alarmas, estado y eventos normalizados.

Los contratos de proveedor (`IRadioProvider`, `ILocationProvider`, `IGroupManagementProvider` e `IDeviceEventProvider`) ya están definidos en `Modules/Integrations/Contracts`; todavía no hay adaptadores concretos ni credenciales de fabricantes configuradas. Los adaptadores serán opcionales por tenant y no deben contaminar el dominio común con objetos específicos de Motorola, Hytera u otro proveedor.

La relación entre los repositorios de referencia y las decisiones implementadas está descrita en [`docs/reference-patterns.md`](docs/reference-patterns.md).
