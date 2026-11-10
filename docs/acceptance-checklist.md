# JACKSTAFF v0.1 — Checklist de aceptación local

Este checklist valida el comportamiento del prototipo. Los datos son ficticios; no se envían comandos a radios ni se ingiere GPS real.

## Arranque e identidad

- [ ] `scripts/Setup-Database.ps1` prepara PostgreSQL/PostGIS y aplica la migración inicial.
- [ ] `Start-JACKSTAFF.ps1` inicia la API y el frontend en dos terminales nativas.
- [ ] `http://localhost:8080/api/health` responde `status: ok`.
- [ ] `http://localhost:5173` muestra la pantalla de acceso.
- [ ] `admin@jackstaff.demo` puede ingresar con `JackstaffDemo!2026`.
- [ ] El selector permite cambiar entre Metro Demo, Security Demo y Logistics Demo.
- [ ] Inventario, instalaciones, grupos y tickets cambian al cambiar de tenant.

## Inventario y organización

- [ ] Crear una instalación con código único.
- [ ] Crear un grupo operativo.
- [ ] Registrar una radio eligiendo modelo e instalación del tenant activo.
- [ ] La radio aparece en el inventario con su etiqueta, serie, alias, modelo y grupo.
- [ ] Intentar usar un identificador de otro tenant no permite crear relaciones cruzadas.

## Mesa de ayuda

- [ ] Crear un incidente vinculado a una radio y una instalación.
- [ ] Abrir el seguimiento del ticket y agregar un comentario.
- [ ] El comentario reaparece con autor y fecha después de cerrar y volver a abrir el seguimiento.
- [ ] Crear un ticket `group_change` que solicite otra asignación.
- [ ] Mientras el ticket está abierto, en revisión o aprobado, la radio conserva su grupo efectivo.
- [ ] Llevar el ticket a `in_progress` y después a `resolved`.
- [ ] Solo después de resolverlo, la radio muestra el nuevo grupo.
- [ ] Un rol sin `tickets.manage` no puede ejecutar transiciones de workflow.

## Restablecimiento

- [ ] Detener API y frontend con `Ctrl+C` conserva la base de datos PostgreSQL.
- [ ] El script `scripts/reset-demo.ps1` elimina y reconstruye la base demo (requiere confirmar `RESET`).
