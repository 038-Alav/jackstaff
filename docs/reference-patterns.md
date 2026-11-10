# Referencias de diseño aplicadas en JACKSTAFF v0.1

JACKSTAFF es una implementación propia. Los repositorios indicados se utilizan como referencias de dominio, flujos y límites arquitectónicos; no se fusionaron sus aplicaciones ni se copió código fuente. Antes de incorporar código de terceros en futuras versiones se debe revisar su licencia y obligaciones de distribución.

## Repositorios consultados

- [Traccar](https://github.com/traccar/traccar) — motor de tracking y protocolos de dispositivos.
- [Snipe-IT](https://github.com/grokability/snipe-it) — inventario y ciclo de vida de activos.
- [GLPI](https://github.com/glpi-project/glpi) — gestión de activos y mesa de servicio.
- [Zammad](https://github.com/zammad/zammad) — experiencia de soporte y workflow de tickets.
- [Fleetbase](https://github.com/fleetbase/fleetbase) — plataforma logística modular y API-first.

| Referencia | Patrón adoptado | Implementado en v0.1 | Diferido explícitamente |
|---|---|---|---|
| Traccar | Separación entre dispositivos internos, adaptadores de protocolo, normalización de posiciones y eventos | La identidad interna de cada radio es independiente del fabricante; estado, batería y última observación están modelados como campos operacionales de demo. La arquitectura reserva contratos `IRadioProvider`, `ILocationProvider`, `IGroupManagementProvider` y `IDeviceEventProvider`. | Ingesta de protocolos, ubicación GPS/indoor, geocercas, eventos de hardware, mapa vivo y workers de telemetría. No se presenta el estado demo como telemetría real. |
| Snipe-IT | Inventario identificable por etiqueta y serie, modelo, ubicación y asignación trazable | Modelos de radio, etiqueta de activo, número de serie, instalación y asignación efectiva a grupo. Las asignaciones de grupo conservan historial y permiten una sola asignación vigente por radio. | Custodia a personas, transferencias entre usuarios, bajas, garantías, compras y ciclo de vida completo del activo. |
| GLPI | Tickets relacionados con activos y ubicaciones; trazabilidad y auditoría | Los tickets pueden vincular radio e instalación; el historial de estados y la auditoría se guardan con las operaciones. | Contratos, proveedores, CMDB avanzada, SLA parametrizables, inventario de otros tipos de activos y catálogo ITSM completo. |
| Zammad | Workflow explícito, prioridades, colas por estado y responsabilidad de soporte | Prioridades, estados, permisos por rol, transiciones restringidas, comentarios append-only e historial visible en la UI; la asignación de grupo solicitada no se convierte en efectiva hasta resolver el ticket. | Notificaciones, SLA, reglas de escalamiento y omnicanalidad. |
| Fleetbase | API-first, organización modular, configuración por organización y extensibilidad | Monolito modular, tenant activo validado por membresía, tenant_id en datos operacionales y RLS PostgreSQL; datos separados por organización. | Webhooks, extensiones instalables, marketplace, facturación, planes comerciales y portal de operadores de plataforma. |

## Decisiones de implementación

- **No fusionar repositorios completos.** JACKSTAFF mantiene un solo modelo de dominio, una API y un esquema de datos coherente.
- **No equiparar grupos operativos y talkgroups.** Los primeros organizan la operación del cliente; los talkgroups/channels son configuración técnica de un fabricante y requieren proveedores.
- **No asumir GPS disponible.** Una radio puede no entregar posición, y un entorno interior puede requerir zonas, infraestructura local o integración con un sistema de despacho.
- **No cambiar asignaciones por una solicitud pendiente.** El workflow de tickets es la puerta de cambio; la actualización del grupo efectivo se ejecuta de forma atómica cuando el ticket se resuelve.
- **Aislamiento por capas.** La API valida tenant y permisos; las consultas incluyen tenant_id; PostgreSQL aplica Row-Level Security como segunda barrera.

## Contratos previstos para proveedores

Los contratos de proveedor son el límite de integración del producto. Una implementación concreta debe ser registrada en DI por configuración de tenant y no debe filtrar DTOs de fabricante al dominio. Las credenciales de proveedor no deben guardarse en texto plano ni incorporarse al repositorio.
